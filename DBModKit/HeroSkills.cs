using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBModKit
{
    /// <summary>英雄的专属技能（hero 表的 skill 列，出生时发放；每个英雄一个，主动或被动）。</summary>
    public sealed class HeroSkill
    {
        private static readonly Dictionary<int, HeroSkill> cache = new Dictionary<int, HeroSkill>();

        public int HeroId { get; }
        /// <summary>主动技能是 ActiveSkillEnum 的值，被动技能是被动表的编号（PasssiveSkillEnum 的值）。</summary>
        public int SkillId { get; }
        public bool IsActive { get; }
        public string IconPath { get; }

        /// <summary>主动技能的表数据；被动技能为 null。</summary>
        public SkillInfo Info { get; }
        public float Cost { get; }
        public float Duration { get; }
        /// <summary>充能数（0 表示没有充能机制）。</summary>
        public int Charges { get; }
        public float DamageBase { get; }
        public float DamageValue { get; }

        /// <summary>被动技能表里的数值（value 列，"|" 分隔）；主动技能为空。</summary>
        public IReadOnlyList<float> PassiveValues { get; }

        private HeroSkill(int heroId, int skillId, bool active)
        {
            HeroId = heroId;
            SkillId = skillId;
            IsActive = active;
            if (active)
            {
                Info = SkillInfo.Get(skillId);
                IconPath = GameRaw.SkillIcon(skillId);
                if (GameRaw.ReadSkillNumbers(skillId, out float cost, out float duration, out int charges, out float damageBase, out float damageValue))
                {
                    Cost = cost;
                    Duration = duration;
                    Charges = charges;
                    DamageBase = damageBase;
                    DamageValue = damageValue;
                }
                PassiveValues = new float[0];
            }
            else
            {
                GameRaw.ReadPassive(skillId, out string icon, out float[] values);
                IconPath = icon;
                PassiveValues = values;
            }
        }

        /// <summary>本地化后的技能名。</summary>
        public string Name => Game.Localize((IsActive ? "a_" : "p_") + SkillId, SkillId.ToString());

        public float Cooldown => Info?.Cooldown ?? 0f;

        /// <summary>某个英雄（HeroType 的值）的专属技能；表没加载或没有这个英雄时为 null。</summary>
        public static HeroSkill Of(int heroId)
        {
            if (!ModKit.Compatible || !GameData.Ready) return null;
            if (!cache.TryGetValue(heroId, out var skill))
            {
                if (!GameRaw.ReadHeroSkill(heroId, out int id, out bool active)) return null;
                cache[heroId] = skill = new HeroSkill(heroId, id, active);
            }
            return skill;
        }
    }

    /// <summary>一次技能释放（本地玩家）。</summary>
    public readonly struct SkillCast
    {
        public int SkillId { get; }
        /// <summary>目标点（地面范围类）或自己的位置。</summary>
        public Vector3 Position { get; }
        /// <summary>朝向（角度）。</summary>
        public float Rotation { get; }
        /// <summary>目标的 netId，没有目标时为 -1。</summary>
        public int TargetId { get; }
        /// <summary>技能书编号（技能栏的哪一格）。</summary>
        public int BookId { get; }
        /// <summary>免费释放（不扣蓝、不进冷却），例如 <see cref="Player.FreeCast"/> 或游戏的"多重施法"。</summary>
        public bool IsFree { get; }

        internal SkillCast(int skillId, Vector3 position, float rotation, int targetId, int bookId, bool isFree)
        {
            SkillId = skillId;
            Position = position;
            Rotation = rotation;
            TargetId = targetId;
            BookId = bookId;
            IsFree = isFree;
        }
    }

    /// <summary>一个主动技能的调整（倍率）。只作用于本地玩家，配置表本身保持原版。</summary>
    public struct SkillTune
    {
        /// <summary>伤害倍率：只对能归属到这个技能的命中生效（命中带技能来源）。</summary>
        public float Damage;
        public float Cooldown;
        public float Cost;
        public float Duration;
        /// <summary>额外充能层数（只对本来就有充能的技能有意义）。</summary>
        public int ExtraCharges;

        public static SkillTune None => new SkillTune { Damage = 1f, Cooldown = 1f, Cost = 1f, Duration = 1f };

        public bool IsNone => Damage == 1f && Cooldown == 1f && Cost == 1f && Duration == 1f && ExtraCharges == 0;

        internal static SkillTune Combine(SkillTune a, SkillTune b) => new SkillTune
        {
            Damage = a.Damage * b.Damage,
            Cooldown = a.Cooldown * b.Cooldown,
            Cost = a.Cost * b.Cost,
            Duration = a.Duration * b.Duration,
            ExtraCharges = a.ExtraCharges + b.ExtraCharges,
        };

        public override string ToString()
            => $"伤害×{Damage:0.##} 冷却×{Cooldown:0.##} 蓝耗×{Cost:0.##} 持续×{Duration:0.##} 充能{ExtraCharges:+0;-0;0}";
    }

    /// <summary>
    /// 只对本地玩家生效的主动技能调整（用 <see cref="ModContext.TuneSkill"/> 设置，卸载时自动撤销）。
    /// 伤害按命中乘倍率；冷却、蓝耗、充能直接改技能实例；蓝耗和持续时间在释放时临时替换配置表。
    /// 多个 Mod 调同一个技能时倍率相乘。
    /// </summary>
    public static class SkillTuning
    {
        /// <summary>需要的补丁都装上了。</summary>
        public static bool Available { get; internal set; }

        private static readonly Dictionary<(string owner, int id), SkillTune> entries = new Dictionary<(string, int), SkillTune>();
        private static readonly Dictionary<int, SkillTune> combined = new Dictionary<int, SkillTune>();

        // 技能实例上原来的值和我们写进去的值，按技能编号记（重试波次时游戏可能换掉实例对象）
        private sealed class Record
        {
            public float BaseCooldown, WrittenCooldown;
            public int BaseCost, WrittenCost;
            public int BaseCharges, WrittenCharges;

            public Record Clone() => (Record)MemberwiseClone();
        }

        private static Dictionary<int, Record> records = new Dictionary<int, Record>();
        private static Dictionary<int, Record> checkpoint;
        private static Component recordsOwner;

        internal static void Set(string owner, int skillId, SkillTune tune)
        {
            if (tune.IsNone) entries.Remove((owner, skillId));
            else entries[(owner, skillId)] = tune;
            Recombine(skillId);
            RunLog.Info(owner, "技能调整", tune.IsNone ? "撤销" : "设置", ("技能", SkillInfo.Get(skillId)?.Name ?? skillId.ToString()), ("合计", Get(skillId).ToString()));
        }

        internal static void ClearOwner(string owner)
        {
            var ids = new List<int>();
            foreach (var key in entries.Keys)
                if (key.owner == owner) ids.Add(key.id);
            foreach (var id in ids)
            {
                entries.Remove((owner, id));
                Recombine(id);
            }
        }

        private static void Recombine(int skillId)
        {
            var total = SkillTune.None;
            foreach (var kv in entries)
                if (kv.Key.id == skillId) total = SkillTune.Combine(total, kv.Value);
            if (total.IsNone) combined.Remove(skillId);
            else combined[skillId] = total;
        }

        /// <summary>这个技能当前的合计调整（没有时是 <see cref="SkillTune.None"/>）。</summary>
        public static SkillTune Get(int skillId) => combined.TryGetValue(skillId, out var t) ? t : SkillTune.None;

        // 每帧：把冷却、蓝耗、充能写进本地玩家的技能实例
        internal static void Tick()
        {
            var player = GameRaw.LocalPlayer();
            if (player != recordsOwner)
            {
                // 换了角色（新的一局）：旧记录作废
                records.Clear();
                checkpoint = null;
                recordsOwner = player;
            }
            if (player == null || (combined.Count == 0 && records.Count == 0)) return;

            List<int> finished = null;
            foreach (var kv in records)
                if (!combined.ContainsKey(kv.Key)) (finished ?? (finished = new List<int>())).Add(kv.Key);
            if (finished != null)
                foreach (var id in finished)
                {
                    var skill = GameRaw.FindActiveSkill(player, id);
                    if (skill != null) Write(skill, records[id], SkillTune.None);
                    records.Remove(id);
                }

            foreach (var kv in combined)
            {
                var skill = GameRaw.FindActiveSkill(player, kv.Key);
                if (skill == null) continue;
                if (!records.TryGetValue(kv.Key, out var r))
                {
                    records[kv.Key] = r = new Record
                    {
                        BaseCooldown = GameRaw.CooldownTotal(skill),
                        BaseCost = GameRaw.BaseCost(skill),
                        BaseCharges = GameRaw.MaxCharges(skill),
                    };
                    r.WrittenCooldown = r.BaseCooldown;
                    r.WrittenCost = r.BaseCost;
                    r.WrittenCharges = r.BaseCharges;
                }
                // 游戏自己改过（卡牌、升级等）：以游戏的新值为原值
                if (!Mathf.Approximately(GameRaw.CooldownTotal(skill), r.WrittenCooldown)) r.BaseCooldown = GameRaw.CooldownTotal(skill);
                if (GameRaw.BaseCost(skill) != r.WrittenCost) r.BaseCost = GameRaw.BaseCost(skill);
                if (GameRaw.MaxCharges(skill) != r.WrittenCharges) r.BaseCharges = GameRaw.MaxCharges(skill);
                Write(skill, r, kv.Value);
            }
        }

        private static void Write(object skill, Record r, SkillTune tune)
        {
            r.WrittenCooldown = r.BaseCooldown * tune.Cooldown;
            r.WrittenCost = Mathf.Max(0, Mathf.RoundToInt(r.BaseCost * tune.Cost));
            r.WrittenCharges = r.BaseCharges > 0 ? Mathf.Max(1, r.BaseCharges + tune.ExtraCharges) : r.BaseCharges;
            if (!Mathf.Approximately(GameRaw.CooldownTotal(skill), r.WrittenCooldown)) GameRaw.SetCooldownTotal(skill, r.WrittenCooldown);
            if (GameRaw.BaseCost(skill) != r.WrittenCost) GameRaw.SetBaseCost(skill, r.WrittenCost);
            if (GameRaw.MaxCharges(skill) != r.WrittenCharges) GameRaw.SetMaxCharges(skill, r.WrittenCharges);
        }

        // 重试波次：游戏把技能实例的字段还原到快照时的值，记录也跟着还原，否则会把我们写的值当成原值再乘一次
        internal static void SaveCheckpoint()
        {
            checkpoint = new Dictionary<int, Record>();
            foreach (var kv in records) checkpoint[kv.Key] = kv.Value.Clone();
        }

        internal static void RestoreCheckpoint()
        {
            if (checkpoint == null) return;
            records = new Dictionary<int, Record>();
            foreach (var kv in checkpoint) records[kv.Key] = kv.Value.Clone();
        }
    }
}
