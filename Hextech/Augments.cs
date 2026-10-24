using System;
using System.Collections.Generic;
using System.IO;
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
        // 大乱斗（或 Source 指定的模式）里的原名，写在说明最后
        public string Origin;
        public string Source = "海克斯大乱斗";
        // 队友卡：房间里有队友时才出现（效果见 Team.cs）
        public bool Team;
        public AugmentPool Pool;
        public Tier Tier;
        public string[] Lines;
        public string IconPath;
        // 招牌卡：只在这个专属技能的英雄出现
        public int SkillId;
        // 某些英雄更容易抽到（英雄编号 → 权重倍数）
        public Dictionary<int, float> HeroWeights;
        public Action<Player> OnPick;
        // 要先选过其中任意一张才会出现
        public string[] Requires;
        // 撤销 OnPick 的效果（有人进房间、关掉总开关时）；一次性的锻造之类撤不了的留空
        public Action<Player> Revert;
        // 敌人卡：之后刷出的怪物倍率
        public float EnemyHp = 1f, EnemyAttack = 1f;
        // 招牌系列（「邦」「大炮」）：同一系列按档次升级，拿了高档的低档不再出现，生效取最高的那张
        public string Series;
        public int Rank;
        // 「邦」：噬魂一击溅射
        public SoulSplash Splash;
        // 「大炮」：男枪弹药
        public AmmoRule Ammo;

        public float WeightFor(int heroId) => HeroWeights != null && HeroWeights.TryGetValue(heroId, out var w) ? w : 1f;

        public Card ToCard() => new Card
        {
            Tier = Tier,
            Name = Name,
            Lines = Origin == null ? Lines.ToList() : Lines.Append($"<color=#8A8A8A>（{Source}「{Origin}」）</color>").ToList(),
            Augment = this,
        };
    }

    // 男枪弹药上限 = 2 + Flat + 超出 150% 的爆伤每 PerCrit 再 +1（PerCrit = 0 表示不按爆伤加），冲锋装填 Dash 发
    internal sealed class AmmoRule
    {
        public int Flat, Dash = 1;
        public float PerCrit;

        public int MaxFor(Player p)
        {
            int extra = PerCrit > 0f ? (int)Math.Floor(Math.Max(0f, p.CriticalDamage - 1.5f) / PerCrit) : 0;
            return Shotgun.BaseMax + Flat + extra;
        }
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
        // 实测（40 波图）第 5 波就叠满了上限 60，放慢普通击杀、提高上限，让它一直涨到后期
        public const int InfiniteNormalKills = 50, InfiniteElite = 2, InfiniteBoss = 10, InfiniteMax = 300;
        // 法力流：由心及物（蓝转血）、魔宗（蓝转攻击，放技能叠蓝）、左脚踩右脚（血转蓝）。
        // 蓝转血 × 血转蓝 = 1 × 0.05 < 1，循环会收敛（总共多出约 1/(1-0.05) 倍），所以不设上限
        public const float MindToMatter = 1f, ManamuneAttack = 0.05f, LoopManaPerHp = 0.05f;
        public const int ManamuneStack = 5, ManamuneMaxStacks = 300;
        // 第二批
        public const float ExcitedSpeed = 0.4f, ExcitedSeconds = 3f;
        public const float FruitElite = 0.3f;
        public const int FruitPerWave = 3, FruitItem = 1407;   // 痊愈神符
        public const int TankNormal = 3, TankElite = 40, TankBoss = 400;
        public const float PorcupineTrigger = 0.2f, PorcupineDamage = 0.5f, PorcupineRadius = 5f;
        public const float ArchmageRatio = 0.3f;
        public const int ShotgunSkill = 520;

        private static Augment Stat(string id, string name, string origin, Tier tier, string line, string key, int amount, string icon = EpicIcon,
            Dictionary<int, float> weights = null) => new Augment
        {
            Id = id, Name = name, Origin = origin, Pool = AugmentPool.General, Tier = tier, Lines = new[] { line }, IconPath = icon, HeroWeights = weights,
            OnPick = p => p.AddAttribute(key, amount),
            Revert = p => p.AddAttribute(key, -amount),
        };

        private static Augment TeamCard(string id, string name, string origin, Tier tier, params string[] lines) => new Augment
        {
            Id = id, Name = name, Origin = origin, Source = "斗魂竞技场", Pool = AugmentPool.General, Tier = tier, Lines = lines, IconPath = EpicIcon, Team = true,
        };

        private static readonly Dictionary<int, float> Frieren = new Dictionary<int, float> { [FrierenHero] = 3f };

        // 法力流的卡：数值每秒按当前的蓝、血重算（Engine.TickMana），撤销时减掉加过的量
        public static readonly string[] ManaCards = { "mind", "manamune", "loop" };

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
                Id = "mind", Name = "由心及物", Origin = "由心及物", Pool = AugmentPool.General, Tier = Tier.Silver, IconPath = EpicIcon, HeroWeights = Frieren,
                Lines = new[] { $"生命上限 + 最大法力的 {MindToMatter:0%}（蓝涨了血跟着涨）" },
            },
            new Augment
            {
                Id = "excited", Name = "兴奋", Origin = "兴奋", Pool = AugmentPool.General, Tier = Tier.Silver,
                Lines = new[] { $"击杀敌人后 {ExcitedSeconds:0} 秒内攻速 +{ExcitedSpeed:0%}（再击杀刷新时间）" },
            },
            new Augment
            {
                Id = "fruitify", Name = "果实化", Origin = "果实化", Pool = AugmentPool.General, Tier = Tier.Silver,
                Lines = new[] { $"击杀变异怪有 {FruitElite:0%} 概率掉落痊愈神符，击杀 Boss 必掉", $"每波最多 {FruitPerWave} 个" },
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
                Id = "manamune", Name = "魔宗", Origin = "魔宗（装备）", Pool = AugmentPool.General, Tier = Tier.Gold, IconPath = EpicIcon, HeroWeights = Frieren,
                Lines = new[]
                {
                    $"攻击 + 最大法力的 {ManamuneAttack:0%}",
                    $"每放一次主动技能，最大法力 +{ManamuneStack}；叠满 {ManamuneMaxStacks} 次后变成「魔切」，攻击加成翻倍",
                },
            },
            new Augment
            {
                Id = "tank", Name = "坦克引擎", Origin = "坦克引擎", Pool = AugmentPool.General, Tier = Tier.Gold,
                Lines = new[]
                {
                    $"每次击杀永久增加生命上限：普通 +{TankNormal}、变异 +{TankElite}、Boss +{TankBoss}",
                    "死亡时失去一半已增加的生命上限",
                },
            },
            new Augment
            {
                Id = "porcupine", Name = "豪猪", Origin = "豪猪", Pool = AugmentPool.General, Tier = Tier.Gold,
                Lines = new[] { $"累计受到生命上限 {PorcupineTrigger:0%} 的伤害时，对周围敌人造成生命上限 {PorcupineDamage:0%} 的伤害" },
            },
            new Augment
            {
                Id = "archmage", Name = "大法师", Origin = "大法师", Pool = AugmentPool.General, Tier = Tier.Gold, HeroWeights = Frieren,
                Lines = new[] { $"放主动技能时，随机另一个冷却中的主动技能冷却 -{ArchmageRatio:0%}" },
            },
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
            new Augment
            {
                Id = "dualwield", Name = "双持", Origin = "双持", Pool = AugmentPool.General, Tier = Tier.Prism,
                Lines = new[] { "多重攻击 +1（普攻多打一个目标）" },
                OnPick = p => p.AddAttackCount(1),
                Revert = p => p.AddAttackCount(-1),
            },
            new Augment
            {
                Id = "loop", Name = "左脚踩右脚", Pool = AugmentPool.General, Tier = Tier.Prism, IconPath = EpicIcon, HeroWeights = Frieren, Requires = new[] { "mind", "manamune" },
                Lines = new[] { $"最大法力 + 生命上限的 {LoopManaPerHp:0%}", "和由心及物、魔宗互相喂：蓝变多 → 血、攻击变多 → 蓝更多" },
            },

            // ---- 招牌（每档都有，开局抽到银也能拿到）----
            new Augment
            {
                Id = "bonk1", Name = "邦", Origin = "邦！", Pool = AugmentPool.Signature, Tier = Tier.Silver, SkillId = SoulDevourerSkill, Series = "bonk", Rank = 1,
                Splash = new SoulSplash { Radius = 2.5f, Ratio = 0.4f },
                Lines = new[] { "噬魂一击的强化普攻同时打中主目标附近的敌人（40% 伤害，各自判定暴击）", "被溅射打死的敌人也照常叠层" },
            },
            new Augment
            {
                Id = "bonk", Name = "邦！", Origin = "邦！", Pool = AugmentPool.Signature, Tier = Tier.Gold, SkillId = SoulDevourerSkill, Series = "bonk", Rank = 2,
                Splash = new SoulSplash { Radius = 3.5f, Ratio = 0.6f },
                Lines = new[] { "噬魂一击的强化普攻同时打中主目标周围的敌人（60% 伤害，各自判定暴击）", "被溅射打死的敌人也照常叠层" },
            },
            new Augment
            {
                Id = "bonk3", Name = "邦邦邦！", Origin = "邦！+ 叠角龙", Pool = AugmentPool.Signature, Tier = Tier.Prism, SkillId = SoulDevourerSkill, Series = "bonk", Rank = 3,
                Splash = new SoulSplash { Radius = 5f, Ratio = 1f, HitStacks = 1 },
                Lines = new[] { "噬魂一击的强化普攻同时打中大范围内的敌人（100% 伤害，各自判定暴击）", "溅射打死的照常叠层，没打死也 +1 层" },
            },

            new Augment
            {
                Id = "cannon1", Name = "上膛", Origin = "大炮", Pool = AugmentPool.Signature, Tier = Tier.Silver, SkillId = ShotgunSkill, Series = "cannon", Rank = 1,
                Ammo = new AmmoRule { Flat = 1, Dash = 2 },
                Lines = new[] { "弹药上限 +1", "冲锋装填一次装 2 发" },
            },
            new Augment
            {
                Id = "cannon", Name = "大炮", Origin = "大炮", Pool = AugmentPool.Signature, Tier = Tier.Gold, SkillId = ShotgunSkill, Series = "cannon", Rank = 2,
                Ammo = new AmmoRule { Flat = 1, Dash = 2, PerCrit = 1f },
                Lines = new[] { "弹药上限 +1，暴击伤害超过 150% 的部分每 100% 再 +1 发（最多 12 发）", "冲锋装填一次装 2 发" },
            },
            new Augment
            {
                Id = "cannon3", Name = "大炮·极", Origin = "大炮", Pool = AugmentPool.Signature, Tier = Tier.Prism, SkillId = ShotgunSkill, Series = "cannon", Rank = 3,
                Ammo = new AmmoRule { Flat = 1, Dash = 2, PerCrit = 0.5f },
                Lines = new[] { "弹药上限 +1，暴击伤害超过 150% 的部分每 50% 再 +1 发（最多 12 发）", "冲锋装填一次装 2 发" },
            },

            // ---- 队友（参照斗魂竞技场；都走游戏原版的治疗、护盾、buff，队友没装 Mod 也生效）----
            TeamCard("sacrifice", "甘愿牺牲", "甘愿牺牲", Tier.Silver,
                $"{Team.Range:0} 米内的队友生命低于 {Team.LowHp:P0} 时：消耗你 {Team.SacrificeMana:P0} 的当前法力，治疗他 你生命上限的 {Team.SacrificeHeal:P0}",
                $"同一个队友每 {Team.SacrificeCooldown:0} 秒最多一次"),
            TeamCard("vengeance", "复仇", "复仇", Tier.Silver,
                $"有队友倒下时，这一波剩下的时间里总伤害 +{Team.VengeanceDamage}%、吸血 +{Team.VengeanceLifesteal}%"),
            TeamCard("juicebox", "果汁盒", "果汁盒", Tier.Silver,
                "每次休息开始时，在每个玩家脚下掉一个痊愈神符"),
            TeamCard("maneuvers", "防御策略", "防御策略", Tier.Gold,
                $"你的生命低于 {Team.LowHp:P0} 时：{Team.ManeuverRadius:0} 米内的所有玩家得到 {Team.ManeuverShield:P0} 生命上限的护盾，范围内的队友回复 {Team.ManeuverHeal:P0} 生命",
                $"每 {Team.ManeuverCooldown:0} 秒最多一次"),
            TeamCard("spiritlink", "灵魂连接", "灵魂连接", Tier.Gold,
                $"{Team.Range:0} 米内的队友掉血时，治疗他掉血量的 {Team.LinkRatio:P0}",
                $"每秒最多治疗你生命上限的 {Team.LinkBudget:P0}"),
            TeamCard("banner", "号令之旗", "号令之旗", Tier.Gold,
                $"你放主动技能时，最近的队友（{Team.Range:0} 米内）获得圣焰祝福：伤害 +{Team.BannerDamage:P0}，持续 {Team.BannerSeconds:0} 秒"),
            TeamCard("sonicboom", "音爆", "音爆", Tier.Gold,
                $"你的海克斯治疗、护盾、祝福队友时，在他周围 {Team.BoomRadius:0} 米爆炸，造成你攻击 {Team.BoomDamage:P0} 的伤害",
                $"同一个队友每 {Team.BoomCooldown:0} 秒最多一次；和其他队友卡搭配"),
            TeamCard("divine", "神圣干预", "神圣干预", Tier.Prism,
                $"每 {Team.DivineInterval:0} 秒，{Team.DivineRadius:0} 米内的所有玩家得到 {Team.DivineShield:P0} 生命上限的护盾，你无敌 {Team.DivineInvulnerable:0.#} 秒"),
            TeamCard("clothesline", "晾衣绳", "晾衣绳", Tier.Prism,
                $"你和 {Team.LineRange:0} 米内的每个队友之间连一条线，每 {Team.LineInterval:0.#} 秒对线上的敌人造成你攻击 {Team.LineDamage:P0} 的伤害"),

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

        // 手绘图标 assets/icons/<Id>.png（tools/HextechIcons/draw.py 生成）；没有文件或图标补丁没装上时沿用锻造器图标
        public static void LoadIcons(ModContext mod)
        {
            if (!ModAssets.IconsAvailable || mod.Folder == null) return;
            int n = 0;
            foreach (var a in All)
            {
                string file = Path.Combine("assets", "icons", a.Id + ".png");
                if (!File.Exists(Path.Combine(mod.Folder, file))) continue;
                a.IconPath = mod.LoadIcon(a.Id, file);
                n++;
            }
            mod.RunLog.Info("图标", "载入手绘图标", ("数量", n));
        }

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
