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

        /// <summary>
        /// 房间里只有自己：没联机，或者自己当主机且没有别人连进来。连到别人的房间时为 false。
        /// 改数值、作弊类功能应该只在单人时生效（游戏不检查 Mod，伤害由各自客户端计算，改了会影响队友）。
        /// </summary>
        public static bool IsSolo
        {
            get
            {
                if (!ModKit.Compatible) return false;
                if (GameRaw.ServerActive()) return GameRaw.ServerConnections() <= 1;
                return !GameRaw.ClientActive();
            }
        }

        /// <summary>当前波次（不在战斗中时是上一局的值）。</summary>
        public static int Wave => ModKit.Compatible ? GameRaw.Wave() : 0;

        /// <summary>自己是房主（联机主机，或者单人）。</summary>
        public static bool IsHost => ModKit.Compatible && (GameRaw.ServerActive() || !GameRaw.ClientActive());

        /// <summary>房间里的玩家数（含自己）。只有主机知道准确人数；连到别人的房间时返回 -1。</summary>
        public static int PlayerCount
        {
            get
            {
                if (!ModKit.Compatible) return 1;
                if (GameRaw.ServerActive()) return Mathf.Max(1, GameRaw.ServerConnections());
                return GameRaw.ClientActive() ? -1 : 1;
            }
        }

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

        // ---- 属性（含装备、遗物、卡牌等加成后的当前值）----
        public long Attack => GameRaw.Attack(Native);
        public int Strength => GameRaw.Strength(Native);
        public int Agility => GameRaw.Agility(Native);
        public int Stamina => GameRaw.Stamina(Native);
        /// <summary>每秒攻击次数。</summary>
        public float AttackSpeed => GameRaw.AttackSpeed(Native);
        /// <summary>暴击率，0~1。</summary>
        public float CriticalRate => GameRaw.CriticalRate(Native);
        public float CriticalDamage => GameRaw.CriticalDamage(Native);
        public int Armor => GameRaw.Armor(Native);
        public int Dodge => GameRaw.Dodge(Native);
        public float LifeSteal => GameRaw.LifeSteal(Native);

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
        public int Gold => GameRaw.Gold(Native);

        /// <summary>是本地玩家（只有本地玩家能用下面的修改方法）。</summary>
        public bool IsLocal => Native != null && Native == GameRaw.LocalPlayer();

        // 修改都走游戏自己的方法：金币等数值有防篡改校验，直接写字段会被当成作弊上报
        /// <summary>回血（会同步给服务器）。</summary>
        public void Heal(double amount)
        {
            RequireLocal();
            if (amount > 0) GameRaw.Heal(Native, amount);
        }

        /// <summary>加蓝（负数为扣蓝），结果限制在 0~上限。</summary>
        public void AddMana(int amount)
        {
            RequireLocal();
            GameRaw.AddMana(Native, amount);
        }

        /// <summary>加金币（不吃金币加成和关卡倍率），返回实际到账数量。</summary>
        public int AddGold(int amount)
        {
            RequireLocal();
            return amount > 0 ? GameRaw.AddGold(Native, amount) : 0;
        }

        /// <summary>获得一个遗物（ItemType 编号，见 RelicInfo）。超过本局获取上限等情况下返回 false。</summary>
        public bool GiveRelic(int relicId)
        {
            RequireLocal();
            return GameRaw.GiveRelic(Native, relicId);
        }

        /// <summary>
        /// 加力量、敏捷、体力、攻击（负数为减），加在加成上而不是基础值上，不会触发防篡改校验；体力会同步生命上限。
        /// 本局有效，下一局英雄重新生成时清零。要临时加成就到时间后用相反数调用一次。
        /// </summary>
        public void AddStats(int strength, int agility, int stamina, long attack)
        {
            RequireLocal();
            GameRaw.AddStats(Native, strength, agility, stamina, attack);
        }

        private void RequireLocal()
        {
            if (!IsLocal) throw new InvalidOperationException("只能修改本地玩家");
        }

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
