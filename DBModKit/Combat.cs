using System;
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

    // GameRaw 交给 Combat 的原始命中数据
    internal struct RawHit
    {
        public Component Source, Target;
        public long Damage, Hp, Shield;
        public int? SkillId, SourceSkillId;
        public bool Critical, Normal;
        public HitType Type;
        public HitOrigin Origin;
    }

    /// <summary>一次命中。</summary>
    public readonly struct HitInfo
    {
        public Role Source { get; }
        public Role Target { get; }
        /// <summary>实际伤害（含打掉的护盾）。</summary>
        public long Damage { get; }
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
        /// <summary>这一下打死了目标（与游戏判定斩杀奖励的方式一致：伤害 ≥ 血量 + 护盾）。</summary>
        public bool Lethal => Damage >= TargetHp + TargetShield;

        internal HitInfo(in RawHit h)
        {
            Source = Role.Wrap(h.Source);
            Target = Role.Wrap(h.Target);
            Damage = h.Damage;
            TargetHp = h.Hp;
            TargetShield = h.Shield;
            SkillId = h.SkillId;
            Critical = h.Critical;
            IsNormalAttack = h.Normal;
            Type = h.Type;
            Origin = h.Origin;
            SourceSkillId = h.SourceSkillId ?? h.SkillId;
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

        /// <summary>伤害上下文的钩子已安装（<see cref="HitInfo.Type"/>、<see cref="HitInfo.Origin"/>、<see cref="HitInfo.SourceSkillId"/> 可用）。</summary>
        public static bool ContextAvailable { get; internal set; }

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
