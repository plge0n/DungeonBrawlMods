using System.Collections.Generic;
using System.Linq;
using DBModKit;

namespace DBStudio
{
    // 强化（类似海克斯）：在三选一界面里选到后，立刻执行"获得时"的效果，并让它专属的规则开始生效（本局有效）
    internal sealed class Augment
    {
        public string Id = Rule.NewId();
        public string Name = "新强化";
        // 留空时按效果自动生成
        public string Description = "";
        // 图标的 Resources 路径
        public string Icon = "";
        // 0~4 = D、C、B、A、S；-1 = 不上色
        public int Quality = 2;
        public bool Enabled = true;
        // 默认必须是空列表：Newtonsoft 读档时会往已有的列表里追加
        public List<RuleAction> Effects = new List<RuleAction>();
        // 选到之后才生效的规则（Rule.Id）
        public List<string> Rules = new List<string>();
    }

    // 什么时候弹出强化选择
    internal sealed class AugmentOffer
    {
        public bool Enabled = true;
        // 每隔几个波次结束弹一次
        public int EveryWaves = 3;
        public int Choices = 3;
        // 同一局里选过的不再出现
        public bool NoRepeat = true;
    }

    internal static class AugmentText
    {
        public static readonly string[] QualityLabels = { "D", "C", "B", "A", "S", "不上色" };

        public static int QualityIndex(int quality) => quality >= 0 && quality <= 4 ? quality : 5;


        public static int QualityFromIndex(int index) => index >= 0 && index <= 4 ? index : -1;

        // 三选一卡片上的描述：自己写的优先，否则按效果和规则生成
        public static string Description(Augment a)
        {
            if (!string.IsNullOrWhiteSpace(a.Description)) return a.Description;
            var parts = new List<string>();
            if (a.Effects.Count > 0) parts.Add("获得时：" + string.Join("，", a.Effects.Select(RuleText.ActionText)) + "。");
            foreach (var id in a.Rules)
            {
                var rule = Packs.Current.Rules.FirstOrDefault(r => r.Id == id);
                if (rule != null) parts.Add(RuleText.Sentence(rule) + "。");
            }
            return parts.Count == 0 ? "（没有效果）" : string.Join("\n", parts);
        }
    }

    internal static class AugmentTemplates
    {
        // 模板会顺带建好它专属的规则
        public static readonly (string label, System.Func<(Augment, Rule[])> make)[] All =
        {
            ("空白强化", () => (new Augment { Icon = DefaultIcon() }, new Rule[0])),
            ("力量涌动（加力量）", () => (new Augment
            {
                Name = "力量涌动",
                Quality = 1,
                Icon = DefaultIcon(),
                Effects = { new RuleAction { Type = ActionType.Strength, Value = 30, Seconds = 0 } },
            }, new Rule[0])),
            ("点金手（给金币）", () => (new Augment
            {
                Name = "点金手",
                Quality = 0,
                Icon = DefaultIcon(),
                Effects = { new RuleAction { Type = ActionType.Gold, Value = 300 } },
            }, new Rule[0])),
            ("嗜血（击杀回血）", () =>
            {
                var rule = new Rule
                {
                    Name = "嗜血",
                    Trigger = Trigger.EnemyKilled,
                    Actions = { new RuleAction { Type = ActionType.HealPercent, Value = 2 } },
                    Cooldown = 0.3,
                };
                return (new Augment { Name = "嗜血", Quality = 2, Icon = DefaultIcon(), Rules = { rule.Id } }, new[] { rule });
            }),
            ("背水一战（残血加攻击）", () =>
            {
                var rule = new Rule
                {
                    Name = "背水一战",
                    Trigger = Trigger.Damaged,
                    Conditions = { new Condition { Type = ConditionType.HpBelow, Value = 30 } },
                    Actions = { new RuleAction { Type = ActionType.Attack, Value = 50, Seconds = 5 } },
                    Cooldown = 10,
                };
                return (new Augment { Name = "背水一战", Quality = 3, Icon = DefaultIcon(), Rules = { rule.Id } }, new[] { rule });
            }),
        };

        private static string DefaultIcon() => RelicInfo.All.FirstOrDefault(r => r.Icon != null)?.IconPath ?? "";
    }

    // 波次结束时弹出强化选择；记住这一局选过哪些
    internal static class AugmentEngine
    {
        // 等游戏自己的遗物选择先弹出来，界面空闲这么久之后才弹
        private const float IdleDelay = 1.5f;

        private static readonly HashSet<string> picked = new HashSet<string>();
        private static int wavesEnded;
        private static bool pending;
        private static float openAt;

        public static IReadOnlyCollection<string> Picked => picked;

        public static void Attach(ModContext mod)
        {
            mod.OnGameEvent(OnEvent);
            mod.OnUpdate(Tick);
            mod.OnBattleStarted(Reset);
            mod.OnBattleEnded(Reset);
        }

        // 规则属于某个强化，并且这一局还没选到它
        public static bool IsLocked(Rule rule)
        {
            bool owned = false;
            foreach (var a in Packs.Current.Augments)
            {
                if (!a.Rules.Contains(rule.Id)) continue;
                if (picked.Contains(a.Id)) return false;
                owned = true;
            }
            return owned;
        }

        public static Augment OwnerOf(Rule rule) => Packs.Current.Augments.FirstOrDefault(a => a.Rules.Contains(rule.Id));

        // 调试用：马上弹一次
        public static void OfferSoon()
        {
            pending = true;
            openAt = 0f;
        }

        private static void Reset()
        {
            picked.Clear();
            wavesEnded = 0;
            pending = false;
        }

        private static void OnEvent(GameEvent e)
        {
            if (e.Kind != GameEventKind.WaveEnded || !Packs.IsActive || !Packs.Current.Offer.Enabled) return;
            wavesEnded++;
            if (wavesEnded % System.Math.Max(1, Packs.Current.Offer.EveryWaves) != 0) return;
            pending = true;
            openAt = UnityEngine.Time.unscaledTime + IdleDelay;
        }

        private static void Tick()
        {
            if (!pending) return;
            if (!Packs.IsActive || !Game.InBattle)
            {
                pending = false;
                return;
            }
            float now = UnityEngine.Time.unscaledTime;
            // 游戏的选择界面开着就等它关掉，再多等一会儿；工作台开着会挡住三选一界面，也等
            if (ChoicePanel.IsBusy || Studio.IsOpen)
            {
                openAt = now + IdleDelay;
                return;
            }
            if (now < openAt) return;
            pending = false;
            Offer();
        }

        private static void Offer()
        {
            var settings = Packs.Current.Offer;
            var pool = Packs.Current.Augments
                .Where(a => a.Enabled && (!settings.NoRepeat || !picked.Contains(a.Id)))
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(UnityEngine.Mathf.Clamp(settings.Choices, 1, ChoicePanel.MaxOptions))
                .ToList();
            if (pool.Count == 0)
            {
                EventLog.Add("该弹出强化了，但没有可选的强化");
                return;
            }
            var options = pool.Select(a => new ChoiceOption
            {
                Name = a.Name,
                Description = AugmentText.Description(a),
                IconPath = a.Icon,
                Quality = AugmentText.QualityFromIndex(AugmentText.QualityIndex(a.Quality)),
            }).ToList();
            if (!ChoicePanel.TryShow("选择强化", options, i => Pick(pool[UnityEngine.Mathf.Clamp(i, 0, pool.Count - 1)])))
            {
                // 刚好被占用了，下一帧再试
                pending = true;
                return;
            }
            EventLog.Add($"弹出强化选择：{string.Join("、", pool.Select(a => a.Name))}");
        }

        private static void Pick(Augment a)
        {
            var p = Game.LocalPlayer;
            if (p == null || !Packs.IsActive) return;
            picked.Add(a.Id);
            foreach (var effect in a.Effects) RuleEngine.Run(effect, p);
            EventLog.Add($"选择了强化「{a.Name}」");
            ScreenNotice.Show($"获得强化「{a.Name}」", 3f);
        }
    }
}
