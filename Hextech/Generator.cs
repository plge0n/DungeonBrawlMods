using System;
using System.Collections.Generic;
using System.Linq;

namespace Hextech
{
    internal enum Tier { Silver, Gold, Prism }

    internal enum BlockKind { Param, Trigger, Cost }

    // 积木：一张卡由几块积木拼成。数值都是"一份"的量，卡上的积木带倍数 Mult
    internal sealed class Block
    {
        public string Id;
        public string Word;
        public BlockKind Kind;
        public float Points;
        // 触发类积木可以改成"每第 3 次释放"，效果 ×EveryThirdFactor
        public bool CanEveryThird;
        // 只允许整数倍（充能）或固定 1 倍（回响）
        public bool WholeOnly, SingleOnly;
        public Tier MinTier = Tier.Silver;
        public Func<SkillProfile, bool> Applies = _ => true;
        // 被动参数积木对应的参数
        public PassiveParam Param;
    }

    internal sealed class CardPart
    {
        public Block Block;
        public float Mult = 1f;
        public bool EveryThird;
        public float Points => Block.Points * Mult;
    }

    internal sealed class Card
    {
        public Tier Tier;
        public List<CardPart> Parts = new List<CardPart>();
        public string Name = "";
        public List<string> Lines = new List<string>();
        // 手写的卡（通用、招牌、敌人），这时 Parts 为空
        public Augment Augment;

        public float Points => Parts.Sum(p => p.Points);
        public string Signature => Augment != null ? "a:" + Augment.Id
            : string.Join(",", Parts.Select(p => $"{p.Block.Id}x{p.Mult}{(p.EveryThird ? "/3" : "")}").OrderBy(s => s));
        // 积木组合（不管倍数）：刷新时按它排除，否则换来的只是同一张卡数值稍有不同
        public string Shape => Augment != null ? "a:" + Augment.Id : string.Join(",", Parts.Select(p => p.Block.Id).OrderBy(s => s));
    }

    internal static class Blocks
    {
        // ---- 主动技能 ----
        public static readonly Block Power = new Block { Id = "power", Word = "威能", Kind = BlockKind.Param, Points = 2f, Applies = p => p.Damage };
        public static readonly Block Haste = new Block { Id = "haste", Word = "急速", Kind = BlockKind.Param, Points = 2f, Applies = p => p.Cooldown >= 2f };
        public static readonly Block Thrift = new Block { Id = "thrift", Word = "节能", Kind = BlockKind.Param, Points = 1f, Applies = p => p.Cost > 0f };
        public static readonly Block Extend = new Block { Id = "extend", Word = "延时", Kind = BlockKind.Param, Points = 2f, Applies = p => p.DurationGood };
        public static readonly Block Charge = new Block
        {
            Id = "charge", Word = "充能", Kind = BlockKind.Param, Points = 4f, WholeOnly = true, MinTier = Tier.Gold, Applies = p => p.Charges > 0,
        };

        public static readonly Block Fury = new Block { Id = "fury", Word = "战意", Kind = BlockKind.Trigger, Points = 2f, CanEveryThird = true };
        public static readonly Block Bulwark = new Block { Id = "bulwark", Word = "铁壁", Kind = BlockKind.Trigger, Points = 2f, CanEveryThird = true };
        public static readonly Block Invulnerable = new Block
        {
            Id = "invuln", Word = "无敌", Kind = BlockKind.Trigger, Points = 3f, CanEveryThird = true, Applies = p => !p.HasInvulnerable,
        };
        public static readonly Block Reflux = new Block { Id = "reflux", Word = "回流", Kind = BlockKind.Trigger, Points = 1f, CanEveryThird = true, Applies = p => p.Cost > 0f };
        public static readonly Block Drain = new Block { Id = "drain", Word = "汲取", Kind = BlockKind.Trigger, Points = 2f, CanEveryThird = true };
        public static readonly Block Harvest = new Block
        {
            Id = "harvest", Word = "收割", Kind = BlockKind.Trigger, Points = 3f, Applies = p => p.Damage && p.Cooldown >= 3f,
        };
        public static readonly Block Echo = new Block
        {
            Id = "echo", Word = "回响", Kind = BlockKind.Trigger, Points = 5f, SingleOnly = true, MinTier = Tier.Gold,
            Applies = p => p.Cooldown >= 3f && !p.NoEcho,
        };

        public static readonly Block Heavy = new Block { Id = "heavy", Word = "沉重", Kind = BlockKind.Cost, Points = -2f, MinTier = Tier.Prism };
        public static readonly Block Greedy = new Block { Id = "greedy", Word = "贪婪", Kind = BlockKind.Cost, Points = -1f, MinTier = Tier.Prism, Applies = p => p.Cost > 0f };

        public static readonly Block[] ActiveBlocks = { Power, Haste, Thrift, Extend, Charge, Fury, Bulwark, Invulnerable, Reflux, Drain, Harvest, Echo, Heavy, Greedy };

        // 同一张卡里互相抵消（或者白给）的积木
        public static bool Conflicts(Block a, Block b) => Pair(a, b) || Pair(b, a);

        private static bool Pair(Block a, Block b) => (a == Haste && b == Heavy) || (a == Thrift && b == Greedy) || (a == Thrift && b == Reflux);

        // 被动：每个参数一块积木
        public static Block ForParam(PassiveParam p) => new Block
        {
            Id = "p" + p.Index, Word = WordFor(p), Kind = BlockKind.Param, Points = p.Points, Param = p,
        };

        private static string WordFor(PassiveParam p)
        {
            if (p.Better < 0) return p.Label.Contains("扣自身") ? "坚韧" : p.Label.Contains("承受") ? "橡皮" : "精打细算";
            if (p.Label.Contains("概率")) return "幸运";
            if (p.Label.Contains("三维")) return "成长";
            return "威能";
        }

        // ---- 数值（一份）----
        public const float PowerPerUnit = 0.15f, HastePerUnit = 0.12f, ThriftPerUnit = 0.35f, ExtendPerUnit = 0.25f;
        public const float HeavyPerUnit = 0.2f, GreedyPerUnit = 0.5f;
        public const float FuryAttackSpeed = 0.25f, BulwarkArmor = 40f, InvulnerableSeconds = 0.5f, RefluxRatio = 0.3f, DrainRatio = 0.03f, HarvestRatio = 0.2f;
        public const float EveryThirdFactor = 2f, EchoDelay = 0.5f;
        // 单次触发的上限：返还蓝耗不超过 100%，无敌不超过 2 秒
        public const float MaxReflux = 1f, MaxInvulnerable = 2f;
        // 冷却最多减到原来的 40%
        public const float MinCooldownFactor = 0.4f;

        // 触发效果至少隔这么久才能再触发一次
        public static float InternalCooldown(SkillProfile p) => Math.Max(p.Cooldown, 3f);

        // 释放后 buff 的持续时间：冷却 × 0.5，限制在 2–8 秒
        public static float BuffSeconds(SkillProfile p) => Math.Min(8f, Math.Max(2f, p.Cooldown * 0.5f));
    }

    internal static class Generator
    {
        private static float Budget(Tier t) => t == Tier.Silver ? 3f : t == Tier.Gold ? 6f : 10f;

        // 触发效果的倍数压低一些：本身就会反复触发
        private static float[] TriggerMults(Tier t) => t == Tier.Silver ? new[] { 1f } : t == Tier.Gold ? new[] { 1f, 1.5f } : new[] { 1f, 1.5f, 2f };

        private static float[] Mults(Tier t, bool passive)
        {
            // 被动只有参数积木，数值大小就是全部差别：按预算精确凑（见 Tolerance），只有一个参数的被动每档只会有一张卡
            if (passive) return new[] { 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 4f, 5f };
            return t == Tier.Silver ? new[] { 1f, 1.5f } : t == Tier.Gold ? new[] { 1f, 1.5f, 2f } : new[] { 1.5f, 2f, 3f };
        }

        // 主动技能允许点数上下差 1；被动必须正好（否则只是同一张卡大一点小一点，玩家只会挑大的）
        private static float Tolerance(SkillProfile p) => p.Active ? 1f : 0.25f;

        public static List<Block> Candidates(SkillProfile p)
            => p.Active ? Blocks.ActiveBlocks.Where(b => b.Applies(p)).ToList() : p.Params.Select(Blocks.ForParam).ToList();

        /// <summary>生成最多 count 张互不相同的卡。exclude 里的积木组合（这次刷新前出现过的，见 Card.Shape）尽量避开。</summary>
        public static List<Card> Offer(SkillProfile p, Tier tier, int count, Random rng, ICollection<string> exclude = null)
        {
            var cards = new List<Card>();
            var avoid = new HashSet<string>(exclude ?? new string[0]);
            for (int attempt = 0; attempt < 400 && cards.Count < count; attempt++)
            {
                var card = Make(p, tier, rng);
                if (card == null) continue;
                // 前一半尝试避开 exclude 和本次已有的组合，之后只要求三张互不相同
                if (attempt < 200 && (avoid.Contains(card.Shape) || cards.Any(c => c.Shape == card.Shape))) continue;
                if (cards.Any(c => c.Signature == card.Signature)) continue;
                cards.Add(card);
            }
            return cards;
        }

        public static Card Make(SkillProfile p, Tier tier, Random rng)
        {
            var all = Candidates(p).Where(b => b.MinTier <= tier).ToList();
            if (all.Count == 0) return null;
            // 参数积木少的技能（翻滚、形态切换、技能窃取……）多放一块触发效果，否则凑不够点数或三张不同的卡
            int paramKinds = all.Count(b => b.Kind == BlockKind.Param);
            int extra = paramKinds >= 2 ? 0 : 1;
            float budget = Budget(tier);
            int maxTriggers = (tier == Tier.Silver ? 0 : tier == Tier.Gold ? 1 : 2) + extra;
            int maxParts = tier == Tier.Silver ? 2 : tier == Tier.Gold ? 3 : 4;
            bool needTrigger = tier == Tier.Prism && all.Any(b => b.Kind == BlockKind.Trigger);
            var mults = Mults(tier, !p.Active);
            float tol = Tolerance(p);

            for (int attempt = 0; attempt < 60; attempt++)
            {
                var card = new Card { Tier = tier };
                float points = 0f;
                int triggers = 0, costs = 0;
                // 棱彩先放一块触发效果，保证"改打法"的那部分一定有
                var order = all.OrderBy(_ => rng.Next()).ToList();
                if (needTrigger)
                {
                    var first = order.First(b => b.Kind == BlockKind.Trigger);
                    order.Remove(first);
                    order.Insert(0, first);
                }
                foreach (var b in order)
                {
                    if (card.Parts.Count >= maxParts) break;
                    if (b.Kind == BlockKind.Trigger && triggers >= maxTriggers) continue;
                    if (b.Kind == BlockKind.Cost && (costs >= 1 || tier != Tier.Prism)) continue;
                    if (card.Parts.Any(x => Blocks.Conflicts(x.Block, b))) continue;
                    var part = new CardPart { Block = b, EveryThird = b.CanEveryThird && rng.NextDouble() < 0.3 };
                    // 代价积木只拿一份；其余在不超预算的倍数里随机挑
                    var options = b.SingleOnly || b.Kind == BlockKind.Cost ? new[] { 1f }
                        : b.WholeOnly ? mults.Where(m => m == Math.Floor(m)).DefaultIfEmpty(1f).ToArray()
                        : b.Kind == BlockKind.Trigger ? TriggerMults(tier)
                        : mults;
                    var fits = options.Where(m => points + b.Points * m <= budget + tol).ToList();
                    if (fits.Count == 0) continue;
                    part.Mult = fits[rng.Next(fits.Count)];
                    card.Parts.Add(part);
                    points += part.Points;
                    if (b.Kind == BlockKind.Trigger) triggers++;
                    if (b.Kind == BlockKind.Cost) costs++;
                    if (points >= budget - tol && points <= budget + tol && rng.NextDouble() < 0.6) break;
                }
                bool hasTrigger = card.Parts.Any(x => x.Block.Kind == BlockKind.Trigger);
                bool onlyCost = card.Parts.All(x => x.Block.Kind == BlockKind.Cost);
                if (points < budget - tol || points > budget + tol || onlyCost || (needTrigger && !hasTrigger)) continue;
                Describe(p, card);
                return card;
            }
            return null;
        }

        // ---- 名字和描述 ----
        private static void Describe(SkillProfile p, Card card)
        {
            var main = card.Parts.Where(x => x.Block.Kind != BlockKind.Cost).OrderByDescending(x => x.Points).ToList();
            var cost = card.Parts.FirstOrDefault(x => x.Block.Kind == BlockKind.Cost);
            string root = main[0].Block.Word;
            string prefix = cost != null ? cost.Block.Word : main.Count > 1 && main[1].Block.Word != root ? main[1].Block.Word : null;
            card.Name = prefix == null ? root : prefix + "·" + root;
            card.Lines = card.Parts
                .OrderBy(x => x.Block.Kind == BlockKind.Param ? 0 : x.Block.Kind == BlockKind.Trigger ? 1 : 2)
                .Select(x => Line(p, x)).ToList();
        }

        public static string Line(SkillProfile p, CardPart x)
        {
            float m = x.Mult;
            float f = x.EveryThird ? Blocks.EveryThirdFactor : 1f;
            string when = x.EveryThird ? "每第 3 次释放" : "释放时";
            string limit = Blocks.InternalCooldown(p) > p.Cooldown + 0.01f ? $"（最多每 {Blocks.InternalCooldown(p):0.#} 秒一次）" : "";
            float n = Blocks.BuffSeconds(p);
            switch (x.Block.Id)
            {
                case "power": return $"技能伤害 +{Pct(Blocks.PowerPerUnit * m)}";
                case "haste": return $"冷却 -{Pct(Blocks.HastePerUnit * m)}";
                case "thrift": return $"蓝耗 -{Pct(Math.Min(1f, Blocks.ThriftPerUnit * m))}";
                case "extend": return $"持续时间 +{Pct(Blocks.ExtendPerUnit * m)}";
                case "charge": return $"充能 +{m:0}";
                case "fury": return $"{when}：{n:0.#} 秒内攻速 +{Pct(Blocks.FuryAttackSpeed * m * f)}{limit}";
                case "bulwark": return $"{when}：{n:0.#} 秒内护甲 +{Blocks.BulwarkArmor * m * f:0}{limit}";
                case "invuln": return $"{when}：无敌 {Math.Min(Blocks.MaxInvulnerable, Blocks.InvulnerableSeconds * m * f):0.##} 秒{limit}";
                case "reflux": return $"{when}：返还 {Pct(Math.Min(Blocks.MaxReflux, Blocks.RefluxRatio * m * f))} 蓝耗{limit}";
                case "drain": return $"{when}：回复 {Pct(Blocks.DrainRatio * m * f)} 生命上限{limit}";
                case "harvest": return $"技能击杀敌人时：返还 {Pct(Blocks.HarvestRatio * m)} 冷却（每次释放最多一次）";
                case "echo": return $"释放后 {Blocks.EchoDelay:0.#} 秒免费再放一次{limit}";
                case "heavy": return $"冷却 +{Pct(Blocks.HeavyPerUnit * m)}";
                case "greedy": return $"蓝耗 +{Pct(Blocks.GreedyPerUnit * m)}";
            }
            var param = x.Block.Param;
            if (param == null) return x.Block.Word;
            string sign = param.Better > 0 ? "+" : "-";
            return param.Relative ? $"{param.Label} {sign}{Pct(param.Step * m)}" : $"{param.Label} {sign}{param.Step * m:0.##}{param.Unit}";
        }

        private static string Pct(float v) => $"{v * 100f:0.#}%";
    }

    // 已选强化合在一起的效果
    internal sealed class Totals
    {
        public float Damage = 1f, Cooldown = 1f, Cost = 1f, Duration = 1f;
        public int Charges;
        public float[] PassiveValues;

        public static Totals Of(SkillProfile p, IEnumerable<Card> picks)
        {
            var parts = picks.SelectMany(c => c.Parts).ToList();
            float Sum(Block b) => parts.Where(x => x.Block == b).Sum(x => x.Mult);
            var t = new Totals
            {
                Damage = 1f + Blocks.PowerPerUnit * Sum(Blocks.Power),
                Cooldown = Math.Max(Blocks.MinCooldownFactor, 1f - Blocks.HastePerUnit * Sum(Blocks.Haste) + Blocks.HeavyPerUnit * Sum(Blocks.Heavy)),
                Cost = Math.Max(0f, 1f - Blocks.ThriftPerUnit * Sum(Blocks.Thrift) + Blocks.GreedyPerUnit * Sum(Blocks.Greedy)),
                Duration = 1f + Blocks.ExtendPerUnit * Sum(Blocks.Extend),
                Charges = (int)Sum(Blocks.Charge),
            };
            if (!p.Active)
            {
                var values = (float[])p.BaseValues.Clone();
                foreach (var param in p.Params)
                {
                    float units = parts.Where(x => x.Block.Param != null && x.Block.Param.Index == param.Index).Sum(x => x.Mult);
                    if (units == 0f) continue;
                    float baseValue = p.BaseValues[param.Index];
                    float delta = param.Relative ? baseValue * param.Step * units : param.Step * units;
                    float v = baseValue + param.Better * delta;
                    values[param.Index] = Math.Min(param.Max, Math.Max(param.Min, v));
                }
                t.PassiveValues = values;
            }
            return t;
        }
    }
}
