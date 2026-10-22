using DBModKit;

namespace SmartSkill
{
    // "按标签推荐"按钮：根据游戏给技能打的标签生成一份初始规则，玩家可以再改
    internal static class Recommend
    {
        public static SkillRule For(SkillInfo skill)
        {
            var rule = new SkillRule { Mode = RuleMode.Custom };

            if (skill.HasKillReward)
            {
                // 处决、撕咬：打死才有成长，Boss/精英给得最多；点金术：范围伤害，每杀一个给金币
                rule.UseKillable = true;
                rule.KillMarginPercent = 90f;
                if (skill.Key.EndsWith("_Alchemy"))
                {
                    rule.Combine = Combine.Any;
                    rule.MinKillable = 2;
                    rule.UseBossOrElite = true;
                }
                else
                {
                    rule.Target = TargetMode.BossFirst;
                }
            }
            else if (skill.IsToggle)
            {
                // 献祭：开着时每 0.5 秒扣 5% 最大血量，附近有敌人且血量充足才开，否则自动关
                rule.UseHpAbove = true;
                rule.HpAbovePercent = 50f;
                rule.UseEnemyCount = true;
                rule.MinEnemies = 1;
                rule.CastWithoutEnemy = true;
            }
            else if (skill.Key == "PayToWin")
            {
                // 生效期间对非 Boss 敌人一击必杀，花的是金币：留给大群小怪
                rule.UseEnemyCount = true;
                rule.MinEnemies = 6;
                rule.Radius = 8f;
                rule.UseNoBoss = true;
                rule.UseEffectEnded = true;
                rule.CastWithoutEnemy = true;
            }
            else if (skill.HasTag(SkillTags.Heal) || skill.HasTag(SkillTags.Revive))
            {
                rule.UseHpBelow = true;
                rule.HpBelowPercent = 50f;
                rule.CastWithoutEnemy = true;
            }
            else if (skill.HasTag(SkillTags.Shield) || skill.HasTag(SkillTags.Defense))
            {
                rule.Combine = Combine.Any;
                rule.UseHpBelow = true;
                rule.HpBelowPercent = 70f;
                rule.UseEnemyCount = true;
                rule.MinEnemies = 4;
                rule.Radius = 6f;
                rule.CastWithoutEnemy = true;
            }
            else if (skill.HasTag(SkillTags.Buff) || skill.HasTag(SkillTags.Henshin) || skill.HasTag(SkillTags.Duration))
            {
                // 增益、变身、持续类：上一次的效果结束了、附近有敌人再放，避免重复放只刷新时间
                rule.UseEffectEnded = true;
                rule.UseEnemyCount = true;
                rule.MinEnemies = 1;
                rule.Radius = 8f;
                rule.CastWithoutEnemy = true;
            }
            else if (skill.HasTag(SkillTags.Area))
            {
                rule.Combine = Combine.Any;
                rule.UseEnemyCount = true;
                rule.MinEnemies = 2;
                rule.UseBossOrElite = true;
            }
            else
            {
                rule.Target = TargetMode.BossFirst;
            }
            return rule;
        }
    }
}
