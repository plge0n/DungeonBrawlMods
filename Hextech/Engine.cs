using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;

namespace Hextech
{
    // 一局里的海克斯：什么时候弹、选中后怎么生效、释放时和击杀时的触发效果、休息时打开锻造、敌人海克斯、重试波次时的存取。
    // 联机按 ModKit 的联机握手（Multiplayer.GameplayAllowed）：进战斗时不允许就本局不开；中途降级只是不再弹，已选的照常生效。
    // 联机时游戏不能暂停，三选一只在休息阶段弹；敌人海克斯以房主为准（怪物在房主那边生成），房主通过大厅数据公布敌人拿到的卡
    internal static class Engine
    {
        private const string EnemyKey = "dbmods.hextech.enemy";
        // 等游戏自己的遗物选择先弹出来，界面空闲这么久之后才弹
        private const float IdleDelay = 1.5f;
        // 开局那次：看到游戏的开局遗物（或第一波开打、或进战斗这么久）之后才弹，避免抢在开场界面前面
        private const float OpeningFallback = 15f;
        public const int Rounds = 4;

        // 本局状态。重试波次时整份存取，所以只放可以直接复制的东西
        private sealed class Run
        {
            public int Seed;
            public List<Card> Picks = new List<Card>();
            public int Round;
            public int RefreshLeft;
            public int RefreshIndex;
            public int Prisms;
            public bool Pending;
            public List<string> Shown = new List<string>();
            // 欠的属性锻造（休息时依次打开）、本波"淘金"已经给了几次
            public List<ForgeTier> Forges = new List<ForgeTier>();
            public int WaveForges;
            // 无限循环往复：击杀累加的技能急速、普通敌人击杀计数
            public int InfiniteBonus, InfiniteKills;
            // 法力流已经加上去的生命上限、攻击、最大法力；魔宗叠了几次
            public int ManaHp, ManaAttack, ManaMp, ManamuneStacks;
            // 第二批：坦克引擎已加 / 待加的生命上限、本波果实化掉了几个、豪猪攒的伤害
            public int TankHp, TankPending, WaveFruits;
            public double PorcupineStored;
            // 敌人海克斯拿到的卡；多给刷新次数的是哪一回合
            public List<Card> EnemyPicks = new List<Card>();
            public int BonusRound = -1;

            public Run Clone()
            {
                var r = (Run)MemberwiseClone();
                r.Picks = new List<Card>(Picks);
                r.Shown = new List<string>(Shown);
                r.Forges = new List<ForgeTier>(Forges);
                r.EnemyPicks = new List<Card>(EnemyPicks);
                return r;
            }
        }

        private static ModContext mod;
        private static Run run = new Run();
        private static Run checkpoint;
        // 专属技能暂不支持（没有参数的被动等）时 profile 为 null，只抽通用卡
        private static SkillProfile profile;
        private static HeroSkill heroSkill;
        private static bool started, disabled, blocked;
        // 客户端：房主公布的敌人海克斯（上次读到的原文）、下次读的时间
        private static string hostEnemy;
        private static float hostEnemyAt;
        private static List<int> offerWaves;
        private static float openAt, battleStart, forgeAt;
        private static bool sawGameChoice, waveStarted, inWave;
        private static float[] appliedPassive;
        private static bool splashSet, ammoSet;
        // 兴奋：限时攻速是否加着、到什么时候
        private static bool excitedOn;
        private static float excitedUntil, tankAt;

        public static IReadOnlyList<Card> Picks => run.Picks;
        public static IReadOnlyList<Card> EnemyPicks => run.EnemyPicks;
        public static SkillProfile Profile => profile;
        public static HeroSkill HeroSkill => heroSkill;
        public static bool Active => Plugin.Enabled.Value && !disabled && started;

        // 敌人海克斯由自己决定：单人，或者联机的房主（客户端跟房主走）
        private static bool OwnEnemy => Game.IsHost;

        // 本局有敌人海克斯：自己决定时看自己的设置，客户端看房主公布的
        private static bool EnemyOn => OwnEnemy ? Plugin.EnemyAugments.Value : hostEnemy != null && hostEnemy.StartsWith("1");

        public static void Attach(ModContext ctx)
        {
            mod = ctx;
            mod.OnBattleStarted(Reset);
            mod.OnBattleEnded(() =>
            {
                Hud.RemoveAll();
                Triggers.Clear(revert: false);
                appliedPassive = null;
                profile = null;
                started = false;
                excitedOn = false;
                ClearState();
                LobbySync.Clear(EnemyKey);
            });
            mod.OnGameEvent(OnEvent);
            mod.OnSkillCast(Triggers.OnCast);
            mod.OnSkillCast(OnAnyCast);
            mod.OnHit(Triggers.OnHit);
            mod.OnCheckpointSaved(() => checkpoint = run.Clone());
            mod.OnCheckpointRestored(Restore);
            mod.OnUpdate(Tick);
            Team.Attach(ctx);
        }

        private static void Reset()
        {
            run = new Run { Seed = UnityEngine.Random.Range(1, int.MaxValue), RefreshLeft = Plugin.Refreshes.Value, Pending = true };
            checkpoint = null;
            profile = null;
            heroSkill = null;
            started = disabled = blocked = false;
            hostEnemy = null;
            hostEnemyAt = 0f;
            offerWaves = null;
            appliedPassive = null;
            battleStart = Time.unscaledTime;
            openAt = battleStart + IdleDelay;
            forgeAt = 0f;
            excitedOn = false;
            sawGameChoice = waveStarted = inWave = false;
            mod.ClearSkillTunes();
            ClearState();
            Triggers.Clear(revert: false);
            Team.Reset();
        }

        // 持续状态（溅射、怪物倍率）清空：只改 ModKit 里的设置，不碰英雄
        private static void ClearState()
        {
            if (splashSet) mod.SetSoulSplash(null);
            if (ammoSet) mod.SetShotgunAmmo(null);
            splashSet = ammoSet = false;
            mod.SetEnemyBoost(1f, 1f, 1f);
        }

        private static void Tick()
        {
            var player = Game.LocalPlayer;
            if (player == null) return;
            if (disabled) return;
            if (!Plugin.Enabled.Value)
            {
                // 战斗中关掉总开关：本局停用（加过的属性只撤销一次）
                disabled = true;
                RevertAll();
                mod.RunLog.Info("开关", "总开关关闭，本局停用");
                return;
            }
            if (!Multiplayer.GameplayAllowed)
            {
                if (!started)
                {
                    // 进战斗时联机握手就不允许：本局不开（ModKit 已经提示过"改玩法的内容本局停用"）
                    disabled = true;
                    mod.RunLog.Info("联机", "联机握手不允许改玩法的内容，本局不开", ("说明", Multiplayer.Status));
                    return;
                }
                if (!blocked)
                {
                    // 中途降级（有人进房、装的不一样）：之后不再弹，已经选了的不收回（和 ModKit 的约定一样）
                    blocked = true;
                    run.Pending = false;
                    mod.RunLog.Info("联机", "联机握手降级，之后不再弹三选一", ("说明", Multiplayer.Status));
                }
            }
            if (ChoicePanel.IsBusy) sawGameChoice = true;
            if (!started)
            {
                // 刚进战斗时英雄可能还没生成完，等一会儿；3 秒后还查不到专属技能就只抽通用卡
                if (player.HeroSkill == null && Time.unscaledTime - battleStart < 3f) return;
                Start(player);
            }

            Triggers.Tick(player);
            Team.Tick(player, inWave);
            TickHostEnemy();
            TickOffer();
            TickForge();
            TickMana(player);
            TickBatch2(player);
        }

        private static void Start(Player player)
        {
            started = true;
            var hs = player.HeroSkill;
            heroSkill = hs;
            if (hs == null || !SkillData.Supported(hs.IsActive, hs.SkillId))
            {
                mod.RunLog.Info("英雄", "专属技能没有专属卡，只抽通用卡", ("技能", hs?.Name), ("编号", hs?.SkillId), ("主动", hs?.IsActive));
                Apply();
                return;
            }
            profile = hs.IsActive
                ? SkillData.Active(hs.SkillId, hs.Name, hs.Cooldown, hs.Cost, hs.Duration, hs.Charges)
                : SkillData.Passive(hs.SkillId, hs.Name, hs.PassiveValues.ToArray());
            mod.RunLog.Info("英雄", "专属技能", ("技能", hs.Name), ("编号", hs.SkillId), ("主动", hs.IsActive),
                ("可用积木", string.Join(" ", Generator.Candidates(profile).Select(b => b.Word))));
            Apply();
        }

        // ---- 出现时机 ----
        private static void OnEvent(GameEvent e)
        {
            if (e.Kind == GameEventKind.EnemyKilled)
            {
                if (Active) OnKill(e);
                return;
            }
            if (e.Kind == GameEventKind.Damaged)
            {
                if (Active) OnDamaged(e);
                return;
            }
            if (e.Kind == GameEventKind.Revived)
            {
                if (Active) OnRevived();
                return;
            }
            if (e.Kind == GameEventKind.WaveStarted)
            {
                waveStarted = inWave = true;
                run.WaveForges = run.WaveFruits = 0;
            }
            if (e.Kind != GameEventKind.WaveEnded) return;
            inWave = false;
            forgeAt = Time.unscaledTime + IdleDelay;
            // 游戏随后拍重试快照：限时加成撤掉、攒着的生命上限先加上
            var me = Game.LocalPlayer;
            if (me != null)
            {
                StopExcited(me);
                FlushTank(me);
                if (Active) Team.OnWaveEnded(me);
            }
            // 休息阶段：限时加成提前结束（游戏随后拍重试快照，快照里不能带着加成）
            Triggers.Clear(revert: true);
            if (run.Round >= Rounds || run.Round == 0) return;
            if (OfferWaves().Contains(Game.Wave) && !blocked)
            {
                run.Pending = true;
                openAt = Time.unscaledTime + IdleDelay;
            }
        }

        // 第 2、3、4 次：约 25%、50%、75% 波次附近的遗物回合（没有遗物回合时就按比例取波次）
        private static List<int> OfferWaves()
        {
            if (offerWaves != null) return offerWaves;
            int total = Game.TotalWaves;
            if (total <= 1) return new List<int>();
            var relics = Game.RelicWaves.Where(w => w > 1).ToList();
            var list = new List<int>();
            foreach (float frac in new[] { 0.25f, 0.5f, 0.75f })
            {
                float target = total * frac;
                var pool = (relics.Count > 0 ? relics : Enumerable.Range(2, total - 1).ToList()).Where(w => !list.Contains(w)).ToList();
                if (pool.Count == 0) break;
                list.Add(pool.OrderBy(w => Math.Abs(w - target)).ThenByDescending(w => w).First());
            }
            list.Sort();
            offerWaves = list;
            mod.RunLog.Info("时机", "海克斯回合", ("总波数", total), ("遗物回合", string.Join(",", relics)), ("海克斯", "开局," + string.Join(",", list)));
            return list;
        }

        private static void TickOffer()
        {
            if (blocked || !Due()) return;
            run.Pending = false;
            Offer();
        }

        // 该弹出了：界面空闲够久；开局那次还要等开场界面和开局遗物出现过
        private static bool Due()
        {
            if (!run.Pending) return false;
            float now = Time.unscaledTime;
            if (ChoicePanel.IsBusy)
            {
                openAt = now + IdleDelay;
                return false;
            }
            // 联机时游戏不暂停：只在休息阶段弹，错过开局的话等第一波打完
            if (!Game.IsSolo && inWave)
            {
                openAt = now + IdleDelay;
                return false;
            }
            if (run.Round == 0 && !sawGameChoice && !waveStarted && now - battleStart < OpeningFallback) return false;
            return now >= openAt;
        }

        private static Tier TierFor(int round, System.Random rng)
        {
            double r = rng.NextDouble();
            Tier t;
            switch (round)
            {
                case 0: t = r < 0.6 ? Tier.Silver : Tier.Gold; break;
                case 1: t = r < 0.2 ? Tier.Silver : r < 0.9 ? Tier.Gold : Tier.Prism; break;
                case 2: t = r < 0.5 ? Tier.Gold : Tier.Prism; break;
                default: t = r < 0.25 ? Tier.Gold : Tier.Prism; break;
            }
            // 一局最多 2 个棱彩
            return t == Tier.Prism && run.Prisms >= 2 ? Tier.Gold : t;
        }

        public static int QualityColor(Tier t) => t == Tier.Silver ? 2 : t == Tier.Gold ? 3 : 4;

        public static string TierName(Tier t) => t == Tier.Silver ? "银" : t == Tier.Gold ? "金" : "棱彩";

        private static bool Picked(string augmentId) => run.Picks.Any(c => c.Augment?.Id == augmentId);

        public static bool Has(string augmentId) => Active && Picked(augmentId);

        // 已经拿到这个系列的第几档（0 = 没拿）
        private static int SeriesRank(string series) => Best(series)?.Rank ?? 0;

        private static Augment Best(string series)
            => run.Picks.Select(c => c.Augment).Where(a => a?.Series == series).OrderByDescending(a => a.Rank).FirstOrDefault();

        public static string IconFor(Card card) => card.Augment?.IconPath ?? heroSkill?.IconPath ?? "Bundles/UI/Icon/Shop/forging_epic";

        // 一次三张：有专属卡的英雄"专属 2 + 通用 1"（招牌卡有一半机会占一个专属位），其他英雄 3 张通用卡
        private static List<Card> Draw(Tier tier, System.Random rng)
        {
            var cards = new List<Card>();
            if (profile != null)
            {
                var ids = SkillIds().ToList();
                var signature = Augments.All.Where(a => a.Pool == AugmentPool.Signature && a.Tier == tier && ids.Contains(a.SkillId)
                    && !Picked(a.Id) && (a.Series == null || a.Rank > SeriesRank(a.Series)) && !run.Shown.Contains("a:" + a.Id)).ToList();
                if (signature.Count > 0 && rng.NextDouble() < 0.7) cards.Add(signature[rng.Next(signature.Count)].ToCard());
                // 积木少的技能刷新几次后会凑出同名的卡（数值不同），同名的只留一张，空位给通用卡
                foreach (var c in Generator.Offer(profile, tier, 2 - cards.Count, rng, run.Shown))
                    if (cards.All(x => x.Name != c.Name)) cards.Add(c);
            }
            int general = 3 - cards.Count;
            // 队友卡只在房间里有队友时出现
            var pool = Augments.All.Where(a => a.Pool == AugmentPool.General && a.Tier == tier && !Picked(a.Id)
                && (a.Requires == null || a.Requires.Any(Picked)) && (!a.Team || Team.HasTeammates)).ToList();
            var fresh = pool.Where(a => !run.Shown.Contains("a:" + a.Id)).ToList();
            int heroId = Game.LocalPlayer?.HeroId ?? -1;
            cards.AddRange(Augments.Draw(fresh.Count >= general ? fresh : pool, heroId, general, rng).Select(a => a.ToCard()));
            return cards;
        }

        private static void Offer()
        {
            if (run.Round >= Rounds) return;
            // 品质只由种子和回合决定，刷新不改品质；卡由种子、回合、刷新次数决定，重试波次后弹出来的和原来一样
            var tier = TierFor(run.Round, new System.Random(run.Seed ^ (run.Round * 7919)));
            var rng = new System.Random(run.Seed ^ (run.Round * 7919) ^ (run.RefreshIndex * 104729 + 1));
            var cards = Draw(tier, rng);
            if (cards.Count == 0)
            {
                mod.RunLog.Warning("三选一", "没有生成出强化", ("技能", profile?.Name), ("品质", TierName(tier)));
                run.Round++;
                return;
            }
            // 敌人海克斯：每回合多给一次刷新
            if (EnemyOn && run.BonusRound != run.Round)
            {
                run.BonusRound = run.Round;
                run.RefreshLeft++;
            }
            var options = cards.Select(c => new ChoiceOption
            {
                Name = c.Name,
                Description = string.Join("\n", c.Lines),
                IconPath = IconFor(c),
                Quality = QualityColor(tier),
            }).ToList();
            bool canRefresh = run.RefreshLeft > 0;
            if (canRefresh)
                options.Add(new ChoiceOption
                {
                    Name = $"刷新（还剩 {run.RefreshLeft} 次）",
                    Description = "换一批同品质的强化",
                    IconPath = heroSkill?.IconPath ?? "Bundles/UI/Icon/Shop/forging_epic",
                });
            string enemy = !EnemyOn ? "" : OwnEnemy ? "，敌人也会拿一张" : "，敌人海克斯跟房主走";
            string title = $"海克斯强化{(profile != null ? " · " + profile.Name : "")}（{TierName(tier)}，第 {run.Round + 1}/{Rounds} 次{enemy}）";
            if (!ChoicePanel.TryShow(title, options, i => OnPick(cards, i, tier)))
            {
                // 刚好被占用了，下一帧再试
                run.Pending = true;
                return;
            }
            run.Shown.AddRange(cards.Select(c => c.Shape));
        }

        private static void OnPick(List<Card> cards, int index, Tier tier)
        {
            if (!Active) return;
            if (index >= cards.Count)
            {
                run.RefreshLeft--;
                run.RefreshIndex++;
                run.Pending = true;
                openAt = Time.unscaledTime + 0.3f;
                mod.RunLog.Info("三选一", "刷新", ("剩余", run.RefreshLeft));
                return;
            }
            var card = cards[Mathf.Clamp(index, 0, cards.Count - 1)];
            run.Picks.Add(card);
            if (tier == Tier.Prism) run.Prisms++;
            run.Round++;
            run.Shown.Clear();
            mod.RunLog.Info("三选一", "选中", ("强化", card.Name), ("品质", TierName(tier)), ("效果", string.Join("；", card.Lines)));
            var player = Game.LocalPlayer;
            if (card.Augment?.OnPick != null && player != null) card.Augment.OnPick(player);
            string notice = $"获得海克斯强化「{card.Name}」";
            if (OwnEnemy && Plugin.EnemyAugments.Value)
            {
                var rng = new System.Random(run.Seed ^ (run.Round * 31337));
                var pool = Augments.All.Where(a => a.Pool == AugmentPool.Enemy && a.Tier == tier).ToList();
                if (pool.Count > 0)
                {
                    var enemy = pool[rng.Next(pool.Count)].ToCard();
                    run.EnemyPicks.Add(enemy);
                    mod.RunLog.Info("敌人海克斯", "敌人获得", ("强化", enemy.Name), ("效果", string.Join("；", enemy.Lines)));
                    notice += $"\n<color=#FF6060>敌人获得海克斯「{enemy.Name}」：之后刷出的{string.Join("、", enemy.Lines)}</color>";
                }
            }
            Apply();
            PublishEnemy();
            ScreenNotice.Show(notice, 4f);
        }

        // ---- 联机的敌人海克斯：房主公布 "开关|卡,卡"，客户端照着设（客户端设的倍率 ModKit 不用，只用来显示）----
        private static void PublishEnemy()
        {
            if (!OwnEnemy || !LobbySync.InLobby) return;
            LobbySync.Publish(EnemyKey, (Plugin.EnemyAugments.Value ? "1|" : "0|") + string.Join(",", run.EnemyPicks.Select(c => c.Augment.Id)));
        }

        private static void TickHostEnemy()
        {
            if (Time.unscaledTime < hostEnemyAt) return;
            hostEnemyAt = Time.unscaledTime + 1f;
            if (OwnEnemy)
            {
                // 开局先公布开关，客户端好知道要不要多给刷新
                if (run.Round == 0) PublishEnemy();
                return;
            }
            string value = LobbySync.ReadHost(EnemyKey);
            if (value == hostEnemy) return;
            hostEnemy = value;
            // 别人写的数据不可信：只认敌人卡池里的编号，最多 Rounds 张
            var parts = (value ?? "").Split('|');
            var ids = parts.Length == 2 ? parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) : new string[0];
            var picks = ids.Take(Rounds).Select(id => Augments.All.FirstOrDefault(a => a.Pool == AugmentPool.Enemy && a.Id == id))
                .Where(a => a != null).Select(a => a.ToCard()).ToList();
            foreach (var c in picks.Skip(run.EnemyPicks.Count))
            {
                mod.RunLog.Info("敌人海克斯", "房主那边敌人获得", ("强化", c.Name), ("效果", string.Join("；", c.Lines)));
                ScreenNotice.Show($"<color=#FF6060>敌人获得海克斯「{c.Name}」（房主）：之后刷出的{string.Join("、", c.Lines)}</color>", 4f);
            }
            run.EnemyPicks = picks;
            Hud.Refresh();
        }

        // ---- 击杀触发（淘金、奥术公理、无限循环往复）----
        private static void OnKill(GameEvent e)
        {
            var player = Game.LocalPlayer;
            if (player == null) return;
            bool boss = e.IsBoss, elite = e.IsElite && !boss;
            if (Picked("goldrush") && run.WaveForges < Augments.GoldRushPerWave)
            {
                float chance = boss ? 1f : elite ? Augments.GoldRushElite : Augments.GoldRushNormal;
                if (UnityEngine.Random.value < chance)
                {
                    run.WaveForges++;
                    QueueForge(ForgeTier.Normal, 1);
                }
            }
            if (Picked("axiom") && (boss || elite))
            {
                foreach (var slot in player.ActiveSkills())
                {
                    // 开着的开关技能（献祭之类）不动，免得被当成"可以再按"
                    if (slot.IsToggledOn) continue;
                    if (boss) slot.ResetCooldown();
                    else slot.ReduceCooldown(slot.CooldownTotal * Augments.AxiomEliteRatio);
                }
                mod.RunLog.Debug("奥术公理", boss ? "Boss：冷却全部转好" : "变异怪：返还冷却");
            }
            // 休息阶段（波次结束后才判定的最后几只）不加：游戏马上要拍重试快照，限时攻速会被拍进去
            if (Picked("excited") && inWave)
            {
                if (!excitedOn) player.AddAttackSpeed(Augments.ExcitedSpeed);
                excitedOn = true;
                excitedUntil = Time.time + Augments.ExcitedSeconds;
            }
            if (Picked("fruitify") && (boss || elite) && run.WaveFruits < Augments.FruitPerWave
                && (boss || UnityEngine.Random.value < Augments.FruitElite) && e.Target != null)
            {
                run.WaveFruits++;
                player.DropItem(Augments.FruitItem, e.Target.Position);
            }
            if (Picked("tank")) run.TankPending += boss ? Augments.TankBoss : elite ? Augments.TankElite : Augments.TankNormal;
            if (Picked("infinite") && run.InfiniteBonus < Augments.InfiniteMax)
            {
                int add = boss ? Augments.InfiniteBoss : elite ? Augments.InfiniteElite : ++run.InfiniteKills % Augments.InfiniteNormalKills == 0 ? 1 : 0;
                add = Math.Min(add, Augments.InfiniteMax - run.InfiniteBonus);
                if (add > 0)
                {
                    player.AddAttribute("CoolDown", add);
                    run.InfiniteBonus += add;
                }
            }
        }

        // ---- 第二批的持续效果：兴奋到时撤销、坦克引擎每秒合并一次加生命上限（每次都会同步，别每杀一只就发一次）----
        private static void TickBatch2(Player player)
        {
            if (excitedOn && Time.time >= excitedUntil) StopExcited(player);
            if (run.TankPending > 0 && Time.unscaledTime >= tankAt)
            {
                tankAt = Time.unscaledTime + 1f;
                FlushTank(player);
            }
        }

        private static void StopExcited(Player player)
        {
            if (!excitedOn) return;
            excitedOn = false;
            player.AddAttackSpeed(-Augments.ExcitedSpeed);
        }

        private static void FlushTank(Player player)
        {
            if (run.TankPending <= 0) return;
            player.AddAttribute("HP", run.TankPending);
            run.TankHp += run.TankPending;
            run.TankPending = 0;
        }

        // 坦克引擎：死亡失去一半（复活时扣）
        private static void OnRevived()
        {
            var player = Game.LocalPlayer;
            if (player == null || !Picked("tank")) return;
            FlushTank(player);
            int lose = run.TankHp / 2;
            if (lose <= 0) return;
            player.AddAttribute("HP", -lose);
            run.TankHp -= lose;
            ScreenNotice.Show($"坦克引擎：死亡失去 {lose} 点生命上限（还剩 +{run.TankHp}）", 3f);
        }

        // 豪猪：掉的血攒够生命上限的一定比例就爆一次
        private static void OnDamaged(GameEvent e)
        {
            if (!Picked("porcupine")) return;
            var player = Game.LocalPlayer;
            if (player == null || !player.IsAlive) return;
            run.PorcupineStored += e.Amount;
            double need = player.MaxHp * Augments.PorcupineTrigger;
            if (need <= 0 || run.PorcupineStored < need) return;
            run.PorcupineStored = 0;
            player.DamageArea((float)Math.Min(float.MaxValue / 4, player.MaxHp * Augments.PorcupineDamage), player.Position, Augments.PorcupineRadius,
                "Effect/DeathExplosion", 1.5f);
        }

        // 大法师：放主动技能时，随机另一个冷却中的技能冷却 -30%
        private static void Archmage(SkillCast cast)
        {
            var player = Game.LocalPlayer;
            if (player == null) return;
            var others = player.ActiveSkills().Where(s => s.SkillId != cast.SkillId && s.CooldownRemaining > 0f && !s.IsToggledOn).ToList();
            if (others.Count == 0) return;
            var slot = others[UnityEngine.Random.Range(0, others.Count)];
            float before = slot.CooldownRemaining;
            slot.ReduceCooldown(slot.CooldownTotal * Augments.ArchmageRatio);
            mod.RunLog.Debug("大法师", "减冷却", ("技能", slot.Info?.Name), ("剩余", $"{before:0.#}→{slot.CooldownRemaining:0.#}"));
        }

        // ---- 法力流（由心及物、魔宗、左脚踩右脚）：每秒按当前的蓝、血算目标值，只补差额 ----
        private static float manaAt;

        private static void OnAnyCast(SkillCast cast)
        {
            if (cast.IsFree || !Active) return;
            if (Picked("archmage")) Archmage(cast);
            if (Game.LocalPlayer is Player me) Team.OnCast(me);
            if (!Picked("manamune") || run.ManamuneStacks >= Augments.ManamuneMaxStacks) return;
            run.ManamuneStacks++;
            if (run.ManamuneStacks == Augments.ManamuneMaxStacks) ScreenNotice.Show("魔宗叠满，变成「魔切」：攻击加成翻倍", 3f);
        }

        private static void TickMana(Player player)
        {
            if (Time.unscaledTime < manaAt || !Augments.ManaCards.Any(Picked)) return;
            manaAt = Time.unscaledTime + 1f;
            // 先算蓝（魔宗叠的 + 血转蓝），再用新的蓝算血和攻击；循环靠每秒重算逐步收敛
            int mp = (Picked("manamune") ? run.ManamuneStacks * Augments.ManamuneStack : 0)
                + (Picked("loop") ? (int)Math.Min(int.MaxValue / 4, player.MaxHp * Augments.LoopManaPerHp) : 0);
            Adjust(player, "MP", ref run.ManaMp, mp);
            long maxMana = player.MaxMana;
            int hp = Picked("mind") ? (int)Math.Min(int.MaxValue / 4, maxMana * Augments.MindToMatter) : 0;
            float attackRatio = Augments.ManamuneAttack * (run.ManamuneStacks >= Augments.ManamuneMaxStacks ? 2f : 1f);
            int attack = Picked("manamune") ? (int)Math.Min(int.MaxValue / 4, maxMana * attackRatio) : 0;
            Adjust(player, "HP", ref run.ManaHp, hp);
            Adjust(player, "Attack", ref run.ManaAttack, attack);
        }

        // 已经加了 applied，目标是 target：只加差额（变化不到 1% 不动，免得每秒都发同步）
        private static void Adjust(Player player, string key, ref int applied, int target)
        {
            int delta = target - applied;
            if (delta == 0 || Math.Abs(delta) < Math.Max(1, Math.Abs(target) / 100)) return;
            player.AddAttribute(key, delta);
            applied = target;
        }

        private static void RevertMana(Player player)
        {
            if (player == null) return;
            if (run.ManaHp != 0) player.AddAttribute("HP", -run.ManaHp);
            if (run.ManaAttack != 0) player.AddAttribute("Attack", -run.ManaAttack);
            if (run.ManaMp != 0) player.AddAttribute("MP", -run.ManaMp);
            run.ManaHp = run.ManaAttack = run.ManaMp = 0;
        }

        // ---- 属性锻造：攒着，休息时（三选一界面空闲、海克斯没在等着弹）依次打开 ----
        public static void QueueForge(ForgeTier tier, int count)
        {
            for (int i = 0; i < count; i++) run.Forges.Add(tier);
            mod.RunLog.Info("锻造", "攒下属性锻造", ("档次", tier), ("次数", count), ("共欠", run.Forges.Count));
            string when = inWave ? "，这波打完后打开" : "";
            ScreenNotice.Show($"获得 {count} 次{(tier == ForgeTier.Gold ? "黄金" : "")}属性锻造{when}（共 {run.Forges.Count} 次）", 2.5f);
        }

        private static void TickForge()
        {
            if (run.Forges.Count == 0 || inWave || run.Pending) return;
            float now = Time.unscaledTime;
            if (ChoicePanel.IsBusy)
            {
                forgeAt = now + IdleDelay;
                return;
            }
            if (now < forgeAt || !Forging.CanOpen) return;
            if (!Forging.Open(run.Forges[0])) return;
            run.Forges.RemoveAt(0);
            forgeAt = now + IdleDelay;
        }

        // ---- 生效 ----
        private static void Apply()
        {
            var player = Game.LocalPlayer;
            if (player == null) return;
            ApplyState();
            if (profile == null)
            {
                Hud.Refresh();
                return;
            }
            var totals = Totals.Of(profile, run.Picks);
            if (profile.Active)
            {
                var tune = new SkillTune
                {
                    Damage = totals.Damage, Cooldown = totals.Cooldown, Cost = totals.Cost, Duration = totals.Duration, ExtraCharges = totals.Charges,
                };
                string tip = Hud.TooltipText(run.Picks);
                foreach (int id in SkillIds())
                {
                    mod.TuneSkill(id, tune);
                    mod.SetSkillTooltip(id, tip);
                }
            }
            else if (appliedPassive == null || !appliedPassive.SequenceEqual(totals.PassiveValues))
            {
                if (player.SetPassiveValues(profile.SkillId, totals.PassiveValues)) appliedPassive = totals.PassiveValues;
            }
            Triggers.Rebuild(profile, run.Picks);
            Hud.Refresh();
        }

        // 持续的状态：按已选的卡重新设置，可以反复调用（重试波次后也走这里）
        private static void ApplyState()
        {
            var splash = Best("bonk")?.Splash;
            if (splash != null || splashSet)
            {
                // 相同设置 ModKit 只是覆盖，重复调用无妨
                mod.SetSoulSplash(splash);
                splashSet = splash != null;
            }
            var ammo = Best("cannon")?.Ammo;
            if (ammo != null || ammoSet)
            {
                if (ammo == null) mod.SetShotgunAmmo(null);
                else mod.SetShotgunAmmo(() => Game.LocalPlayer is Player p ? ammo.MaxFor(p) : Shotgun.BaseMax, ammo.Dash);
                ammoSet = ammo != null;
            }
            float hp = 1f, attack = 1f;
            foreach (var c in run.EnemyPicks)
            {
                hp *= c.Augment.EnemyHp;
                attack *= c.Augment.EnemyAttack;
            }
            mod.SetEnemyBoost(hp, attack);
        }

        // 篮球巨星的花式扣篮在拿到"鸡舞"卡牌后会被换成真·鸡舞（515），强化跟着走
        public static IEnumerable<int> SkillIds()
        {
            if (profile == null || !profile.Active) yield break;
            yield return profile.SkillId;
            if (profile.SkillId == 509) yield return 515;
        }

        // 卸载（热重载）时：技能调整由 ModContext 自动撤销，这里撤销被动数值和限时加成
        public static void Unload()
        {
            RevertAll();
            LobbySync.Clear(EnemyKey);
        }

        private static void RevertAll()
        {
            var me = Game.LocalPlayer;
            if (me != null)
            {
                foreach (var c in run.Picks) c.Augment?.Revert?.Invoke(me);
                if (run.InfiniteBonus > 0) me.AddAttribute("CoolDown", -run.InfiniteBonus);
                RevertMana(me);
                StopExcited(me);
                if (run.TankHp > 0) me.AddAttribute("HP", -run.TankHp);
                Team.Revert(me);
            }
            run.InfiniteBonus = run.TankHp = run.TankPending = 0;
            run.Forges.Clear();
            ClearState();
            mod.ClearSkillTunes();
            foreach (int id in SkillIds()) mod.SetSkillTooltip(id, null);
            Triggers.Clear(revert: true);
            var player = Game.LocalPlayer;
            if (player != null && profile != null && !profile.Active && appliedPassive != null)
                player.SetPassiveValues(profile.SkillId, profile.BaseValues);
            appliedPassive = null;
            Hud.Refresh();
        }

        // 重试波次：游戏已经把英雄、技能实例还原到快照（被动数值、限时加成也跟着回去了），这里还原自己的记录再重新生效
        private static void Restore()
        {
            if (checkpoint == null) return;
            run = checkpoint.Clone();
            Triggers.Clear(revert: false);
            excitedOn = false;
            appliedPassive = null;
            mod.RunLog.Info("重试波次", "还原", ("已选", run.Picks.Count), ("待弹", run.Pending));
            if (run.Pending) openAt = Time.unscaledTime + IdleDelay;
            // 游戏随后会重弹这一波的遗物选择，欠的锻造等它先出来
            forgeAt = Time.unscaledTime + IdleDelay;
            Apply();
        }

        // ---- 列出已选 ----
        public static string Summary()
        {
            string head = profile != null ? $"海克斯强化 · {profile.Name}" : "海克斯强化";
            if (run.Picks.Count == 0) return head + "：还没有选择。";
            var lines = run.Picks.Select(c => $"{c.Name}（{TierName(c.Tier)}）：{string.Join("；", c.Lines)}").ToList();
            if (run.InfiniteBonus > 0) lines.Add($"无限循环往复：击杀已累计技能急速 +{run.InfiniteBonus}");
            if (run.Forges.Count > 0) lines.Add($"还有 {run.Forges.Count} 次属性锻造，休息时打开");
            lines.AddRange(run.EnemyPicks.Select(c => $"<color=#FF6060>敌人·{c.Name}：{string.Join("；", c.Lines)}</color>"));
            return head + "\n" + string.Join("\n", lines);
        }
    }
}
