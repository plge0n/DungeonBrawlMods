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

            // 开关技能开着时被询问的是"要不要关掉"：条件仍满足就保持开着
            bool toggledOn = info.IsToggle && slot.IsToggledOn;

            // 硬性限制，不参与"全部/任一"组合
            if (!toggledOn && slot.MaxCharges > 0 && rule.KeepCharges > 0 && slot.Charges <= rule.KeepCharges)
                return AutoCastVerdict.Skip($"保留{rule.KeepCharges}层充能（剩{slot.Charges}）");
            if (!toggledOn && rule.UseEffectEnded)
            {
                float left = player.SkillEffectRemaining(info.Id);
                if (left > EffectGrace) return AutoCastVerdict.Skip($"效果还剩{left:F1}秒");
            }

            float radius = rule.Radius > 0f ? rule.Radius : info.CastRange;
            var results = new List<(bool ok, string text)>();

            if (rule.UseHpBelow)
            {
                float hp = player.HpPercent * 100f;
                results.Add((hp <= rule.HpBelowPercent, $"血{hp:F0}%≤{rule.HpBelowPercent:F0}%"));
            }
            if (rule.UseHpAbove)
            {
                float hp = player.HpPercent * 100f;
                results.Add((hp >= rule.HpAbovePercent, $"血{hp:F0}%≥{rule.HpAbovePercent:F0}%"));
            }
            if (rule.UseManaAfter)
            {
                int cost = slot.CostsGold || toggledOn ? 0 : slot.CostFor(player);
                float after = (player.Mana - cost) * 100f / Math.Max(1, player.MaxMana);
                results.Add((after >= rule.ManaAfterPercent, $"剩蓝{after:F0}%≥{rule.ManaAfterPercent:F0}%"));
            }

            bool needEnemies = rule.UseEnemyCount || rule.UseBossOrElite || rule.UseNoBoss || rule.UseTargetHpBelow || rule.UseKillable;
            var enemies = needEnemies ? player.EnemiesWithin(radius) : null;
            if (rule.UseEnemyCount)
                results.Add((enemies.Count >= rule.MinEnemies, $"敌人{enemies.Count}≥{rule.MinEnemies}"));
            if (rule.UseBossOrElite)
            {
                bool any = enemies.Any(e => e.IsBoss || e.IsElite);
                results.Add((any, any ? "有Boss/精英" : "无Boss/精英"));
            }
            if (rule.UseNoBoss)
            {
                bool boss = enemies.Any(e => e.IsBoss);
                results.Add((!boss, boss ? "有Boss" : "无Boss"));
            }
            if (rule.UseTargetHpBelow)
            {
                int low = enemies.Count(e => IsLowHp(rule, e));
                results.Add((low > 0, $"血≤{rule.TargetHpBelowPercent:F0}%的敌人{low}个"));
            }
            Func<Role, bool> killable = null;
            if (rule.UseKillable && info.HasKillReward)
            {
                killable = e => KillPredictor.CanKill(slot, player, e, rule.KillMarginPercent);
                int n = enemies.Count(killable);
                results.Add((n >= rule.MinKillable, $"可斩杀{n}≥{rule.MinKillable}"));
            }

            bool pass = results.Count == 0
                || (rule.Combine == Combine.All ? results.All(r => r.ok) : results.Any(r => r.ok));
            string reason = results.Count == 0 ? "无条件"
                : string.Join(rule.Combine == Combine.All ? " 且 " : " 或 ",
                    results.Select(r => (r.ok ? "✓" : "✗") + r.text));

            if (toggledOn)
                return pass ? AutoCastVerdict.Skip("保持开启：" + reason) : AutoCastVerdict.Cast("关闭：" + reason);
            if (!pass) return AutoCastVerdict.Skip(reason);
            if (rule.CastWithoutEnemy && info.IsSelfCastable) return AutoCastVerdict.CastOnSelf(reason);
            return AutoCastVerdict.Cast(reason, TargetScore(rule, player, killable));
        }

        // 瞬发技能的实例也会短暂存在，留一点余量
        private const float EffectGrace = 0.3f;

        private static bool IsLowHp(SkillRule rule, Role e) => e.HpPercent * 100f <= rule.TargetHpBelowPercent;

        // 分数越小越优先：先按条件分档（无敌的放最后，可斩杀、低血量的优先），档内按目标模式，距离作为最后的排序
        private static Func<Role, float> TargetScore(SkillRule rule, Player player, Func<Role, bool> killable)
        {
            var baseScore = BaseScore(rule.Target, player);
            return e =>
            {
                float tier = 0f;
                if (e.IsInvulnerable) tier += 4f;
                if (killable != null && !killable(e)) tier += 2f;
                if (rule.UseTargetHpBelow && !IsLowHp(rule, e)) tier += 1f;
                return tier * 100000f + baseScore(e);
            };
        }

        private static Func<Role, float> BaseScore(TargetMode mode, Player player) => mode switch
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
