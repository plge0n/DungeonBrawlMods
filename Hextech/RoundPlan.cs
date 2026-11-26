using System;
using System.Collections.Generic;
using System.Linq;

namespace Hextech
{
    internal enum Tier { Silver, Gold, Prism }

    internal static class RoundPlan
    {
        public const int MaxRounds = 8;

        public static int RoundCount(int configured, int totalWaves)
            => configured == 0
                ? Math.Min(7, 4 + Math.Max(0, (totalWaves - 20) / 10))
                : Math.Clamp(configured, 4, MaxRounds);

        public static int[] TargetWaves(int rounds, int totalWaves, IEnumerable<int> relicWaves)
        {
            if (totalWaves <= 1) return Array.Empty<int>();
            var pool = relicWaves.Where(w => w > 1 && w <= totalWaves).Distinct().ToList();
            if (pool.Count < rounds - 1) pool = Enumerable.Range(2, totalWaves - 1).ToList();

            var targets = new List<int>();
            for (int i = 1; i < rounds && pool.Count > 0; i++)
            {
                double target = (double)totalWaves * i / rounds;
                int wave = pool.OrderBy(w => Math.Abs(w - target)).ThenByDescending(w => w).First();
                targets.Add(wave);
                pool.Remove(wave);
            }
            targets.Sort();
            return targets.ToArray();
        }

        public static bool HasDueOffer(IEnumerable<int> targets, int wave, int completedNormalRounds, int lastOfferRest)
            => lastOfferRest != wave && targets.Count(target => target <= wave) > completedNormalRounds;

        public static Tier TierFor(int round, int rounds, Random rng, int prisms)
        {
            double roll = rng.NextDouble();
            double progress = (double)round / (rounds - 1);
            Tier tier;
            if (round == 0) tier = roll < 0.6 ? Tier.Silver : Tier.Gold;
            else if (progress <= 1.0 / 3) tier = roll < 0.2 ? Tier.Silver : roll < 0.9 ? Tier.Gold : Tier.Prism;
            else if (progress <= 2.0 / 3) tier = roll < 0.5 ? Tier.Gold : Tier.Prism;
            else tier = roll < 0.25 ? Tier.Gold : Tier.Prism;
            return tier == Tier.Prism && prisms >= Math.Max(2, rounds / 2) ? Tier.Gold : tier;
        }
    }
}
