using System.Collections.Generic;
using DBModKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmartSkill
{
    public enum RuleMode
    {
        Vanilla, // 原版逻辑，不干预
        Custom,  // 按下面的条件
        Never,   // 开了右键自动也不放
    }

    public enum Combine
    {
        All, // 勾选的条件全部满足
        Any, // 任一满足
    }

    public enum TargetMode
    {
        Nearest,
        BossFirst, // Boss > 精英 > 最近
        LowestHp,
        HighestHp,
    }

    public class SkillRule
    {
        [JsonConverter(typeof(StringEnumConverter))] public RuleMode Mode = RuleMode.Vanilla;
        [JsonConverter(typeof(StringEnumConverter))] public Combine Combine = Combine.All;

        public bool UseHpBelow;
        public float HpBelowPercent = 50f;

        // 献祭这类扣血的技能：血量充足才放
        public bool UseHpAbove;
        public float HpAbovePercent = 60f;

        public bool UseManaAfter;
        public float ManaAfterPercent = 30f;

        public bool UseEnemyCount;
        public int MinEnemies = 2;

        public bool UseBossOrElite;

        // PayToWin 这类对 Boss 无效的技能
        public bool UseNoBoss;

        // 范围内有血量 ≤ X% 的敌人，并优先打它们
        public bool UseTargetHpBelow;
        public float TargetHpBelowPercent = 30f;

        // 有斩杀奖励的技能：范围内能一击打死的敌人 ≥ MinKillable，并优先打它们。
        // 预计伤害 × KillMarginPercent% ≥ 血量 + 护盾 才算能打死（留余量应对减伤和估算误差）
        public bool UseKillable;
        public int MinKillable = 1;
        public float KillMarginPercent = 90f;

        // 自己上一次放的效果（增益、变身、持续伤害）结束后再放
        public bool UseEffectEnded;

        // 充能技能至少留几层（留给手动放）
        public int KeepCharges;

        // 0 表示用技能自身的施法范围
        public float Radius;

        // 无指向技能不要求附近有敌人
        public bool CastWithoutEnemy;

        [JsonConverter(typeof(StringEnumConverter))] public TargetMode Target = TargetMode.Nearest;
    }

    // 规则按技能枚举名（SkillInfo.Key）存在 BepInEx/config/SmartSkill.rules.json
    internal static class RuleStore
    {
        public class Rules : Dictionary<string, SkillRule> { }

        private static JsonConfig<Rules> config;

        public static void Init(JsonConfig<Rules> c) => config = c;

        public static SkillRule Get(SkillInfo skill)
        {
            if (!config.Data.TryGetValue(skill.Key, out var rule))
            {
                rule = new SkillRule();
                config.Data[skill.Key] = rule;
            }
            return rule;
        }

        public static void Set(SkillInfo skill, SkillRule rule)
        {
            config.Data[skill.Key] = rule;
            config.MarkDirty();
        }

        public static void MarkDirty() => config.MarkDirty();

        public static void Flush() => config?.Flush();
    }
}
