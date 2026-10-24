using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DBModKit
{
    /// <summary>技能静态数据（技能表）。</summary>
    public sealed class SkillInfo
    {
        private static readonly Dictionary<int, SkillInfo> cache = new Dictionary<int, SkillInfo>();

        /// <summary>技能编号（游戏枚举 ActiveSkillEnum 的值）。</summary>
        public int Id { get; }
        /// <summary>技能枚举名，例如 D_Execution。跨版本稳定，适合作为存档 key。</summary>
        public string Key { get; }
        /// <summary>指示器类型：None / Switch（无需瞄准）、Range（地面范围）、Vector（方向）、Target（锁定目标）。</summary>
        public string Indicator { get; }
        public float CastingRange { get; }
        public float Range { get; }
        public float Cooldown { get; }
        /// <summary>允许右键开启自动释放。</summary>
        public bool CanAutoCast { get; }
        /// <summary>技能标签（SkillTag 枚举名），常用值见 <see cref="SkillTags"/>。</summary>
        public IReadOnlyList<string> Tags { get; }

        internal SkillInfo(int id, string key, string indicator, float castingRange, float range, float cooldown, bool canAutoCast, IReadOnlyList<string> tags)
        {
            Id = id;
            Key = key;
            Indicator = indicator;
            CastingRange = castingRange;
            Range = range;
            Cooldown = cooldown;
            CanAutoCast = canAutoCast;
            Tags = tags;
        }

        // 技能表被 GameData 改动后丢掉缓存
        internal static void Invalidate(int id) => cache.Remove(id);

        /// <summary>按枚举名（<see cref="Key"/>）查找。</summary>
        public static SkillInfo Find(string key)
        {
            if (!ModKit.Compatible || !GameData.Ready) return null;
            return GameRaw.SkillIdOf(key) is int id ? Get(id) : null;
        }

        /// <summary>技能表里的所有技能（游戏表加载后才有）。</summary>
        public static List<SkillInfo> All()
        {
            var list = new List<SkillInfo>();
            foreach (var e in GameData.Entries(DataTable.Skill))
            {
                var info = Find(e.Key);
                if (info != null) list.Add(info);
            }
            return list;
        }

        public static SkillInfo Get(int id)
        {
            if (!ModKit.Compatible) return null;
            if (!cache.TryGetValue(id, out var info))
            {
                info = GameRaw.ReadSkillInfo(id);
                if (info != null) cache[id] = info;
            }
            return info;
        }

        /// <summary>本地化后的技能名，与技能提示框一致。</summary>
        public string Name => Game.Localize("a_" + Id, Key);

        /// <summary>有效施法范围：优先 castingRange，否则 range。</summary>
        public float CastRange => CastingRange > 0f ? CastingRange : Range;

        /// <summary>无需瞄准（None / Switch），可以不依赖敌人位置直接释放。</summary>
        public bool IsSelfCastable => Indicator == "None" || Indicator == "Switch";

        /// <summary>开关类技能：放一次打开，再放一次关闭。</summary>
        public bool IsToggle => Indicator == "Switch";

        /// <summary>有斩杀奖励：处决（属性）、点金术（金币）、撕咬（最大血量）。支持 <see cref="SkillSlot.EstimateDamage"/>。</summary>
        public bool HasKillReward => Key.EndsWith("_Execution") || Key.EndsWith("_Alchemy") || Key == "Bite";

        public bool HasTag(string tag)
        {
            foreach (var t in Tags)
                if (t == tag) return true;
            return false;
        }

        public static string TagDisplayName(string tag) => Game.Localize("skillTag_" + tag, tag);
    }

    /// <summary>常用技能标签。</summary>
    public static class SkillTags
    {
        public const string Heal = "Heal";
        public const string Revive = "Revive";
        public const string Shield = "Shield";
        public const string Defense = "Defense";
        public const string Buff = "Buff";
        public const string Summon = "Summon";
        public const string Aura = "Aura";
        public const string Area = "Area";
        public const string Move = "Move";
        public const string Control = "Control";
        public const string Henshin = "Henshin";
        public const string Duration = "Duration";
    }

    /// <summary>玩家身上的一个技能（技能栏里的一格）。同一个技能实例总是对应同一个 SkillSlot。</summary>
    public sealed class SkillSlot
    {
        private static readonly ConditionalWeakTable<object, SkillSlot> cache = new ConditionalWeakTable<object, SkillSlot>();

        internal readonly object Native;

        private SkillSlot(object native) => Native = native;

        internal static SkillSlot Wrap(object native) => native == null ? null : cache.GetValue(native, n => new SkillSlot(n));

        public SkillInfo Info => SkillInfo.Get(GameRaw.SkillId(Native));
        /// <summary>技能编号（ActiveSkillEnum 的值）；被动格是 -1（None），这时 <see cref="Info"/> 为 null。</summary>
        public int SkillId => GameRaw.SkillId(Native);
        /// <summary>冷却总时长（技能实例上的值，已含 <see cref="SkillTuning"/> 的调整）。</summary>
        public float CooldownTotal => GameRaw.CooldownTotal(Native);

        /// <summary>缩短剩余冷却（秒，最多缩到 0）。只能用于本地玩家的技能。</summary>
        public void ReduceCooldown(float seconds)
        {
            if (seconds <= 0f || GameRaw.SkillOwner(Native) != GameRaw.LocalPlayer()) return;
            GameRaw.SetCooldownRemaining(Native, Mathf.Max(0f, CooldownRemaining - seconds));
        }
        /// <summary>冷却立刻转好（充能技能只转好正在冷却的这一格）。只能用于本地玩家的技能。</summary>
        public void ResetCooldown() => ReduceCooldown(CooldownRemaining);

        /// <summary>右键开启了自动释放。</summary>
        public bool IsAutoCastOn => GameRaw.IsAutoCastOn(Native);

        /// <summary>在技能栏上（有按钮正显示这个技能）。</summary>
        public bool IsOnSkillBar => GameRaw.IsOnSkillBar(Native);

        /// <summary>
        /// 打开或关闭自动释放（和右键技能图标一样，图标上的自动标记跟着变）。只能用于本地玩家的技能；
        /// 表里不允许自动的技能（<see cref="SkillInfo.CanAutoCast"/> 为 false）不会打开。玩家之后仍然可以右键关掉。
        /// </summary>
        public void SetAutoCast(bool on)
        {
            if (GameRaw.SkillOwner(Native) != GameRaw.LocalPlayer()) return;
            if (on && Info?.CanAutoCast != true) return;
            GameRaw.SetAutoCast(Native, on);
        }
        public float CooldownRemaining => GameRaw.CooldownRemaining(Native);
        /// <summary>消耗金币而不是蓝（PayToWin 类技能）。</summary>
        public bool CostsGold => GameRaw.CostsGold(Native);
        /// <summary>对该玩家的实际消耗（已计入减耗等加成）。</summary>
        public int CostFor(Player player) => GameRaw.Cost(Native, player.Native);
        /// <summary>开关类技能（献祭等，指示器为 Switch）当前开着。开着时自动释放会把它关掉。</summary>
        public bool IsToggledOn => GameRaw.IsToggledOn(Native);
        /// <summary>当前充能层数；没有充能机制的技能为 0（看 <see cref="MaxCharges"/>）。</summary>
        public int Charges => GameRaw.Charges(Native);
        public int MaxCharges => GameRaw.MaxCharges(Native);

        /// <summary>
        /// 估算这个技能打在目标身上的伤害（未计暴击、目标减伤和各种触发效果）。
        /// 只支持有斩杀奖励的技能（处决、点金术、撕咬），其他技能返回 null。
        /// 已乘上 <see cref="Player.DamageBonusAgainst"/>。
        /// </summary>
        public long? EstimateDamage(Player player, Role target)
        {
            var raw = GameRaw.BaseSkillDamage(GameRaw.SkillId(Native), player.Native, target.Native);
            if (raw == null) return null;
            return (long)(raw.Value * (1.0 + player.DamageBonusAgainst(target)));
        }
    }

    /// <summary>技能栏上的一个按钮。</summary>
    public sealed class SkillButton
    {
        /// <summary>按钮上的技能；空格子为 null。</summary>
        public SkillSlot Slot { get; internal set; }
        /// <summary>技能图标（150×150，挂在战斗 UI 的 Canvas 下），可以在上面加自己的 UI。</summary>
        public RectTransform Icon { get; internal set; }
    }

    /// <summary>本地玩家的技能栏。</summary>
    public static class SkillBar
    {
        private static readonly List<(object button, object skill, RectTransform icon)> raw = new List<(object, object, RectTransform)>();
        private static readonly ConditionalWeakTable<object, SkillButton> buttons = new ConditionalWeakTable<object, SkillButton>();

        /// <summary>当前技能栏按钮（含空格子）；不在战斗中时为空。</summary>
        public static List<SkillButton> Buttons()
        {
            var result = new List<SkillButton>();
            if (!ModKit.Compatible) return result;
            GameRaw.ReadSkillBar(raw);
            foreach (var (button, skill, icon) in raw)
            {
                var b = buttons.GetValue(button, _ => new SkillButton());
                b.Slot = SkillSlot.Wrap(skill);
                b.Icon = icon;
                result.Add(b);
            }
            return result;
        }

        /// <summary>按技能编号找技能栏里的技能。</summary>
        public static SkillSlot Find(int skillId)
        {
            foreach (var b in Buttons())
                if (b.Slot != null && GameRaw.SkillId(b.Slot.Native) == skillId)
                    return b.Slot;
            return null;
        }
    }
}
