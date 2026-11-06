using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;
namespace AutoForge
{
    public enum SelectionMode { Weighted, Balanced }
    public static class Selection
    {
        // 7.13 forging.xlsx：缺 D 的属性取最低实际档（B），详见 README。
        public static readonly IReadOnlyDictionary<ForgeAttribute, float> Baselines = new Dictionary<ForgeAttribute, float> {
            [ForgeAttribute.AddDamage] = 4f,
            [ForgeAttribute.Agi] = 4f,
            [ForgeAttribute.AllBreakShield] = 4f,
            [ForgeAttribute.ArmedAdd] = 8f,
            [ForgeAttribute.Armor] = 4f,
            [ForgeAttribute.Attack] = 12f,
            [ForgeAttribute.AttackSpeed] = 6f,
            [ForgeAttribute.CoolDown] = 5f,
            [ForgeAttribute.Critical] = 5f,
            [ForgeAttribute.CriticalDamage] = 12f,
            [ForgeAttribute.Doge] = 5f,
            [ForgeAttribute.ExpAdd] = 4f,
            [ForgeAttribute.ExtraDamage] = 50f,
            [ForgeAttribute.HP] = 120f,
            [ForgeAttribute.HPSec] = 20f,
            [ForgeAttribute.HaloAdd] = 8f,
            [ForgeAttribute.HenshinAdd] = 8f,
            [ForgeAttribute.HpPercent] = 5f,
            [ForgeAttribute.HpSecRate] = 1f,
            [ForgeAttribute.Luck] = 15f,
            [ForgeAttribute.MP] = 20f,
            [ForgeAttribute.MPSec] = 2f,
            [ForgeAttribute.MoveSpeed] = 0.5f,
            [ForgeAttribute.NormalAdd] = 6f,
            [ForgeAttribute.NormalBreak] = 6f,
            [ForgeAttribute.ReduceInjury] = 18f,
            [ForgeAttribute.SkillAdd] = 6f,
            [ForgeAttribute.SkillBreak] = 6f,
            [ForgeAttribute.SkillHit] = 5f,
            [ForgeAttribute.Sta] = 4f,
            [ForgeAttribute.Str] = 4f,
            [ForgeAttribute.SummonAdd] = 8f,
            [ForgeAttribute.Three] = 3f,
            [ForgeAttribute.XiXue] = 15f,
            [ForgeAttribute.XiXueRate] = 1.5f,
        };
        public static Dictionary<ForgeAttribute, int> Defaults() => new Dictionary<ForgeAttribute, int> {
            [ForgeAttribute.AddDamage] = 5,
            [ForgeAttribute.Agi] = 5,
            [ForgeAttribute.AllBreakShield] = 2,
            [ForgeAttribute.ArmedAdd] = 5,
            [ForgeAttribute.Armor] = 3,
            [ForgeAttribute.Attack] = 5,
            [ForgeAttribute.AttackSpeed] = 5,
            [ForgeAttribute.CoolDown] = 5,
            [ForgeAttribute.Critical] = 5,
            [ForgeAttribute.CriticalDamage] = 5,
            [ForgeAttribute.Doge] = 3,
            [ForgeAttribute.ExpAdd] = 1,
            [ForgeAttribute.ExtraDamage] = 5,
            [ForgeAttribute.HP] = 3,
            [ForgeAttribute.HPSec] = 3,
            [ForgeAttribute.HaloAdd] = 5,
            [ForgeAttribute.HenshinAdd] = 5,
            [ForgeAttribute.HpPercent] = 3,
            [ForgeAttribute.HpSecRate] = 3,
            [ForgeAttribute.Luck] = 1,
            [ForgeAttribute.MP] = 1,
            [ForgeAttribute.MPSec] = 1,
            [ForgeAttribute.MoveSpeed] = 2,
            [ForgeAttribute.NormalAdd] = 5,
            [ForgeAttribute.NormalBreak] = 2,
            [ForgeAttribute.ReduceInjury] = 3,
            [ForgeAttribute.SkillAdd] = 5,
            [ForgeAttribute.SkillBreak] = 2,
            [ForgeAttribute.SkillHit] = 3,
            [ForgeAttribute.Sta] = 5,
            [ForgeAttribute.Str] = 5,
            [ForgeAttribute.SummonAdd] = 5,
            [ForgeAttribute.Three] = 5,
            [ForgeAttribute.XiXue] = 3,
            [ForgeAttribute.XiXueRate] = 3,
        };
        public static int Weight(IReadOnlyDictionary<ForgeAttribute, int> weights, ForgeAttribute type)
            => weights.TryGetValue(type, out int w) ? Math.Max(0, Math.Min(10, w)) : 0;
        public static float Score(ForgeCandidate candidate, IReadOnlyDictionary<ForgeAttribute, int> weights)
            => candidate.Stats.Sum(s => Weight(weights, s.Type) * s.Value / Baselines[s.Type]);
        public static int? Choose(ForgeOffer offer, SelectionMode mode, IReadOnlyDictionary<ForgeAttribute, int> weights,
            IReadOnlyDictionary<ForgeAttribute, float> totals)
        {
            int? best = null; float bestScore = 0, bestNeed = float.PositiveInfinity;
            for (int i = 0; i < offer.Candidates.Count; i++) {
                var c = offer.Candidates[i]; float score = Score(c, weights);
                if (!(score > 0)) continue;
                float need = c.Stats.Where(s => Weight(weights, s.Type) > 0).Min(s =>
                    (totals.TryGetValue(s.Type, out float total) ? total : 0) / Weight(weights, s.Type));
                if (!best.HasValue || (mode == SelectionMode.Balanced && need < bestNeed)
                    || ((mode == SelectionMode.Weighted || need == bestNeed) && score > bestScore)) {
                    best = i; bestScore = score; bestNeed = need;
                }
            }
            return best;
        }
        public static void Accumulate(Dictionary<ForgeAttribute, float> totals, ForgeCandidate chosen)
        {
            foreach (var s in chosen.Stats) totals[s.Type] = (totals.TryGetValue(s.Type, out float old) ? old : 0) + s.Value / Baselines[s.Type];
        }
    }
}
