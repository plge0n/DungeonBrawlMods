using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBModKit
{
    // 内容包的内置效果库：JSON 里的效果编译成 EffectAction，由 ContentActiveSkill / ContentPassive 在各个触发点执行

    // 执行效果的技能（主动技能的一次释放，或技能栏里的一个被动）
    internal interface IEffectHost
    {
        /// <summary>本机的拥有者；别人放的技能为 null（这时只播表现，不结算）。</summary>
        Player Me { get; }
        string Name { get; }
        int Level { get; }
        float Value(int i);
        /// <summary>施法者 / 拥有者的位置。</summary>
        Vector3 SelfPosition { get; }
        /// <summary>技能的作用半径；被动为 0。</summary>
        float Radius { get; }
        /// <summary>技能表伤害公式的值；被动为 0。</summary>
        long SkillDamage { get; }
        int DamageArea(Vector3 center, float radius, long amount);
        long DamageTarget(Role target, long amount);
        void Effect(string path, Vector3 position, float duration, float scale);
        bool Animate(string clip, float speed, float? duration);
        /// <summary>登记"跟着技能走"的撤销：主动技能结束、被动失去时执行。</summary>
        void Hold(Action revert);
        /// <summary>效果的内部冷却：效果 → 下次可用的时间。</summary>
        Dictionary<EffectAction, float> Cooldowns { get; }
    }

    // 一次触发的上下文
    internal sealed class EffectScope
    {
        public IEffectHost Host;
        public Player Me => Host.Me;
        /// <summary>表达式里的 damage：命中的伤害、受伤量；主动技能里是技能公式伤害。</summary>
        public double Damage;
        public Role Target;
        public Vector3 Position;
    }

    internal sealed class EffectAction
    {
        // 出错时日志里的位置，例如 "雷霆套装/skills.json activeSkills[0].onCast[1]"
        public string Where = "";
        // 内容包的名字：运行记录的来源
        public string Source;
        public ContentExpr Chance;
        public ContentExpr HpBelow;
        public bool? IsBoss;
        public float Cooldown;
        public Action<EffectScope> Run;

        public void Execute(EffectScope s)
        {
            if (Chance != null && UnityEngine.Random.value * 100f >= Chance(s)) return;
            if (HpBelow != null && (s.Me == null || s.Me.HpPercent * 100f > HpBelow(s))) return;
            if (IsBoss.HasValue && (s.Target == null || s.Target.IsBoss != IsBoss.Value)) return;
            if (Cooldown > 0f)
            {
                if (s.Host.Cooldowns.TryGetValue(this, out float ready) && Time.time < ready) return;
                s.Host.Cooldowns[this] = Time.time + Cooldown;
            }
            string previous = RunLog.CallerOverride;
            RunLog.CallerOverride = Source;
            try
            {
                Safe.Run(Where, () => Run(s));
            }
            finally
            {
                RunLog.CallerOverride = previous;
            }
        }

        public static void RunAll(List<EffectAction> actions, EffectScope s)
        {
            if (actions == null) return;
            foreach (var a in actions) a.Execute(s);
        }
    }

    // 限时效果到时间撤销。离开战斗时丢掉：下一局英雄重新生成，属性本来就清零了
    internal static class ContentTimers
    {
        private static readonly List<(float at, Action action)> pending = new List<(float, Action)>();
        private static bool hooked;

        public static void After(float seconds, Action action)
        {
            if (!hooked)
            {
                hooked = true;
                ModEvents.Update += Tick;
                ModEvents.BattleEnded += pending.Clear;
            }
            pending.Add((Time.time + seconds, action));
        }

        private static void Tick()
        {
            if (pending.Count == 0) return;
            float now = Time.time;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].at > now) continue;
                var action = pending[i].action;
                pending.RemoveAt(i);
                Safe.Run("内容包：限时效果到期", action);
            }
        }
    }

    internal sealed class ActiveSpec
    {
        public List<EffectAction> OnCast = new List<EffectAction>();
        public List<EffectAction> OnEnd = new List<EffectAction>();
        public float TickEvery;
        public List<EffectAction> OnTick = new List<EffectAction>();
        public string Animation;
        public float AnimationSpeed = 1f;
        public float? AnimationDuration;
    }

    internal sealed class ContentActiveSkill : ActiveSkill, IEffectHost
    {
        private readonly ActiveSpec spec;
        private readonly List<Action> held = new List<Action>();
        private float tickTimer;

        public ContentActiveSkill(ActiveSpec spec) => this.spec = spec;

        public Player Me => IsMine ? Game.LocalPlayer : null;
        public string Name => Info?.Key ?? "";
        public int Level => 0;
        public Vector3 SelfPosition => Caster?.Position ?? Position;
        public long SkillDamage => FormulaDamage;
        public Dictionary<EffectAction, float> Cooldowns { get; } = new Dictionary<EffectAction, float>();

        int IEffectHost.DamageArea(Vector3 center, float radius, long amount) => DamageArea(center, radius, amount);
        long IEffectHost.DamageTarget(Role target, long amount) => Damage(target, amount);
        void IEffectHost.Effect(string path, Vector3 position, float duration, float scale) => Effect(path, position, duration, scale);
        public bool Animate(string clip, float speed, float? duration) => PlayAnimation(clip, speed, duration);
        public void Hold(Action revert) => held.Add(revert);

        private EffectScope Scope() => new EffectScope { Host = this, Damage = FormulaDamage, Target = Target, Position = Position };

        protected internal override void OnCast()
        {
            if (spec.Animation != null) Animate(spec.Animation, spec.AnimationSpeed, spec.AnimationDuration);
            EffectAction.RunAll(spec.OnCast, Scope());
        }

        protected internal override void OnUpdate(float deltaTime)
        {
            if (spec.TickEvery <= 0f || spec.OnTick.Count == 0) return;
            tickTimer += deltaTime;
            while (tickTimer >= spec.TickEvery)
            {
                tickTimer -= spec.TickEvery;
                EffectAction.RunAll(spec.OnTick, Scope());
            }
        }

        protected internal override void OnEnd()
        {
            EffectAction.RunAll(spec.OnEnd, Scope());
            foreach (var revert in held) Safe.Run($"{Name} 撤销", revert);
            held.Clear();
        }
    }

    // 内容包的遗物：触发器和被动一样。升级时撤销"跟着遗物走"的加成，再按新等级重新执行 onAdded
    internal sealed class ContentRelic : RelicBehaviour, IEffectHost
    {
        private readonly PassiveSpec spec;
        private readonly List<Action> held = new List<Action>();
        private float tickTimer;

        public ContentRelic(PassiveSpec spec) => this.spec = spec;

        // 遗物只在拥有者的客户端上运行
        public Player Me => Owner;
        public string Name => Info?.Key ?? "";
        int IEffectHost.Level => Level;
        float IEffectHost.Value(int i) => Value(i);
        public Vector3 SelfPosition => Owner?.Position ?? Vector3.zero;
        public float Radius => 0f;
        public long SkillDamage => 0;
        public Dictionary<EffectAction, float> Cooldowns { get; } = new Dictionary<EffectAction, float>();

        // 读包时已经拒绝了 damage、animation
        public int DamageArea(Vector3 center, float radius, long amount) => 0;
        public long DamageTarget(Role target, long amount) => 0;
        public void Effect(string path, Vector3 position, float duration, float scale)
            => Safe.Run($"{Name}.Effect({path})", () => GameRaw.SpawnEffect(path, position, duration, scale));
        public bool Animate(string clip, float speed, float? duration) => false;
        public void Hold(Action revert) => held.Add(revert);

        private void Fire(PassiveTrigger trigger, double damage = 0, Role target = null)
        {
            if (!spec.On.TryGetValue(trigger, out var actions)) return;
            EffectAction.RunAll(actions, new EffectScope { Host = this, Damage = damage, Target = target, Position = target?.Position ?? SelfPosition });
        }

        private void Release()
        {
            foreach (var revert in held) Safe.Run($"{Name} 撤销", revert);
            held.Clear();
        }

        protected internal override void OnAdded() => Fire(PassiveTrigger.OnAdded);

        protected internal override void OnRemoved()
        {
            Fire(PassiveTrigger.OnRemoved);
            Release();
        }

        protected internal override void OnLevelChanged(int oldLevel, int newLevel)
        {
            Release();
            Fire(PassiveTrigger.OnAdded);
        }

        protected internal override void OnUpdate()
        {
            if (spec.TickEvery <= 0f) return;
            tickTimer += Time.deltaTime;
            if (tickTimer < spec.TickEvery) return;
            tickTimer = 0f;
            Fire(PassiveTrigger.OnTick);
        }

        protected internal override void OnHit(HitInfo hit)
        {
            if (hit.Damage > 0) Fire(PassiveTrigger.OnHit, hit.Damage, hit.Target);
        }

        protected internal override void OnGameEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.EnemyKilled: Fire(PassiveTrigger.OnKill, 0, e.Target); break;
                case GameEventKind.Critical: Fire(PassiveTrigger.OnCrit, e.Amount, e.Target); break;
                case GameEventKind.Damaged: Fire(PassiveTrigger.OnHurt, e.Amount); break;
                case GameEventKind.WaveStarted: Fire(PassiveTrigger.OnWaveStart); break;
                case GameEventKind.SkillCast: Fire(PassiveTrigger.OnCast); break;
            }
        }
    }

    internal sealed class ContentEquipEffect : EquipEffectBehaviour, IEffectHost
    {
        private readonly PassiveSpec spec;
        private readonly List<Action> held = new List<Action>();
        private float tickTimer;

        public ContentEquipEffect(PassiveSpec spec) => this.spec = spec;

        // 装备特效只在拥有者的客户端上运行；强化时和遗物升级一样，撤销 onAdded 的加成再按新等级执行一次
        public Player Me => Owner;
        public string Name => Info?.Key ?? "";
        int IEffectHost.Level => Level;
        float IEffectHost.Value(int i) => Value(i);
        public Vector3 SelfPosition => Owner?.Position ?? Vector3.zero;
        public float Radius => 0f;
        public long SkillDamage => 0;
        public Dictionary<EffectAction, float> Cooldowns { get; } = new Dictionary<EffectAction, float>();

        // 读包时已经拒绝了 damage、animation
        public int DamageArea(Vector3 center, float radius, long amount) => 0;
        public long DamageTarget(Role target, long amount) => 0;
        public void Effect(string path, Vector3 position, float duration, float scale)
            => Safe.Run($"{Name}.Effect({path})", () => GameRaw.SpawnEffect(path, position, duration, scale));
        public bool Animate(string clip, float speed, float? duration) => false;
        public void Hold(Action revert) => held.Add(revert);

        private void Fire(PassiveTrigger trigger, double damage = 0, Role target = null)
        {
            if (!spec.On.TryGetValue(trigger, out var actions)) return;
            EffectAction.RunAll(actions, new EffectScope { Host = this, Damage = damage, Target = target, Position = target?.Position ?? SelfPosition });
        }

        private void Release()
        {
            foreach (var revert in held) Safe.Run($"{Name} 撤销", revert);
            held.Clear();
        }

        protected internal override void OnAdded() => Fire(PassiveTrigger.OnAdded);

        protected internal override void OnRemoved()
        {
            Fire(PassiveTrigger.OnRemoved);
            Release();
        }

        protected internal override void OnLevelChanged(int oldLevel, int newLevel)
        {
            Release();
            Fire(PassiveTrigger.OnAdded);
        }

        protected internal override void OnUpdate()
        {
            if (spec.TickEvery <= 0f) return;
            tickTimer += Time.deltaTime;
            if (tickTimer < spec.TickEvery) return;
            tickTimer = 0f;
            Fire(PassiveTrigger.OnTick);
        }

        protected internal override void OnHit(HitInfo hit)
        {
            if (hit.Damage > 0) Fire(PassiveTrigger.OnHit, hit.Damage, hit.Target);
        }

        protected internal override void OnGameEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.EnemyKilled: Fire(PassiveTrigger.OnKill, 0, e.Target); break;
                case GameEventKind.Critical: Fire(PassiveTrigger.OnCrit, e.Amount, e.Target); break;
                case GameEventKind.Damaged: Fire(PassiveTrigger.OnHurt, e.Amount); break;
                case GameEventKind.WaveStarted: Fire(PassiveTrigger.OnWaveStart); break;
                case GameEventKind.SkillCast: Fire(PassiveTrigger.OnCast); break;
            }
        }
    }

    internal enum PassiveTrigger
    {
        OnAdded,
        OnRemoved,
        OnHit,
        OnKill,
        OnCrit,
        OnHurt,
        OnWaveStart,
        OnCast,
        OnTick,
    }

    internal sealed class PassiveSpec
    {
        public Dictionary<PassiveTrigger, List<EffectAction>> On = new Dictionary<PassiveTrigger, List<EffectAction>>();
        public float TickEvery;
    }

    internal sealed class ContentPassive : PassiveSkill, IEffectHost
    {
        private readonly PassiveSpec spec;
        private readonly List<Action> held = new List<Action>();
        private float tickTimer;

        public ContentPassive(PassiveSpec spec) => this.spec = spec;

        // 被动只在拥有者的客户端上运行
        public Player Me => Owner;
        public string Name => Info?.Key ?? "";
        int IEffectHost.Level => Level;
        float IEffectHost.Value(int i) => Value(i);
        public Vector3 SelfPosition => Owner?.Position ?? Vector3.zero;
        public float Radius => 0f;
        public long SkillDamage => 0;
        public Dictionary<EffectAction, float> Cooldowns { get; } = new Dictionary<EffectAction, float>();

        // 读包时已经拒绝了被动里的 damage、animation
        public int DamageArea(Vector3 center, float radius, long amount) => 0;
        public long DamageTarget(Role target, long amount) => 0;
        public void Effect(string path, Vector3 position, float duration, float scale)
            => Safe.Run($"{Name}.Effect({path})", () => GameRaw.SpawnEffect(path, position, duration, scale));
        public bool Animate(string clip, float speed, float? duration) => false;
        public void Hold(Action revert) => held.Add(revert);

        private void Fire(PassiveTrigger trigger, double damage = 0, Role target = null)
        {
            if (!spec.On.TryGetValue(trigger, out var actions)) return;
            EffectAction.RunAll(actions, new EffectScope { Host = this, Damage = damage, Target = target, Position = target?.Position ?? SelfPosition });
        }

        protected internal override void OnAdded() => Fire(PassiveTrigger.OnAdded);

        protected internal override void OnRemoved()
        {
            Fire(PassiveTrigger.OnRemoved);
            foreach (var revert in held) Safe.Run($"{Name} 撤销", revert);
            held.Clear();
        }

        protected internal override void OnUpdate()
        {
            if (spec.TickEvery <= 0f) return;
            tickTimer += Time.deltaTime;
            if (tickTimer < spec.TickEvery) return;
            tickTimer = 0f;
            Fire(PassiveTrigger.OnTick);
        }

        protected internal override void OnHit(HitInfo hit)
        {
            if (hit.Damage > 0) Fire(PassiveTrigger.OnHit, hit.Damage, hit.Target);
        }

        protected internal override void OnGameEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.EnemyKilled: Fire(PassiveTrigger.OnKill, 0, e.Target); break;
                case GameEventKind.Critical: Fire(PassiveTrigger.OnCrit, e.Amount, e.Target); break;
                case GameEventKind.Damaged: Fire(PassiveTrigger.OnHurt, e.Amount); break;
                case GameEventKind.WaveStarted: Fire(PassiveTrigger.OnWaveStart); break;
                case GameEventKind.SkillCast: Fire(PassiveTrigger.OnCast); break;
            }
        }
    }
}
