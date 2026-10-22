using System;
using UnityEngine;

namespace DBModKit
{
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
        /// <summary>普攻（含普攻特效）。不是普攻、也没有 SkillId 时，是被动、异常状态、装备特效等造成的。</summary>
        public bool IsNormalAttack { get; }
        /// <summary>这一下打死了目标（与游戏判定斩杀奖励的方式一致：伤害 ≥ 血量 + 护盾）。</summary>
        public bool Lethal => Damage >= TargetHp + TargetShield;

        internal HitInfo(Role source, Role target, long damage, long targetHp, long targetShield, int? skillId, bool critical, bool normal)
        {
            Source = source;
            Target = target;
            Damage = damage;
            TargetHp = targetHp;
            TargetShield = targetShield;
            SkillId = skillId;
            Critical = critical;
            IsNormalAttack = normal;
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

        internal static void Install()
        {
            GameRaw.SubscribeHits(OnHit);
        }

        private static void OnHit(Component source, Component target, long damage, long hp, long shield, int skillId, bool active, bool critical, bool normal)
        {
            var evt = Hit;
            if (evt == null) return;
            var info = new HitInfo(Role.Wrap(source), Role.Wrap(target), damage, hp, shield, active ? skillId : (int?)null, critical, normal);
            Safe.InvokeEach(evt, nameof(Hit), info);
        }
    }
}
