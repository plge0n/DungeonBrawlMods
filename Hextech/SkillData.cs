using System.Collections.Generic;

namespace Hextech
{
    // 生成器用到的技能数据。只用普通 C# 类型，不碰 ModKit 和 Unity，生成器可以脱离游戏单独测试
    internal sealed class SkillProfile
    {
        public int SkillId;
        public bool Active;
        public string Name = "";

        // ---- 主动技能（表里的原始值）----
        public float Cooldown, Cost, Duration;
        public int Charges;
        // 伤害能归属到这个技能（命中带技能来源）："威力""收割"只在这时出现
        public bool Damage;
        // 持续时间越长越好（炸弹的引信、冲锋的冲刺时长不算）
        public bool DurationGood;
        // 变身、开关、召唤、技能窃取、冲刺中会被游戏拦下等，不出"回响"
        public bool NoEcho;
        // 技能本身带无敌，不出"无敌"
        public bool HasInvulnerable;

        // ---- 被动技能 ----
        public PassiveParam[] Params = new PassiveParam[0];
        public float[] BaseValues = new float[0];
    }

    // 被动技能 value 里的一项（含义以代码为准，见 NOTES.md "专属技能强化"）
    internal sealed class PassiveParam
    {
        public int Index;
        public string Label;
        // +1：越大越好；-1：越小越好
        public int Better = 1;
        // 每一份强化改多少：Relative 时是原值的比例（0.25 = 原值的 25%），否则是绝对值
        public float Step;
        public bool Relative;
        public float Points = 2f;
        public float Min = float.MinValue, Max = float.MaxValue;
        public string Unit = "";
    }

    internal static class SkillData
    {
        // 主动专属技能（ActiveSkillEnum 值）的手工标注，依据第 0 期调研：
        // 伤害能归属：命中带技能实例（闪烁、领域、炸弹、撕咬、雷之一闪实测或查证过）；拔刀术是普攻伤害、演奏和扣篮没有技能实例，都归属不了
        private static readonly Dictionary<int, (bool damage, bool durationGood, bool noEcho, bool invulnerable)> active =
            new Dictionary<int, (bool, bool, bool, bool)>
            {
                [500] = (true, false, false, false),  // 闪烁
                [501] = (false, false, false, true),  // 拔刀术：伤害写死在代码里，斩击后无敌 0.5 秒
                [502] = (false, false, true, false),  // 演奏音乐：演奏中再放会打断
                [503] = (true, true, false, false),   // 领域展开
                [504] = (false, false, true, true),   // 翻滚：翻滚中无敌
                [505] = (false, true, true, false),   // 进击的巨人：变身
                [506] = (false, false, false, false), // 制作蟹黄堡
                [507] = (false, true, true, false),   // 召唤祢豆子
                [508] = (false, false, true, false),  // 技能窃取
                [509] = (false, false, false, false), // 花式扣篮（拿到鸡舞卡牌后是真·鸡舞 515，强化跟着走）
                [510] = (false, true, true, false),   // 黑暗形态：变身
                [511] = (false, true, true, false),   // 影分身之术
                [512] = (false, false, false, false), // 噬魂一击
                [513] = (true, false, false, false),  // 炸弹！：duration 是引信时长，加长反而吃亏
                [514] = (false, false, true, false),  // 我没有开挂：会弹调整属性的选择
                [516] = (false, true, true, false),   // 氪金：花金币，再放一次等于白扣钱
                [517] = (false, false, true, false),  // 形态切换：开关
                [518] = (true, false, false, false),  // 撕咬
                [519] = (false, true, false, false),  // 预借
                [520] = (false, false, false, false), // 冲锋装填：duration 是冲刺时长
                [521] = (true, false, true, false),   // 雷之一闪：连闪中再按只会提前结束
            };

        // 有参数的 11 个被动（被动表编号）。没参数的 10 个第一期不开
        private static readonly Dictionary<int, PassiveParam[]> passive = new Dictionary<int, PassiveParam[]>
        {
            [501] = new[] // 起洞：概率%、基础伤害、力量系数
            {
                new PassiveParam { Index = 0, Label = "打洞概率", Step = 4f, Unit = "%", Max = 60f },
                new PassiveParam { Index = 1, Label = "打洞基础伤害", Step = 0.25f, Relative = true, Points = 1f },
                new PassiveParam { Index = 2, Label = "打洞力量系数", Step = 0.25f, Relative = true },
            },
            [502] = new[] { new PassiveParam { Index = 0, Label = "每本技能书加的三维", Step = 0.25f, Relative = true } },
            [503] = new[] { new PassiveParam { Index = 0, Label = "每次死亡加的三维", Step = 0.25f, Relative = true } },
            // 内部折扣：value 是价格倍率（70 = 按原价 70% 卖），越小越好
            [504] = new[] { new PassiveParam { Index = 0, Label = "商店价格", Better = -1, Step = 5f, Unit = "%", Min = 30f } },
            [505] = new[] { new PassiveParam { Index = 0, Label = "每 60 秒的三维成长", Step = 0.25f, Relative = true } },
            [506] = new[] // 对死者的供奉：伤害 =（敏+力）×([0] + 等级 × [1])
            {
                new PassiveParam { Index = 0, Label = "死亡爆炸基础系数", Step = 0.25f, Relative = true, Points = 1f },
                new PassiveParam { Index = 1, Label = "死亡爆炸每级系数", Step = 0.25f, Relative = true },
            },
            [511] = new[] // 电锯恶魔：攻击附加目标最大生命%、被普通敌人打时扣自身最大生命%、被 Boss 打时扣自身最大生命%
            {
                new PassiveParam { Index = 0, Label = "攻击附加目标最大生命", Step = 1f, Unit = "%" },
                new PassiveParam { Index = 1, Label = "被普通敌人打时扣自身最大生命", Better = -1, Step = 0.3f, Unit = "%", Min = 0.5f },
                new PassiveParam { Index = 2, Label = "被 Boss 打时扣自身最大生命", Better = -1, Step = 1f, Unit = "%", Min = 1f, Points = 1f },
            },
            [513] = new[] { new PassiveParam { Index = 0, Label = "技能书升级概率", Step = 8f, Unit = "%", Max = 100f } },
            [514] = new[] { new PassiveParam { Index = 0, Label = "爆炸力量系数", Step = 0.2f, Relative = true } },
            // 橡胶果实：value 是承受伤害的比例，越小越好
            [515] = new[] { new PassiveParam { Index = 0, Label = "承受伤害比例", Better = -1, Step = 5f, Unit = "%", Min = 25f } },
            [521] = new[] { new PassiveParam { Index = 0, Label = "爆炸力量系数", Step = 0.2f, Relative = true } },
        };

        public static bool Supported(bool isActive, int skillId) => isActive ? active.ContainsKey(skillId) : passive.ContainsKey(skillId);

        public static SkillProfile Active(int skillId, string name, float cooldown, float cost, float duration, int charges)
        {
            var flags = active[skillId];
            return new SkillProfile
            {
                SkillId = skillId,
                Active = true,
                Name = name,
                Cooldown = cooldown,
                Cost = cost,
                Duration = duration,
                Charges = charges,
                Damage = flags.damage,
                DurationGood = flags.durationGood && duration > 0f,
                NoEcho = flags.noEcho,
                HasInvulnerable = flags.invulnerable,
            };
        }

        public static SkillProfile Passive(int skillId, string name, float[] baseValues)
        {
            var list = new List<PassiveParam>();
            foreach (var p in passive[skillId])
                if (p.Index < baseValues.Length) list.Add(p);
            return new SkillProfile { SkillId = skillId, Name = name, Params = list.ToArray(), BaseValues = baseValues };
        }
    }
}
