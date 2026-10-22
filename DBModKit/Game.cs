using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DBModKit
{
    /// <summary>游戏全局状态。</summary>
    public static class Game
    {
        /// <summary>本地玩家；不在战斗中时为 null。</summary>
        public static Player LocalPlayer => ModKit.Compatible ? Player.Wrap(GameRaw.LocalPlayer()) : null;

        public static bool InBattle => LocalPlayer != null;

        /// <summary>游戏设置里的"自动战斗"开着。</summary>
        public static bool AutoBattle => ModKit.Compatible && GameRaw.AutoBattleOn();

        /// <summary>取游戏本地化文本，找不到时返回 fallback。</summary>
        public static string Localize(string key, string fallback = null)
            => ModKit.Compatible ? GameRaw.Localize(key, fallback ?? key) : fallback ?? key;
    }

    /// <summary>场上的角色（玩家、敌人、召唤物）。同一个游戏对象总是对应同一个 Role 实例，可以直接比较。</summary>
    public class Role
    {
        private static readonly ConditionalWeakTable<Component, Role> cache = new ConditionalWeakTable<Component, Role>();

        internal readonly Component Native;

        internal Role(Component native) => Native = native;

        internal static Role Wrap(Component native)
        {
            if (native == null) return null;
            return cache.GetValue(native, n => n is bbe ? new Player(n) : new Role(n));
        }

        /// <summary>游戏原始对象（混淆类型），仅供调试；不要依赖它的成员名。</summary>
        public Component Raw => Native;

        public long Hp => GameRaw.Hp(Native);
        public long MaxHp => GameRaw.MaxHp(Native);
        public float HpPercent => (float)Hp / Mathf.Max(1L, MaxHp);
        /// <summary>存活且在场。对象被销毁后返回 false。</summary>
        public bool IsAlive => Native != null && GameRaw.IsAlive(Native);
        public Vector3 Position => GameRaw.Position(Native);
        /// <summary>体型半径：判断距离时游戏会把它加到范围上。</summary>
        public float HitRadius => GameRaw.HitRadius(Native);
        public bool IsBoss => GameRaw.IsBoss(Native);
        public bool IsElite => GameRaw.IsElite(Native);
        public long Shield => GameRaw.Shield(Native);
        /// <summary>血量 + 护盾：伤害要达到这个值才能击杀。</summary>
        public long EffectiveHp => Hp + Shield;
        /// <summary>无敌中，受到的伤害记为 0。</summary>
        public bool IsInvulnerable => GameRaw.IsInvulnerable(Native);
        /// <summary>普攻距离；对方在 距离 ≤ AttackRange + 对方体型半径 内时可以被普攻打到。</summary>
        public float AttackRange => GameRaw.AttackRange(Native);
        public float MoveSpeed => GameRaw.MoveSpeed(Native);
        /// <summary>当前状态（RoleState 枚举名）：Idle、Run、Attack、Skill、Skill2…、Dead 等。</summary>
        public string State => GameRaw.State(Native);
        public bool IsAttacking => GameRaw.IsAttacking(Native);
        /// <summary>在对方的普攻范围内。</summary>
        public bool InAttackRangeOf(Role attacker, float extra = 0f)
            => attacker.DistanceTo(this) <= attacker.AttackRange + HitRadius + extra;

        /// <summary>水平距离（忽略高度）。</summary>
        public float DistanceTo(Role other) => GameRaw.Distance(Native, other.Position);

        /// <summary>与游戏判定一致：range ≤ 0 表示不限距离，否则 距离 ≤ range + 对方体型半径。</summary>
        public bool IsWithin(Role other, float range)
            => range <= 0f || DistanceTo(other) <= range + other.HitRadius;
    }

    /// <summary>玩家角色。</summary>
    public sealed class Player : Role
    {
        internal Player(Component native) : base(native) { }

        internal new static Player Wrap(Component native) => Role.Wrap(native) as Player;

        public int Mana => GameRaw.Mana(Native);
        public int MaxMana => GameRaw.MaxMana(Native);

        /// <summary>变身中（变身类技能生效期间）。</summary>
        public bool IsTransformed => GameRaw.IsTransformed(Native);

        /// <summary>普攻冷却好了。</summary>
        public bool AttackReady => GameRaw.AttackTimer(Native) > GameRaw.AttackInterval(Native);
        /// <summary>距离下次能普攻还有多少秒。</summary>
        public float AttackCooldownLeft => Mathf.Max(0f, GameRaw.AttackInterval(Native) - GameRaw.AttackTimer(Native));
        public float AttackInterval => GameRaw.AttackInterval(Native);
        /// <summary>本次普攻已经出手（伤害或弹道已经发出）。之后移动不会丢掉这次攻击；出手前移动会取消攻击，但冷却会立刻转好。</summary>
        public bool AttackHitDone => GameRaw.AttackHitDone(Native);

        /// <summary>
        /// 自己放的某个技能还在生效的剩余秒数（持续伤害、增益、变身等），没有则为 0。
        /// 瞬发技能的实例也会短暂存在，判断"增益还在"时最好留一点阈值。
        /// </summary>
        public float SkillEffectRemaining(int skillId) => GameRaw.SkillEffectRemaining(Native, skillId);

        /// <summary>对该敌人的增伤比例（全局增伤 + 对 Boss/精英/普通敌人的增伤），0.3 表示 +30%。</summary>
        public float DamageBonusAgainst(Role target) => GameRaw.DamageBonus(Native, target.Native);

        /// <summary>当前所有存活的敌人。</summary>
        public List<Role> Enemies()
        {
            var result = new List<Role>();
            foreach (var e in GameRaw.Enemies(Native))
            {
                var role = Wrap(e);
                if (role != null && role.IsAlive) result.Add(role);
            }
            return result;
        }

        /// <summary>范围内存活的敌人（range ≤ 0 表示不限距离）。</summary>
        public List<Role> EnemiesWithin(float range)
        {
            var result = Enemies();
            result.RemoveAll(e => !IsWithin(e, range));
            return result;
        }

        /// <summary>按评分选范围内的敌人，分数越低越优先；没有敌人时返回 null。</summary>
        public Role PickEnemy(float range, Func<Role, float> score)
        {
            Role best = null;
            float bestScore = float.MaxValue;
            foreach (var e in EnemiesWithin(range))
            {
                float s = score(e);
                if (s < bestScore)
                {
                    best = e;
                    bestScore = s;
                }
            }
            return best;
        }
    }
}
