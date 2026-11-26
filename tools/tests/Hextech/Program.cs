using System;
using System.Linq;
using Hextech;

CheckPlan(20, 4, new[] { 1, 5, 10, 15, 20 }, new[] { 5, 10, 15 });
CheckPlan(30, 5, new[] { 5, 10, 15, 20, 25, 30 }, new[] { 5, 10, 20, 25 });
CheckPlan(40, 6, new[] { 5, 10, 15, 20, 25, 30, 35, 40 }, new[] { 5, 15, 20, 25, 35 });
CheckPlan(50, 7, new[] { 5, 10, 15, 20, 25, 30, 35, 40, 46, 50 }, new[] { 5, 15, 20, 30, 35, 40 });
Check(RoundPlan.TargetWaves(8, 20, new[] { 6, 19 }).SequenceEqual(new[] { 3, 5, 8, 10, 13, 15, 18 }), "遗物不足时按比例取波次");
Check(RoundPlan.TargetWaves(4, 20, Array.Empty<int>()).SequenceEqual(new[] { 5, 10, 15 }), "没有遗物时按比例取波次");
Check(RoundPlan.TargetWaves(8, 3, new[] { 1, 2, 2, 4 }).SequenceEqual(new[] { 2, 3 }), "短局只选有效且不重复的波次");
Check(RoundPlan.TargetWaves(4, 1, new[] { 1 }).Length == 0, "只有一波时保留开局");
Check(RoundPlan.TargetWaves(8, 12, Array.Empty<int>()).SequenceEqual(new[] { 2, 3, 5, 6, 8, 9, 11 }), "等距时取较晚波次");
Console.WriteLine("通过：20/30/40/50 波安排（含 Crazy1/2/3）、比例回退、短局与等距取舍");

foreach (var (configured, expected) in new[] { (0, 6), (3, 4), (4, 4), (8, 8), (9, 8), (-1, 4) })
    Check(RoundPlan.RoundCount(configured, 40) == expected, $"配置 {configured} → {expected}");
Check(RoundPlan.RoundCount(0, 10) == 4 && RoundPlan.RoundCount(0, 100) == 7, "自动次数上下限");
Console.WriteLine("通过：自动次数上下限与手动配置夹取");

for (int seed = 1; seed <= 1000; seed++)
for (int round = 0; round < 4; round++)
for (int prisms = 0; prisms <= 3; prisms++)
{
    int rollSeed = seed ^ (round * 7919);
    var oldRng = new Random(rollSeed);
    var newRng = new Random(rollSeed);
    Check(LegacyTier(round, oldRng, prisms) == RoundPlan.TierFor(round, 4, newRng, prisms),
        $"旧品质对照：种子 {seed}，回合 {round}，已选棱彩 {prisms}");
    Check(oldRng.Next() == newRng.Next(), "随机数消费一致");
}
for (int rounds = 4; rounds <= 8; rounds++)
{
    int cap = Math.Max(2, rounds / 2);
    Check(RoundPlan.TierFor(rounds - 1, rounds, new Random(0), cap - 1) == Tier.Prism, "达到上限前可抽棱彩");
    Check(RoundPlan.TierFor(rounds - 1, rounds, new Random(0), cap) == Tier.Gold, "达到上限后棱彩降为金色");
}
Console.WriteLine("通过：1000 个种子的四回合品质与旧实现一致，4–8 回合棱彩上限");

var targets = new[] { 5, 10, 15 };
var run = (Completed: 0, LastOfferRest: 0);
Check(!CanOffer(4), "第一个目标之前不弹");
// 跳过第 5 和第 10 波之前的休息事件。
Check(CanOffer(11), "漏掉目标后在第 11 波补弹");
run.LastOfferRest = 11;
var checkpoint = run; // 快照在安排之后、选卡之前。
run.Completed++;
Check(!CanOffer(11), "同一次休息最多安排一次");
run = checkpoint;
Check(!CanOffer(11) && run.Completed == 0, "重试还原已安排但未选的状态");
run.Completed++; // 重试后完成快照中待弹的那次。
Check(CanOffer(12), "下一次休息补第二次");
run = (2, 12);
Check(!CanOffer(13) && CanOffer(15), "补齐后等下一个目标");
Console.WriteLine("通过：漏回合补弹、每次休息只补一次、还原安排与完成计数");

bool CanOffer(int wave) => RoundPlan.HasDueOffer(targets, wave, run.Completed, run.LastOfferRest);

static void CheckPlan(int total, int rounds, int[] relics, int[] targets)
{
    Check(RoundPlan.RoundCount(0, total) == rounds, $"{total} 波自动次数");
    Check(RoundPlan.TargetWaves(rounds, total, relics).SequenceEqual(targets), $"{total} 波目标");
}

static Tier LegacyTier(int round, Random rng, int prisms)
{
    double roll = rng.NextDouble();
    Tier tier;
    switch (round)
    {
        case 0: tier = roll < 0.6 ? Tier.Silver : Tier.Gold; break;
        case 1: tier = roll < 0.2 ? Tier.Silver : roll < 0.9 ? Tier.Gold : Tier.Prism; break;
        case 2: tier = roll < 0.5 ? Tier.Gold : Tier.Prism; break;
        default: tier = roll < 0.25 ? Tier.Gold : Tier.Prism; break;
    }
    return tier == Tier.Prism && prisms >= 2 ? Tier.Gold : tier;
}

static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
}
