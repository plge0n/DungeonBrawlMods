using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartSkill
{
    internal enum Decision
    {
        Vanilla,    // 交给原版逻辑
        Cast,       // 条件满足，按规则选目标
        CastOnSelf, // 条件满足，不需要敌人在范围内
        Skip,       // 这一帧不放
    }

    internal static class SkillBrain
    {
        // 当前 dxx 调用使用的规则；dxy 据此选目标
        [ThreadStatic] internal static SkillRule ActiveRule;
        [ThreadStatic] internal static bool CastOnSelf;

        public static Decision Decide(ActiveSkillEnum skill, bbe player, out string reason)
        {
            var rule = RuleStore.Get(skill);
            switch (rule.Mode)
            {
                case RuleMode.Vanilla:
                    reason = "原版";
                    return Decision.Vanilla;
                case RuleMode.Never:
                    reason = "设为永不自动";
                    return Decision.Skip;
            }

            var data = GameApi.SkillData(skill);
            float radius = rule.Radius > 0f ? rule.Radius : GameApi.CastRange(data);
            var results = new List<(bool ok, string text)>();

            if (rule.UseHpBelow)
            {
                float hp = GameApi.HpPercent(player) * 100f;
                results.Add((hp <= rule.HpBelowPercent, $"血{hp:F0}%≤{rule.HpBelowPercent:F0}%"));
            }
            if (rule.UseManaAfter)
            {
                var slot = GameApi.SkillSlots().FirstOrDefault(s => GameApi.SkillOf(s) == skill);
                int cost = slot != null && !GameApi.CostsGold(slot) ? GameApi.Cost(slot, player) : 0;
                float after = (GameApi.Mana(player) - cost) * 100f / Math.Max(1, GameApi.MaxMana(player));
                results.Add((after >= rule.ManaAfterPercent, $"剩蓝{after:F0}%≥{rule.ManaAfterPercent:F0}%"));
            }
            if (rule.UseEnemyCount)
            {
                int count = CountEnemies(player, radius);
                results.Add((count >= rule.MinEnemies, $"敌人{count}≥{rule.MinEnemies}"));
            }
            if (rule.UseBossOrElite)
            {
                bool any = AnyBossOrEliteWithin(player, radius);
                results.Add((any, any ? "有Boss/精英" : "无Boss/精英"));
            }

            bool pass = results.Count == 0
                || (rule.Combine == Combine.All ? results.All(r => r.ok) : results.Any(r => r.ok));
            reason = results.Count == 0 ? "无条件"
                : string.Join(rule.Combine == Combine.All ? " 且 " : " 或 ",
                    results.Select(r => (r.ok ? "✓" : "✗") + r.text));

            if (!pass) return Decision.Skip;
            return rule.CastWithoutEnemy && GameApi.IsSelfCastable(data) ? Decision.CastOnSelf : Decision.Cast;
        }

        public static yb PickTarget(bbe player, float castingRange, TargetMode mode)
        {
            yb best = null;
            float bestScore = float.MaxValue;
            foreach (var enemy in GameApi.Enemies(player))
            {
                if (!InRange(player, enemy, castingRange)) continue;
                float dist = GameApi.Distance(player, enemy);
                // 分数越小越优先；距离作为同档次的次要排序
                float score = mode switch
                {
                    TargetMode.BossFirst => (GameApi.IsBoss(enemy) ? 0f : GameApi.IsElite(enemy) ? 1000f : 2000f) + dist,
                    TargetMode.LowestHp => GameApi.HpPercent(enemy) * 1000f + dist,
                    TargetMode.HighestHp => (1f - GameApi.HpPercent(enemy)) * 1000f + dist,
                    _ => dist,
                };
                if (score < bestScore)
                {
                    best = enemy;
                    bestScore = score;
                }
            }
            return best;
        }

        private static int CountEnemies(bbe player, float range) =>
            GameApi.Enemies(player).Count(e => InRange(player, e, range));

        private static bool AnyBossOrEliteWithin(bbe player, float range) =>
            GameApi.Enemies(player).Any(e => InRange(player, e, range) && (GameApi.IsBoss(e) || GameApi.IsElite(e)));

        // 与原版 ez.dyb 一致：range <= 0 表示不限距离
        private static bool InRange(bbe player, yb enemy, float range)
        {
            if (!GameApi.IsAlive(enemy)) return false;
            return range <= 0f || GameApi.Distance(player, enemy) <= range + GameApi.AddRange(enemy);
        }
    }
}
