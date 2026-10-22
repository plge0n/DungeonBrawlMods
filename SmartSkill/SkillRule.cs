using System;
using System.Collections.Generic;
using System.IO;
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

        public bool UseManaAfter;
        public float ManaAfterPercent = 30f;

        public bool UseEnemyCount;
        public int MinEnemies = 2;

        public bool UseBossOrElite;

        // 0 表示用技能自身的施法范围
        public float Radius;

        // 无指向技能不要求附近有敌人
        public bool CastWithoutEnemy;

        [JsonConverter(typeof(StringEnumConverter))] public TargetMode Target = TargetMode.Nearest;

        public bool AnyConditionEnabled => UseHpBelow || UseManaAfter || UseEnemyCount || UseBossOrElite;
    }

    internal static class RuleStore
    {
        private static Dictionary<ActiveSkillEnum, SkillRule> rules = new Dictionary<ActiveSkillEnum, SkillRule>();
        private static string path;

        public static void Load(string configDir)
        {
            path = Path.Combine(configDir, "SmartSkill.rules.json");
            if (!File.Exists(path)) return;
            try
            {
                var loaded = JsonConvert.DeserializeObject<Dictionary<string, SkillRule>>(File.ReadAllText(path));
                foreach (var kv in loaded)
                    if (Enum.TryParse(kv.Key, out ActiveSkillEnum skill))
                        rules[skill] = kv.Value;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"读取 {path} 失败，使用默认规则：{e.Message}");
            }
        }

        private static bool dirty;
        private static float lastSaveTime;

        // 拖动滑条时每帧都会改值，标记后隔一秒才落盘
        public static void MarkDirty() => dirty = true;

        public static void Tick()
        {
            if (dirty && UnityEngine.Time.unscaledTime - lastSaveTime > 1f) Flush();
        }

        public static void Flush()
        {
            if (!dirty) return;
            Save();
            dirty = false;
            lastSaveTime = UnityEngine.Time.unscaledTime;
        }

        private static void Save()
        {
            var output = new Dictionary<string, SkillRule>();
            foreach (var kv in rules)
                output[kv.Key.ToString()] = kv.Value;
            File.WriteAllText(path, JsonConvert.SerializeObject(output, Formatting.Indented));
        }

        public static SkillRule Get(ActiveSkillEnum skill)
        {
            if (!rules.TryGetValue(skill, out var rule))
            {
                rule = new SkillRule();
                rules[skill] = rule;
            }
            return rule;
        }

        public static void Set(ActiveSkillEnum skill, SkillRule rule) => rules[skill] = rule;
    }
}
