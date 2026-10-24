using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBModKit
{
    /// <summary>伤害类型（游戏的 AttackType）。</summary>
    public enum HitType
    {
        Unknown = -1,
        Normal,
        Skill,
        /// <summary>持续伤害（灼烧、中毒等 Buff 每跳的伤害）。</summary>
        Buff,
        /// <summary>普攻特效。</summary>
        AttackEffect,
        TrueDamage,
    }

    /// <summary>伤害来源（游戏的 DamageSourceKind）。大部分伤害是 Default，只有少数地方会标出来。</summary>
    public enum HitOrigin
    {
        Unknown = -1,
        Default,
        ActiveSkill,
        Aura,
        Status,
        Reflect,
        PassiveProc,
        IllusoryWeapon,
    }

    /// <summary>没带技能实例的伤害是谁造成的。</summary>
    public enum HitEffectKind
    {
        None,
        /// <summary>被动技能。</summary>
        Passive,
        /// <summary>装备特效。</summary>
        Equipment,
        Relic,
        /// <summary>持续伤害的 Buff（灼烧、中毒……）。</summary>
        Buff,
    }

    // GameRaw 交给 Combat 的原始命中数据
    internal struct RawHit
    {
        public Component Source, Target;
        public long Damage, RawDamage, Hp, Shield;
        public int? SkillId, SourceSkillId;
        public bool Critical, Normal;
        public HitType Type;
        public HitOrigin Origin;
        public HitEffectKind EffectKind;
        public string EffectName;
    }

    /// <summary>一次命中。</summary>
    public readonly struct HitInfo
    {
        public Role Source { get; }
        public Role Target { get; }
        /// <summary>实际伤害（含打掉的护盾）。</summary>
        public long Damage { get; }
        /// <summary>
        /// 打出的伤害，不受目标剩余血量限制：打死目标时溢出的部分也算（<see cref="Damage"/> 最多到目标的血量 + 护盾）。
        /// 适合统计"最高一击"；补丁没装上时等于 <see cref="Damage"/>。
        /// </summary>
        public long RawDamage { get; }
        /// <summary>命中前目标的血量和护盾。</summary>
        public long TargetHp { get; }
        public long TargetShield { get; }
        /// <summary>由主动技能造成时为技能编号，普攻、被动等为 null。</summary>
        public int? SkillId { get; }
        public bool Critical { get; }
        /// <summary>普攻本身（AttackType.Normal）。普攻特效、被动、异常状态、装备特效等既不是普攻、也没有 SkillId。</summary>
        public bool IsNormalAttack { get; }
        /// <summary>伤害类型；取不到时为 Unknown。</summary>
        public HitType Type { get; }
        /// <summary>伤害来源；取不到时为 Unknown。</summary>
        public HitOrigin Origin { get; }
        /// <summary>
        /// 造成这次伤害的技能实例的技能编号（技能放出来的弹道、区域等）。和 <see cref="SkillId"/> 不同，
        /// 游戏没有记成"主动释放"的技能伤害（很多技能结算伤害时没标来源）也有。没有技能实例时为 null。
        /// </summary>
        public int? SourceSkillId { get; }
        /// <summary>
        /// 没有技能实例时，造成这次伤害的被动、装备特效、遗物或 Buff（只对本地玩家打出的伤害识别）。
        /// 名字是游戏里显示的名字（装备特效用装备名）；识别不出时 Kind 为 None、名字为 null。
        /// </summary>
        public HitEffectKind EffectKind { get; }
        public string EffectName { get; }
        /// <summary>这一下打死了目标（与游戏判定斩杀奖励的方式一致：伤害 ≥ 血量 + 护盾）。</summary>
        public bool Lethal => Damage >= TargetHp + TargetShield;

        internal HitInfo(in RawHit h)
        {
            Source = Role.Wrap(h.Source);
            Target = Role.Wrap(h.Target);
            Damage = h.Damage;
            RawDamage = h.RawDamage;
            TargetHp = h.Hp;
            TargetShield = h.Shield;
            SkillId = h.SkillId;
            Critical = h.Critical;
            IsNormalAttack = h.Normal;
            Type = h.Type;
            Origin = h.Origin;
            SourceSkillId = h.SourceSkillId ?? h.SkillId;
            EffectKind = h.EffectKind;
            EffectName = h.EffectName;
        }
    }

    /// <summary>战斗事件。</summary>
    public static class Combat
    {
        /// <summary>
        /// 本机角色造成伤害时触发（攻击方的客户端上计算伤害，所以只能看到自己和自己召唤物打出的伤害）。
        /// 联机时斩杀以主机为准，这里的 Lethal 是本地判断，极少数情况下和主机不一致。
        /// </summary>
        public static event Action<HitInfo> Hit;

        // 有订阅者时才值得去取调用栈
        internal static bool HasSubscribers => Hit != null;

        /// <summary>致命伤害拦截的补丁装上了（<see cref="ModContext.OnLethalDamage"/> 可用）。</summary>
        public static bool LethalGuardAvailable { get; internal set; }

        private static readonly List<Func<double, bool>> lethalGuards = new List<Func<double, bool>>();

        internal static bool HasLethalGuards => lethalGuards.Count > 0;

        internal static void AddLethalGuard(Func<double, bool> guard) => lethalGuards.Add(guard);

        internal static void RemoveLethalGuard(Func<double, bool> guard) => lethalGuards.Remove(guard);

        // 依次问，第一个返回 true 的拦下（后面的不再问，免得两张免死卡一次全用掉）
        internal static bool RaiseLethal(double damage)
        {
            foreach (var g in lethalGuards.ToArray())
            {
                bool stop = false;
                Safe.Run("致命伤害拦截", () => stop = g(damage));
                if (stop) return true;
            }
            return false;
        }

        /// <summary>伤害上下文的钩子已安装（<see cref="HitInfo.Type"/>、<see cref="HitInfo.Origin"/>、<see cref="HitInfo.SourceSkillId"/> 可用）。</summary>
        public static bool ContextAvailable { get; internal set; }

        /// <summary>
        /// 本地玩家放出技能时触发（扣蓝、进冷却之前；取消瞄准不算）。免费释放也会触发，看 <see cref="SkillCast.IsFree"/>。
        /// 形态切换类技能关闭、雷之一闪连闪中再按都不触发。
        /// </summary>
        public static event Action<SkillCast> Cast;

        internal static void RaiseSkillCast(SkillCast cast)
        {
            var evt = Cast;
            if (evt != null) Safe.InvokeEach(evt, nameof(Cast), cast);
        }

        internal static void Install()
        {
            GameRaw.SubscribeHits(OnHit);
        }

        private static void OnHit(RawHit raw)
        {
            var evt = Hit;
            if (evt == null) return;
            var info = new HitInfo(raw);
            Safe.InvokeEach(evt, nameof(Hit), info);
        }
    }
}
