using System;
using System.Linq;

namespace SmartSkill
{
    // "按标签推荐"按钮：根据游戏给技能打的标签生成一份初始规则，玩家可以再改
    internal static class Recommend
    {
        public static SkillRule For(ActiveSkillEnum skill)
        {
            var data = GameApi.SkillData(skill);
            var tags = data.tags ?? Array.Empty<SkillTag>();
            var rule = new SkillRule { Mode = RuleMode.Custom };

            if (tags.Contains(SkillTag.Heal) || tags.Contains(SkillTag.Revive))
            {
                rule.UseHpBelow = true;
                rule.HpBelowPercent = 50f;
                rule.CastWithoutEnemy = true;
            }
            else if (tags.Contains(SkillTag.Shield) || tags.Contains(SkillTag.Defense))
            {
                rule.Combine = Combine.Any;
                rule.UseHpBelow = true;
                rule.HpBelowPercent = 70f;
                rule.UseEnemyCount = true;
                rule.MinEnemies = 4;
                rule.Radius = 6f;
                rule.CastWithoutEnemy = true;
            }
            else if (tags.Contains(SkillTag.Area))
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
