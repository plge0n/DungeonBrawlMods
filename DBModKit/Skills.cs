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
    }

    /// <summary>玩家身上的一个技能（技能栏里的一格）。同一个技能实例总是对应同一个 SkillSlot。</summary>
    public sealed class SkillSlot
    {
        private static readonly ConditionalWeakTable<object, SkillSlot> cache = new ConditionalWeakTable<object, SkillSlot>();

        internal readonly object Native;

        private SkillSlot(object native) => Native = native;

        internal static SkillSlot Wrap(object native) => native == null ? null : cache.GetValue(native, n => new SkillSlot(n));

        public SkillInfo Info => SkillInfo.Get(GameRaw.SkillId(Native));
        /// <summary>右键开启了自动释放。</summary>
        public bool IsAutoCastOn => GameRaw.IsAutoCastOn(Native);
        public float CooldownRemaining => GameRaw.CooldownRemaining(Native);
        /// <summary>消耗金币而不是蓝（PayToWin 类技能）。</summary>
        public bool CostsGold => GameRaw.CostsGold(Native);
        /// <summary>对该玩家的实际消耗（已计入减耗等加成）。</summary>
        public int CostFor(Player player) => GameRaw.Cost(Native, player.Native);
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
