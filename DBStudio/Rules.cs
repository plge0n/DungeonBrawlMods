using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DBStudio
{
    // 规则：当【触发】时，如果【条件全部满足】，就【做这些事】
    internal enum Trigger
    {
        EnemyKilled,
        EliteKilled, // 精英或 Boss
        BossKilled,
        SkillCast,
        Critical,
        Dodged,
        Damaged,
        LevelUp,
        Revived,
        ItemPicked,
        WaveStarted,
        WaveEnded,
        BossAppeared,
        Interval, // 战斗中每隔几秒
    }

    internal enum ConditionType
    {
        HpBelow,      // 自身血量 ≤ x%
        HpAbove,      // 自身血量 ≥ x%
        ManaBelow,    // 蓝量 ≤ x%
        ManaAbove,    // 蓝量 ≥ x%
        EnemiesNear,  // 8 米内敌人 ≥ x 个
        WaveAtLeast,  // 波次 ≥ x
        Chance,       // x% 的概率
    }

    internal enum ActionType
    {
        HealPercent,  // 回复 x% 生命上限
        Heal,         // 回复 x 点生命
        Mana,         // 回复 x 点蓝
        Gold,         // 获得 x 金币
        Attack,       // 攻击 +x
        Strength,
        Agility,
        Stamina,
        Notice,       // 屏幕上显示一句话
    }

    internal sealed class Condition
    {
        [JsonConverter(typeof(StringEnumConverter))] public ConditionType Type = ConditionType.HpBelow;
        public double Value = 50;
    }

    internal sealed class RuleAction
    {
        [JsonConverter(typeof(StringEnumConverter))] public ActionType Type = ActionType.HealPercent;
        public double Value = 10;
        // 加属性的持续秒数；0 表示本局一直有效
        public double Seconds = 10;
        public string Text = "";
    }

    internal sealed class Rule
    {
        public string Name = "新规则";
        public bool Enabled = true;
        [JsonConverter(typeof(StringEnumConverter))] public Trigger Trigger = Trigger.EnemyKilled;
        // Interval 触发的间隔秒数
        public double Every = 10;
        public List<Condition> Conditions = new List<Condition>();
        // 默认必须是空列表：Newtonsoft 读档时会往已有的列表里追加
        public List<RuleAction> Actions = new List<RuleAction>();
        // 触发后多少秒内不再触发
        public double Cooldown;
        // 每局最多触发几次，0 = 不限
        public int MaxPerBattle;
    }

    // 下拉框里的文字、单位，以及把规则念成一句话
    internal static class RuleText
    {
        public const float NearRange = 8f;

        public static readonly (Trigger value, string label)[] Triggers =
        {
            (Trigger.EnemyKilled, "击杀敌人时"),
            (Trigger.EliteKilled, "击杀精英或 Boss 时"),
            (Trigger.BossKilled, "击杀 Boss 时"),
            (Trigger.SkillCast, "放出技能时"),
            (Trigger.Critical, "打出暴击时"),
            (Trigger.Dodged, "闪避攻击时"),
            (Trigger.Damaged, "受到伤害时"),
            (Trigger.LevelUp, "升级时"),
            (Trigger.Revived, "复活时"),
            (Trigger.ItemPicked, "捡到东西时"),
            (Trigger.WaveStarted, "波次开始时"),
            (Trigger.WaveEnded, "波次结束时"),
            (Trigger.BossAppeared, "Boss 出场时"),
            (Trigger.Interval, "战斗中每隔几秒"),
        };

        public static readonly (ConditionType value, string label, string unit)[] Conditions =
        {
            (ConditionType.HpBelow, "自身血量不超过", "%"),
            (ConditionType.HpAbove, "自身血量至少", "%"),
            (ConditionType.ManaBelow, "蓝量不超过", "%"),
            (ConditionType.ManaAbove, "蓝量至少", "%"),
            (ConditionType.EnemiesNear, $"{NearRange:0} 米内敌人至少", "个"),
            (ConditionType.WaveAtLeast, "波次至少", "波"),
            (ConditionType.Chance, "概率", "%"),
        };

        public static readonly (ActionType value, string label, string unit)[] Actions =
        {
            (ActionType.HealPercent, "回复生命上限的", "%"),
            (ActionType.Heal, "回复生命", "点"),
            (ActionType.Mana, "回复蓝量", "点"),
            (ActionType.Gold, "获得金币", "个"),
            (ActionType.Attack, "攻击加", ""),
            (ActionType.Strength, "力量加", ""),
            (ActionType.Agility, "敏捷加", ""),
            (ActionType.Stamina, "体力加", ""),
            (ActionType.Notice, "屏幕提示", ""),
        };

        public static bool IsStat(ActionType t) => t == ActionType.Attack || t == ActionType.Strength || t == ActionType.Agility || t == ActionType.Stamina;

        public static string Label(Trigger t) => Triggers.First(x => x.value == t).label;
        public static (string label, string unit) Label(ConditionType t) { var x = Conditions.First(c => c.value == t); return (x.label, x.unit); }
        public static (string label, string unit) Label(ActionType t) { var x = Actions.First(a => a.value == t); return (x.label, x.unit); }

        // 例："击杀敌人时，如果自身血量不超过 50%，就回复生命上限的 10%（冷却 1 秒）"
        public static string Sentence(Rule r)
        {
            string when = r.Trigger == Trigger.Interval ? $"战斗中每隔 {Num(r.Every)} 秒" : Label(r.Trigger);
            string cond = r.Conditions.Count == 0 ? "" : "，如果" + string.Join("、", r.Conditions.Select(c =>
            {
                var (label, unit) = Label(c.Type);
                return $"{label} {Num(c.Value)}{unit}";
            }));
            string act = r.Actions.Count == 0 ? "什么也不做" : string.Join("，并", r.Actions.Select(ActionText));
            var limits = new List<string>();
            if (r.Cooldown > 0) limits.Add($"冷却 {Num(r.Cooldown)} 秒");
            if (r.MaxPerBattle > 0) limits.Add($"每局最多 {r.MaxPerBattle} 次");
            return $"{when}{cond}，就{act}" + (limits.Count > 0 ? $"（{string.Join("，", limits)}）" : "");
        }

        public static string ActionText(RuleAction a)
        {
            if (a.Type == ActionType.Notice) return $"提示「{a.Text}」";
            var (label, unit) = Label(a.Type);
            string text = $"{label} {Num(a.Value)}{unit}";
            if (IsStat(a.Type)) text += a.Seconds > 0 ? $"，持续 {Num(a.Seconds)} 秒" : "，本局有效";
            return text;
        }

        public static string Num(double v) => v.ToString("0.##");
    }

    // 新建规则时可选的模板，零基础用户改改数字就能用
    internal static class RuleTemplates
    {
        public static readonly (string label, System.Func<Rule> make)[] All =
        {
            ("空白规则", () => new Rule { Actions = { new RuleAction() } }),
            ("击杀回血", () => new Rule
            {
                Name = "击杀回血",
                Trigger = Trigger.EnemyKilled,
                Actions = { new RuleAction { Type = ActionType.HealPercent, Value = 3 } },
                Cooldown = 0.5,
            }),
            ("残血自救", () => new Rule
            {
                Name = "残血自救",
                Trigger = Trigger.Damaged,
                Conditions = { new Condition { Type = ConditionType.HpBelow, Value = 30 } },
                Actions =
                {
                    new RuleAction { Type = ActionType.HealPercent, Value = 30 },
                    new RuleAction { Type = ActionType.Notice, Text = "残血自救！" },
                },
                Cooldown = 60,
            }),
            ("每波开始加攻击", () => new Rule
            {
                Name = "每波开始加攻击",
                Trigger = Trigger.WaveStarted,
                Actions = { new RuleAction { Type = ActionType.Attack, Value = 5, Seconds = 0 } },
            }),
            ("暴击得金币", () => new Rule
            {
                Name = "暴击得金币",
                Trigger = Trigger.Critical,
                Conditions = { new Condition { Type = ConditionType.Chance, Value = 20 } },
                Actions = { new RuleAction { Type = ActionType.Gold, Value = 1 } },
            }),
            ("被围攻时狂暴", () => new Rule
            {
                Name = "被围攻时狂暴",
                Trigger = Trigger.Interval,
                Every = 1,
                Conditions = { new Condition { Type = ConditionType.EnemiesNear, Value = 5 } },
                Actions = { new RuleAction { Type = ActionType.Strength, Value = 20, Seconds = 5 } },
                Cooldown = 15,
            }),
        };
    }
}
