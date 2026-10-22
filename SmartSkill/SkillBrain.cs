using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;

namespace SmartSkill
{
    // 按玩家给每个技能设的规则决定放不放、打谁
    internal static class SkillBrain
    {
        public static AutoCastVerdict Decide(SkillSlot slot, Player player)
        {
            if (!Plugin.Enabled.Value) return AutoCastVerdict.Vanilla("Mod 已关闭");

            var info = slot.Info;
            var rule = RuleStore.Get(info);
            switch (rule.Mode)
            {
                case RuleMode.Vanilla:
                    return AutoCastVerdict.Vanilla("原版");
                case RuleMode.Never:
                    return AutoCastVerdict.Skip("设为永不自动");
            }

            float radius = rule.Radius > 0f ? rule.Radius : info.CastRange;
            var results = new List<(bool ok, string text)>();

            if (rule.UseHpBelow)
            {
                float hp = player.HpPercent * 100f;
                results.Add((hp <= rule.HpBelowPercent, $"血{hp:F0}%≤{rule.HpBelowPercent:F0}%"));
            }
            if (rule.UseManaAfter)
            {
                int cost = slot.CostsGold ? 0 : slot.CostFor(player);
                float after = (player.Mana - cost) * 100f / Math.Max(1, player.MaxMana);
                results.Add((after >= rule.ManaAfterPercent, $"剩蓝{after:F0}%≥{rule.ManaAfterPercent:F0}%"));
            }
            if (rule.UseEnemyCount || rule.UseBossOrElite)
            {
                var enemies = player.EnemiesWithin(radius);
                if (rule.UseEnemyCount)
                    results.Add((enemies.Count >= rule.MinEnemies, $"敌人{enemies.Count}≥{rule.MinEnemies}"));
                if (rule.UseBossOrElite)
                {
                    bool any = enemies.Any(e => e.IsBoss || e.IsElite);
                    results.Add((any, any ? "有Boss/精英" : "无Boss/精英"));
                }
            }

            bool pass = results.Count == 0
                || (rule.Combine == Combine.All ? results.All(r => r.ok) : results.Any(r => r.ok));
            string reason = results.Count == 0 ? "无条件"
                : string.Join(rule.Combine == Combine.All ? " 且 " : " 或 ",
                    results.Select(r => (r.ok ? "✓" : "✗") + r.text));

            if (!pass) return AutoCastVerdict.Skip(reason);
            if (rule.CastWithoutEnemy && info.IsSelfCastable) return AutoCastVerdict.CastOnSelf(reason);
            return AutoCastVerdict.Cast(reason, TargetScore(rule.Target, player));
        }

        // 分数越小越优先；距离作为同档次的次要排序
        private static Func<Role, float> TargetScore(TargetMode mode, Player player) => mode switch
        {
            TargetMode.BossFirst => e => (e.IsBoss ? 0f : e.IsElite ? 1000f : 2000f) + player.DistanceTo(e),
            TargetMode.LowestHp => e => e.HpPercent * 1000f + player.DistanceTo(e),
            TargetMode.HighestHp => e => (1f - e.HpPercent) * 1000f + player.DistanceTo(e),
            _ => e => player.DistanceTo(e),
        };
    }

    // 记录每个技能最近一次的结果，供弹窗显示；VerboseLog 打开时写日志
    internal static class StatusLog
    {
        private static readonly Dictionary<string, string> last = new Dictionary<string, string>();
        // 没放出来的技能每帧都会触发，日志只在结果变化时写；真正放出来的每次都写
        private static readonly Dictionary<string, string> lastLogged = new Dictionary<string, string>();

        public static string Last(SkillInfo skill) => last.TryGetValue(skill.Key, out var s) ? s : null;

        public static void OnResolved(AutoCastResult r)
        {
            var key = r.Slot.Info.Key;
            string outcome = r.Casted ? "已释放"
                : r.Verdict.Action == AutoCastAction.Skip ? "跳过"
                : "原版未放(范围内无目标或状态不对)";
            string status = $"{outcome}：{r.Verdict.Reason}";
            last[key] = status;

            if (!Plugin.VerboseLog.Value) return;
            if (!r.Casted)
            {
                if (lastLogged.TryGetValue(key, out var prev) && prev == outcome) return;
                lastLogged[key] = outcome;
            }
            else
            {
                lastLogged.Remove(key);
            }
            Plugin.Log.LogInfo($"{key}: {status}");
        }
    }
}
