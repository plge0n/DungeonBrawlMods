using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;

namespace Hextech
{
    // 一局里的海克斯：什么时候弹、选中后怎么生效、释放时的触发效果、重试波次时的存取
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

            public Run Clone()
            {
                var r = (Run)MemberwiseClone();
                r.Picks = new List<Card>(Picks);
                r.Shown = new List<string>(Shown);
                return r;
            }
        }

        private static ModContext mod;
        private static Run run = new Run();
        private static Run checkpoint;
        private static SkillProfile profile;
        private static HeroSkill heroSkill;
        // 专属技能暂不支持时记下来，在开局那次本该弹出的时候提示一次
        private static HeroSkill unsupported;
        private static bool disabled;
        private static List<int> offerWaves;
        private static float openAt, battleStart;
        private static bool sawGameChoice, waveStarted;
        private static float[] appliedPassive;

        public static IReadOnlyList<Card> Picks => run.Picks;
        public static SkillProfile Profile => profile;
        public static HeroSkill HeroSkill => heroSkill;
        public static bool Active => Plugin.Enabled.Value && !disabled && profile != null && Game.IsSolo;

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
            });
            mod.OnGameEvent(OnEvent);
            mod.OnSkillCast(Triggers.OnCast);
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
            unsupported = null;
            disabled = false;
            offerWaves = null;
            appliedPassive = null;
            battleStart = Time.unscaledTime;
            openAt = battleStart + IdleDelay;
            sawGameChoice = waveStarted = false;
            mod.ClearSkillTunes();
            Triggers.Clear(revert: false);
        }

        private static void Tick()
        {
            var player = Game.LocalPlayer;
            if (player == null) return;
            if (!Plugin.Enabled.Value)
            {
                if (run.Picks.Count > 0 || appliedPassive != null) RevertAll();
                return;
            }
            if (disabled) return;
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
            if (profile == null && !BuildProfile(player))
            {
                TickUnsupportedNotice(player);
                return;
            }

            Triggers.Tick(player);
            TickOffer();
        }

        private static bool BuildProfile(Player player)
        {
            var hs = player.HeroSkill;
            if (hs == null) return false;
            if (!SkillData.Supported(hs.IsActive, hs.SkillId))
            {
                if (unsupported == null)
                {
                    unsupported = hs;
                    mod.RunLog.Info("英雄", "这个英雄的专属技能暂不支持", ("技能", hs.Name), ("编号", hs.SkillId), ("主动", hs.IsActive));
                }
                return false;
            }
            heroSkill = hs;
            profile = hs.IsActive
                ? SkillData.Active(hs.SkillId, hs.Name, hs.Cooldown, hs.Cost, hs.Duration, hs.Charges)
                : SkillData.Passive(hs.SkillId, hs.Name, hs.PassiveValues.ToArray());
            mod.RunLog.Info("英雄", "专属技能", ("技能", hs.Name), ("编号", hs.SkillId), ("主动", hs.IsActive),
                ("可用积木", string.Join(" ", Generator.Candidates(profile).Select(b => b.Word))));
            Apply();
            return true;
        }

        // ---- 出现时机 ----
        private static void OnEvent(GameEvent e)
        {
            if (e.Kind == GameEventKind.WaveStarted) waveStarted = true;
            if (e.Kind != GameEventKind.WaveEnded) return;
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

        // 不支持的英雄：在开局海克斯本该弹出的时候提示一次，免得以为 Mod 没装好
        private static void TickUnsupportedNotice(Player player)
        {
            if (unsupported == null || !Due()) return;
            run.Pending = false;
            ScreenNotice.Show($"海克斯强化：{player.HeroName}的专属技能「{unsupported.Name}」暂不支持，本局不会出现海克斯三选一。", 8f);
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

        private static void Offer()
        {
            if (profile == null || run.Round >= Rounds) return;
            // 品质只由种子和回合决定，刷新不改品质；卡由种子、回合、刷新次数决定，重试波次后弹出来的和原来一样
            var tier = TierFor(run.Round, new System.Random(run.Seed ^ (run.Round * 7919)));
            var rng = new System.Random(run.Seed ^ (run.Round * 7919) ^ (run.RefreshIndex * 104729 + 1));
            var cards = Generator.Offer(profile, tier, 3, rng, run.Shown);
            if (cards.Count == 0)
            {
                mod.RunLog.Warning("三选一", "没有生成出强化", ("技能", profile.Name), ("品质", TierName(tier)));
                run.Round++;
                return;
            }
            var options = cards.Select(c => new ChoiceOption
            {
                Name = c.Name,
                Description = string.Join("\n", c.Lines),
                IconPath = heroSkill?.IconPath ?? "",
                Quality = QualityColor(tier),
            }).ToList();
            bool canRefresh = run.RefreshLeft > 0;
            if (canRefresh)
                options.Add(new ChoiceOption
                {
                    Name = $"刷新（还剩 {run.RefreshLeft} 次）",
                    Description = "换一批同品质的强化",
                    IconPath = heroSkill?.IconPath ?? "",
                });
            string title = $"海克斯强化 · {profile.Name}（{TierName(tier)}，第 {run.Round + 1}/{Rounds} 次）";
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
            Apply();
            ScreenNotice.Show($"获得海克斯强化「{card.Name}」", 3f);
        }

        // ---- 生效 ----
        private static void Apply()
        {
            var player = Game.LocalPlayer;
            if (profile == null || player == null) return;
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
            Apply();
        }

        // ---- 列出已选 ----
        public static string Summary()
        {
            if (profile == null) return "海克斯强化：这个英雄的专属技能暂不支持。";
            if (run.Picks.Count == 0) return $"海克斯强化 · {profile.Name}：还没有选择。";
            var lines = run.Picks.Select(c => $"{c.Name}（{TierName(c.Tier)}）：{string.Join("；", c.Lines)}");
            return $"海克斯强化 · {profile.Name}\n" + string.Join("\n", lines);
        }
    }
}
