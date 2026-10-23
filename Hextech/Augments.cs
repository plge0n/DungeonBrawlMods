using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;

namespace Hextech
{
    internal enum AugmentPool { General, Signature, Enemy }

    // 手写的强化：通用卡（所有英雄都能抽，参照 LoL 海克斯大乱斗）、招牌卡（只给某个专属技能）、敌人卡（"敌人海克斯"开着时敌人拿的）。
    // 一次性的效果（加属性、欠锻造）写在 OnPick，只在选中时执行一次；重试波次时游戏会把玩家属性连同快照一起还原，不用再执行。
    // 持续的状态（溅射、怪物倍率）由 Engine.Apply 按已选的卡重新设置，可以反复调用
    internal sealed class Augment
    {
        public string Id;
        public string Name;
        // 大乱斗里的原名，写在说明最后
        public string Origin;
        public AugmentPool Pool;
        public Tier Tier;
        public string[] Lines;
        public string IconPath;
        // 招牌卡：只在这个专属技能的英雄出现
        public int SkillId;
        // 某些英雄更容易抽到（英雄编号 → 权重倍数）
        public Dictionary<int, float> HeroWeights;
        public Action<Player> OnPick;
        // 撤销 OnPick 的效果（有人进房间、关掉总开关时）；一次性的锻造之类撤不了的留空
        public Action<Player> Revert;
        // 敌人卡：之后刷出的怪物倍率
        public float EnemyHp = 1f, EnemyAttack = 1f;

        public float WeightFor(int heroId) => HeroWeights != null && HeroWeights.TryGetValue(heroId, out var w) ? w : 1f;

        public Card ToCard() => new Card
        {
            Tier = Tier,
            Name = Name,
            Lines = Origin == null ? Lines.ToList() : Lines.Append($"<color=#8A8A8A>（海克斯大乱斗「{Origin}」）</color>").ToList(),
            Augment = this,
        };
    }

    internal static class Augments
    {
        private const string ForgeIcon = "Bundles/UI/Icon/Shop/forging";
        private const string GoldForgeIcon = "Bundles/UI/Icon/Shop/forging_gold";
        private const string EpicIcon = "Bundles/UI/Icon/Shop/forging_epic";
        public const int FrierenHero = 20;
        public const int SoulDevourerSkill = 512;

        // ---- 数值 ----
        public const float GoldRushNormal = 0.01f, GoldRushElite = 0.15f;
        public const int GoldRushPerWave = 3;
        public const float AxiomEliteRatio = 0.15f;
        public const int RecursionHaste = 40, InfiniteHaste = 60;
        public const int InfiniteNormalKills = 20, InfiniteElite = 2, InfiniteBoss = 10, InfiniteMax = 60;

        private static Augment Stat(string id, string name, string origin, Tier tier, string line, string key, int amount, string icon = EpicIcon,
            Dictionary<int, float> weights = null) => new Augment
        {
            Id = id, Name = name, Origin = origin, Pool = AugmentPool.General, Tier = tier, Lines = new[] { line }, IconPath = icon, HeroWeights = weights,
            OnPick = p => p.AddAttribute(key, amount),
            Revert = p => p.AddAttribute(key, -amount),
        };

        private static readonly Dictionary<int, float> Frieren = new Dictionary<int, float> { [FrierenHero] = 3f };

        public static readonly Augment[] All =
        {
            // ---- 银 ----
            Stat("blunt", "大力", "大力", Tier.Silver, "总伤害 +15%", "AddDamage", 15),
            Stat("deft", "灵巧", "灵巧", Tier.Silver, "攻速 +30%", "AttackSpeed", 30),
            Stat("goredrink", "嗜血", "嗜血", Tier.Silver, "吸血 +8%", "XiXueRate", 8),
            new Augment
            {
                Id = "stats", Name = "属性！", Origin = "属性！", Pool = AugmentPool.General, Tier = Tier.Silver, IconPath = ForgeIcon,
                Lines = new[] { "获得 2 次属性锻造（休息时打开，和商店买的一样）" },
                OnPick = _ => Engine.QueueForge(ForgeTier.Normal, 2),
            },
            new Augment
            {
                Id = "goldrush", Name = "淘金", Pool = AugmentPool.General, Tier = Tier.Silver, IconPath = ForgeIcon,
                Lines = new[]
                {
                    $"击杀敌人有概率获得一次属性锻造：普通 {GoldRushNormal:0%}、变异 {GoldRushElite:0%}、Boss 必得",
                    $"每波最多 {GoldRushPerWave} 次，休息时打开",
                },
            },

            // ---- 金 ----
            Stat("recursion", "循环往复", "循环往复", Tier.Gold, $"技能急速 +{RecursionHaste}", "CoolDown", RecursionHaste, weights: Frieren),
            Stat("critical", "暴击", "暴击", Tier.Gold, "暴击率 +20%", "Critical", 20),
            Stat("colossus", "巨像", "天体", Tier.Gold, "生命上限 +20%", "HpPercent", 20),
            new Augment
            {
                Id = "axiom", Name = "奥术公理", Origin = "奥术公理", Pool = AugmentPool.General, Tier = Tier.Gold, IconPath = EpicIcon, HeroWeights = Frieren,
                Lines = new[] { $"击杀变异怪：所有主动技能返还 {AxiomEliteRatio:0%} 冷却", "击杀 Boss：所有主动技能冷却立刻转好" },
            },

            // ---- 棱彩 ----
            new Augment
            {
                Id = "infinite", Name = "无限循环往复", Origin = "无限循环往复", Pool = AugmentPool.General, Tier = Tier.Prism, IconPath = EpicIcon,
                HeroWeights = Frieren,
                Lines = new[]
                {
                    $"技能急速 +{InfiniteHaste}",
                    $"之后每击杀 {InfiniteNormalKills} 个普通敌人 +1、每个变异怪 +{InfiniteElite}、每个 Boss +{InfiniteBoss}（最多再 +{InfiniteMax}）",
                },
                OnPick = p => p.AddAttribute("CoolDown", InfiniteHaste),
                Revert = p => p.AddAttribute("CoolDown", -InfiniteHaste),
            },
            new Augment
            {
                Id = "stats3", Name = "属性！属性！属性！", Origin = "属性！属性！属性！", Pool = AugmentPool.General, Tier = Tier.Prism, IconPath = GoldForgeIcon,
                Lines = new[] { "获得 3 次黄金属性锻造（休息时打开）" },
                OnPick = _ => Engine.QueueForge(ForgeTier.Gold, 3),
            },
            Stat("goliath", "巨人", "巨人", Tier.Prism, "生命上限 +35%", "HpPercent", 35),

            // ---- 招牌 ----
            new Augment
            {
                Id = "bonk", Name = "邦！", Origin = "邦！", Pool = AugmentPool.Signature, Tier = Tier.Gold, SkillId = SoulDevourerSkill,
                Lines = new[] { "噬魂一击的强化普攻同时打中主目标周围的敌人（60% 伤害，各自判定暴击）", "被溅射打死的敌人也照常叠层" },
            },

            // ---- 敌人 ----
            new Augment { Id = "e_tough", Name = "坚韧", Pool = AugmentPool.Enemy, Tier = Tier.Silver, EnemyHp = 1.1f, Lines = new[] { "敌人生命 +10%" } },
            new Augment { Id = "e_fierce", Name = "凶暴", Pool = AugmentPool.Enemy, Tier = Tier.Silver, EnemyAttack = 1.1f, Lines = new[] { "敌人攻击 +10%" } },
            new Augment { Id = "e_iron", Name = "铁骨", Pool = AugmentPool.Enemy, Tier = Tier.Gold, EnemyHp = 1.2f, Lines = new[] { "敌人生命 +20%" } },
            new Augment { Id = "e_rage", Name = "狂怒", Pool = AugmentPool.Enemy, Tier = Tier.Gold, EnemyAttack = 1.2f, Lines = new[] { "敌人攻击 +20%" } },
            new Augment
            {
                Id = "e_tide", Name = "魔潮", Pool = AugmentPool.Enemy, Tier = Tier.Prism, EnemyHp = 1.3f, EnemyAttack = 1.2f, Lines = new[] { "敌人生命 +30%、攻击 +20%" },
            },
        };

        public static Augment Get(string id) => All.FirstOrDefault(a => a.Id == id);

        // 按权重不放回地抽 count 张
        public static List<Augment> Draw(IEnumerable<Augment> pool, int heroId, int count, Random rng)
        {
            var left = pool.ToList();
            var result = new List<Augment>();
            while (result.Count < count && left.Count > 0)
            {
                double total = left.Sum(a => a.WeightFor(heroId));
                double r = rng.NextDouble() * total;
                var pick = left[left.Count - 1];
                foreach (var a in left)
                {
                    r -= a.WeightFor(heroId);
                    if (r < 0) { pick = a; break; }
                }
                result.Add(pick);
                left.Remove(pick);
            }
            return result;
        }
    }
}
