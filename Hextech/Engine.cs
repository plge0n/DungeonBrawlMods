using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;

namespace Hextech
{
    // 一局里的海克斯：什么时候弹、选中后怎么生效、释放时和击杀时的触发效果、休息时打开锻造、敌人海克斯、重试波次时的存取
    internal static class Engine
    {
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
        private static bool started, disabled;
        private static List<int> offerWaves;
        private static float openAt, battleStart, forgeAt;
        private static bool sawGameChoice, waveStarted, inWave;
        private static float[] appliedPassive;
        private static bool splashSet;

        public static IReadOnlyList<Card> Picks => run.Picks;
        public static IReadOnlyList<Card> EnemyPicks => run.EnemyPicks;
        public static SkillProfile Profile => profile;
        public static HeroSkill HeroSkill => heroSkill;
        public static bool Active => Plugin.Enabled.Value && !disabled && started && Game.IsSolo;

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
                ClearState();
            });
            mod.OnGameEvent(OnEvent);
            mod.OnSkillCast(Triggers.OnCast);
            mod.OnSkillCast(OnAnyCast);
            mod.OnHit(Triggers.OnHit);
            mod.OnCheckpointSaved(() => checkpoint = run.Clone());
            mod.OnCheckpointRestored(Restore);
            mod.OnUpdate(Tick);
        }

        private static void Reset()
        {
            run = new Run { Seed = UnityEngine.Random.Range(1, int.MaxValue), RefreshLeft = Plugin.Refreshes.Value, Pending = true };
            checkpoint = null;
            profile = null;
            heroSkill = null;
            started = disabled = false;
            offerWaves = null;
            appliedPassive = null;
            battleStart = Time.unscaledTime;
            openAt = battleStart + IdleDelay;
            forgeAt = 0f;
            sawGameChoice = waveStarted = inWave = false;
            mod.ClearSkillTunes();
            ClearState();
            Triggers.Clear(revert: false);
        }

        // 持续状态（溅射、怪物倍率）清空：只改 ModKit 里的设置，不碰英雄
        private static void ClearState()
        {
            if (splashSet) mod.SetSoulSplash(null);
            splashSet = false;
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
            if (!Game.IsSolo)
            {
                // 有人进了房间：本局停用，已选的强化全部撤销
                disabled = true;
                RevertAll();
                mod.RunLog.Info("房间", "有其他玩家，本局停用");
                if (run.Picks.Count > 0) ScreenNotice.Show("海克斯强化只在单人时生效：房间里有其他玩家，本局已撤销所有强化。", 6f);
                return;
            }
            if (ChoicePanel.IsBusy) sawGameChoice = true;
            if (!started)
            {
                // 刚进战斗时英雄可能还没生成完，等一会儿；3 秒后还查不到专属技能就只抽通用卡
                if (player.HeroSkill == null && Time.unscaledTime - battleStart < 3f) return;
                Start(player);
            }

            Triggers.Tick(player);
            TickOffer();
            TickForge();
            TickMana(player);
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
            if (e.Kind == GameEventKind.WaveStarted)
            {
                waveStarted = inWave = true;
                run.WaveForges = 0;
            }
            if (e.Kind != GameEventKind.WaveEnded) return;
            inWave = false;
            forgeAt = Time.unscaledTime + IdleDelay;
            // 休息阶段：限时加成提前结束（游戏随后拍重试快照，快照里不能带着加成）
            Triggers.Clear(revert: true);
            if (run.Round >= Rounds || run.Round == 0) return;
            if (OfferWaves().Contains(Game.Wave))
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
            if (!Due()) return;
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

        public static string IconFor(Card card) => card.Augment?.IconPath ?? heroSkill?.IconPath ?? "Bundles/UI/Icon/Shop/forging_epic";

        // 一次三张：有专属卡的英雄"专属 2 + 通用 1"（招牌卡有一半机会占一个专属位），其他英雄 3 张通用卡
        private static List<Card> Draw(Tier tier, System.Random rng)
        {
            var cards = new List<Card>();
            if (profile != null)
            {
                var ids = SkillIds().ToList();
                var signature = Augments.All.Where(a => a.Pool == AugmentPool.Signature && a.Tier == tier && ids.Contains(a.SkillId)
                    && !Picked(a.Id) && !run.Shown.Contains("a:" + a.Id)).ToList();
                if (signature.Count > 0 && rng.NextDouble() < 0.5) cards.Add(signature[rng.Next(signature.Count)].ToCard());
                cards.AddRange(Generator.Offer(profile, tier, 2 - cards.Count, rng, run.Shown));
            }
            int general = 3 - cards.Count;
            var pool = Augments.All.Where(a => a.Pool == AugmentPool.General && a.Tier == tier && !Picked(a.Id)
                && (a.Requires == null || a.Requires.Any(Picked))).ToList();
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
            if (Plugin.EnemyAugments.Value && run.BonusRound != run.Round)
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
            string enemy = Plugin.EnemyAugments.Value ? "，敌人也会拿一张" : "";
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
            if (Plugin.EnemyAugments.Value)
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
            ScreenNotice.Show(notice, 4f);
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

        // ---- 法力流（由心及物、魔宗、左脚踩右脚）：每秒按当前的蓝、血算目标值，只补差额 ----
        private static float manaAt;

        private static void OnAnyCast(SkillCast cast)
        {
            if (cast.IsFree || !Active || !Picked("manamune") || run.ManamuneStacks >= Augments.ManamuneMaxStacks) return;
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
            bool bonk = Picked("bonk");
            if (bonk != splashSet)
            {
                mod.SetSoulSplash(bonk ? new SoulSplash { Radius = 3.5f, Ratio = 0.6f } : null);
                splashSet = bonk;
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
        public static void Unload() => RevertAll();

        private static void RevertAll()
        {
            var me = Game.LocalPlayer;
            if (me != null)
            {
                foreach (var c in run.Picks) c.Augment?.Revert?.Invoke(me);
                if (run.InfiniteBonus > 0) me.AddAttribute("CoolDown", -run.InfiniteBonus);
                RevertMana(me);
            }
            run.InfiniteBonus = 0;
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
