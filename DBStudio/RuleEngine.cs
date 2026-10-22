using System.Linq;
using System.Collections.Generic;
using DBModKit;
using UnityEngine;

namespace DBStudio
{
    // 执行玩法包里的规则。只在玩法包生效（单人）时执行；停用时撤销规则加过的属性
    internal static class RuleEngine
    {
        // 冷却、持续时间都用游戏时间：暂停时一起停
        private sealed class State
        {
            public float ReadyAt;
            public int Count;
            public float NextTick;
        }

        private sealed class StatBonus
        {
            public int Str, Agi, Sta;
            public long Attack;
            public float Until; // PositiveInfinity = 本局一直有效
            public string Rule;
        }

        private static readonly Dictionary<Rule, State> states = new Dictionary<Rule, State>();
        private static readonly List<StatBonus> bonuses = new List<StatBonus>();
        private static Player bonusOwner;

        public static void Attach(ModContext mod)
        {
            mod.OnGameEvent(OnEvent);
            mod.OnUpdate(Tick);
            mod.OnBattleStarted(Reset);
            mod.OnBattleEnded(Reset);
            mod.AddCleanup(RemoveBonuses);
        }

        // 规则被删除或改了触发方式后，丢掉它的冷却和计数
        public static void Forget(Rule rule) => states.Remove(rule);

        private static void Reset()
        {
            states.Clear();
            bonuses.Clear();
            bonusOwner = null;
        }

        private static void Tick()
        {
            var p = Game.LocalPlayer;
            if (!Packs.IsActive || p == null)
            {
                // 停用（比如有人进了房间）时把加过的属性撤掉
                if (bonuses.Count > 0) RemoveBonuses();
                return;
            }
            if (p != bonusOwner)
            {
                // 本地玩家被顶替了（没经过离开战斗），旧角色的加成不能扣到新角色上
                bonuses.Clear();
                bonusOwner = null;
            }
            float now = Time.time;
            for (int i = bonuses.Count - 1; i >= 0; i--)
            {
                if (now < bonuses[i].Until) continue;
                Plugin.RunLogger.Info("规则", $"「{bonuses[i].Rule}」的限时加成到期");
                Apply(p, bonuses[i], -1);
                bonuses.RemoveAt(i);
            }
            foreach (var rule in Packs.Current.Rules)
            {
                if (!rule.Enabled || rule.Trigger != Trigger.Interval || AugmentEngine.IsLocked(rule)) continue;
                var s = StateOf(rule);
                if (s.NextTick == 0f) s.NextTick = now + Interval(rule);
                if (now < s.NextTick) continue;
                s.NextTick = now + Interval(rule);
                TryFire(rule, p, default);
            }
        }

        private static float Interval(Rule rule) => Mathf.Max(0.2f, (float)rule.Every);

        private static void OnEvent(GameEvent e)
        {
            if (!Packs.IsActive) return;
            var p = Game.LocalPlayer;
            if (p == null) return;
            foreach (var rule in Packs.Current.Rules.ToArray())
                if (rule.Enabled && Matches(rule.Trigger, e) && !AugmentEngine.IsLocked(rule))
                    TryFire(rule, p, e);
        }

        private static bool Matches(Trigger t, GameEvent e)
        {
            switch (t)
            {
                case Trigger.EnemyKilled: return e.Kind == GameEventKind.EnemyKilled;
                case Trigger.EliteKilled: return e.Kind == GameEventKind.EnemyKilled && (e.IsElite || e.IsBoss);
                case Trigger.BossKilled: return e.Kind == GameEventKind.EnemyKilled && e.IsBoss;
                case Trigger.SkillCast: return e.Kind == GameEventKind.SkillCast;
                case Trigger.Critical: return e.Kind == GameEventKind.Critical;
                case Trigger.Dodged: return e.Kind == GameEventKind.Dodged;
                case Trigger.Damaged: return e.Kind == GameEventKind.Damaged;
                case Trigger.LevelUp: return e.Kind == GameEventKind.LevelUp;
                case Trigger.Revived: return e.Kind == GameEventKind.Revived;
                case Trigger.ItemPicked: return e.Kind == GameEventKind.ItemPicked;
                case Trigger.WaveStarted: return e.Kind == GameEventKind.WaveStarted;
                case Trigger.WaveEnded: return e.Kind == GameEventKind.WaveEnded;
                case Trigger.BossAppeared: return e.Kind == GameEventKind.BossAppeared;
                default: return false;
            }
        }

        private static void TryFire(Rule rule, Player p, GameEvent e)
        {
            // 死了就别回血、加属性了（复活事件除外，那时已经活了）
            if (!p.IsAlive)
            {
                Skipped(rule, "角色已死亡");
                return;
            }
            var s = StateOf(rule);
            float now = Time.time;
            if (now < s.ReadyAt)
            {
                Skipped(rule, $"冷却中，还剩 {s.ReadyAt - now:0.0} 秒");
                return;
            }
            if (rule.MaxPerBattle > 0 && s.Count >= rule.MaxPerBattle)
            {
                Skipped(rule, $"本局已触发 {s.Count} 次，达到上限");
                return;
            }
            foreach (var c in rule.Conditions)
                if (!Check(c, p))
                {
                    Skipped(rule, "条件不满足：" + Describe(c, p));
                    return;
                }

            s.ReadyAt = now + (float)rule.Cooldown;
            s.Count++;
            // 先记触发，动作的效果（回血前后的血量等）由 ModKit 紧接着记录
            Plugin.RunLogger.Info("规则", $"规则「{rule.Name}」触发",
                ("触发", rule.Trigger.ToString()), ("事件", e.Kind == default && rule.Trigger == Trigger.Interval ? "定时" : e.Kind.ToString()),
                ("条件", rule.Conditions.Count == 0 ? "无" : string.Join("；", rule.Conditions.Select(c => Describe(c, p)))),
                ("本局第", s.Count));
            foreach (var a in rule.Actions) Run(a, p, rule.Name);
        }

        // 没触发的原因记成 Debug（调试页勾"详细"能看到）
        private static void Skipped(Rule rule, string reason) => Plugin.RunLogger.Debug("规则", $"规则「{rule.Name}」没触发", ("原因", reason));

        // 条件和当前值，例如"HpBelow 30（当前 20.4）"
        private static string Describe(Condition c, Player p)
        {
            double? now;
            switch (c.Type)
            {
                case ConditionType.HpBelow:
                case ConditionType.HpAbove: now = p.HpPercent * 100.0; break;
                case ConditionType.ManaBelow:
                case ConditionType.ManaAbove: now = ManaPercent(p); break;
                case ConditionType.EnemiesNear: now = p.EnemiesWithin(RuleText.NearRange).Count; break;
                case ConditionType.WaveAtLeast: now = Game.Wave; break;
                default: now = null; break;
            }
            return $"{c.Type} {c.Value:0.#}" + (now.HasValue ? $"（当前 {now.Value:0.#}）" : "");
        }

        private static bool Check(Condition c, Player p)
        {
            double v = c.Value;
            switch (c.Type)
            {
                case ConditionType.HpBelow: return p.HpPercent * 100.0 <= v;
                case ConditionType.HpAbove: return p.HpPercent * 100.0 >= v;
                case ConditionType.ManaBelow: return ManaPercent(p) <= v;
                case ConditionType.ManaAbove: return ManaPercent(p) >= v;
                case ConditionType.EnemiesNear: return p.EnemiesWithin(RuleText.NearRange).Count >= v;
                case ConditionType.WaveAtLeast: return Game.Wave >= v;
                case ConditionType.Chance: return Random.value * 100.0 < v;
                default: return true;
            }
        }

        private static double ManaPercent(Player p) => p.MaxMana > 0 ? p.Mana * 100.0 / p.MaxMana : 0.0;

        // 强化也用它执行"获得时"的效果
        internal static void Run(RuleAction a, Player p, string source)
        {
            double v = System.Math.Max(0.0, a.Value);
            switch (a.Type)
            {
                case ActionType.HealPercent: p.Heal(p.MaxHp * v / 100.0); break;
                case ActionType.Heal: p.Heal(v); break;
                case ActionType.Mana: p.AddMana(ToInt(v)); break;
                case ActionType.Gold: p.AddGold(ToInt(v)); break;
                case ActionType.Relic:
                    // 发没发出由 ModKit 记录
                    p.GiveRelic(ToInt(v));
                    break;
                case ActionType.Notice:
                    if (!string.IsNullOrEmpty(a.Text)) ScreenNotice.Show(a.Text, 3f);
                    break;
                default:
                    AddBonus(p, a.Type, ToInt(v), a.Seconds, source);
                    break;
            }
        }

        private static void AddBonus(Player p, ActionType type, int v, double seconds, string source)
        {
            if (v == 0) return;
            if (bonusOwner != p)
            {
                // 换了角色（新的一局），旧角色上的加成随角色一起没了
                bonuses.Clear();
                bonusOwner = p;
            }
            var b = new StatBonus { Until = seconds > 0 ? Time.time + (float)seconds : float.PositiveInfinity, Rule = source };
            Plugin.RunLogger.Info("规则", seconds > 0 ? $"「{source}」限时加成 {seconds:0.#} 秒" : $"「{source}」本局加成", ("属性", type.ToString()), ("数值", v));
            switch (type)
            {
                case ActionType.Attack: b.Attack = v; break;
                case ActionType.Strength: b.Str = v; break;
                case ActionType.Agility: b.Agi = v; break;
                case ActionType.Stamina: b.Sta = v; break;
            }
            Apply(p, b, 1);
            bonuses.Add(b);
        }

        private static void RemoveBonuses()
        {
            var p = Game.LocalPlayer;
            if (p != null && p == bonusOwner)
                foreach (var b in bonuses) Safe(() => Apply(p, b, -1));
            bonuses.Clear();
            bonusOwner = null;
        }

        private static void Apply(Player p, StatBonus b, int sign) => p.AddStats(sign * b.Str, sign * b.Agi, sign * b.Sta, sign * b.Attack);

        private static State StateOf(Rule rule)
        {
            if (!states.TryGetValue(rule, out var s)) states[rule] = s = new State();
            return s;
        }

        private static int ToInt(double v) => (int)System.Math.Round(System.Math.Min(int.MaxValue, v));

        private static void Safe(System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"撤销规则加成失败：{e}");
            }
        }
    }
}
