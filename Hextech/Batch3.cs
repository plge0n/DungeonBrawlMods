using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;

namespace Hextech
{
    // 第三批里不用存进重试快照的效果：慢炖、无休恢复、神秘拳、自动施放、俯冲轰炸。
    // 任务计数、属性互转要跟着重试波次还原，放在 Engine 的 Run 里
    internal static class Batch3
    {
        public const float DiveRadius = 6f, DiveDamage = 0.5f;
        public const float CookRadius = 4f, CookDamage = 0.25f;
        public const float RestlessMeters = 10f, RestlessHeal = 0.02f;
        public const float PunchSeconds = 0.3f, PunchEvery = 0.2f;

        private static float cookAt, punchAt, autoAt;
        private static Vector3 lastPos;
        private static bool hasPos;
        private static float moved;
        // 自动施放：已经开过一次的技能实例（同一个实例被玩家关掉就不再开；换了新实例、或者离开技能栏再回来时再开一次）
        private static readonly HashSet<SkillSlot> autoSeen = new HashSet<SkillSlot>();

        private static bool Has(string id) => Engine.Has(id);

        public static void Reset()
        {
            cookAt = punchAt = autoAt = 0f;
            hasPos = false;
            moved = 0f;
            autoSeen.Clear();
        }

        public static void Tick(Player me, bool inWave)
        {
            if (!me.IsAlive)
            {
                hasPos = false;
                return;
            }
            float now = Time.time;
            if (Has("slowcook") && inWave && now >= cookAt)
            {
                cookAt = now + 1f;
                me.DamageArea((float)Math.Min(2e9, me.Attack * CookDamage), me.Position, CookRadius, null, 1f, spareTeammates: true);
            }
            if (Has("restless")) TickRestless(me);
            if (Has("autocast") && Time.unscaledTime >= autoAt)
            {
                autoAt = Time.unscaledTime + 1f;
                foreach (int id in Engine.SkillIds())
                {
                    var slot = me.FindSkill(id);
                    if (slot == null) continue;
                    // 技能栏重排时游戏会把离开的技能的自动释放关掉：不在栏上就忘掉它，回到栏上再开
                    if (!slot.IsOnSkillBar)
                    {
                        autoSeen.Remove(slot);
                        continue;
                    }
                    if (autoSeen.Add(slot)) slot.SetAutoCast(true);
                }
            }
        }

        // 无休恢复：按走过的路程回血；一帧挪太远的是传送、复活，不算
        private static void TickRestless(Player me)
        {
            var pos = me.Position;
            if (hasPos)
            {
                var d = pos - lastPos;
                d.y = 0f;
                float step = d.magnitude;
                if (step < 5f) moved += step;
            }
            lastPos = pos;
            hasPos = true;
            if (moved < RestlessMeters) return;
            moved -= RestlessMeters;
            me.Heal(me.MaxHp * RestlessHeal);
        }

        // 停用、卸载时：关掉 Hextech 打开过、现在还开着的自动释放
        public static void Revert()
        {
            foreach (var slot in autoSeen)
                if (slot.IsAutoCastOn) slot.SetAutoCast(false);
            autoSeen.Clear();
        }

        // 神秘拳：自己的普攻命中时减所有主动技能的剩余冷却
        public static void OnHit(HitInfo hit)
        {
            if (!hit.IsNormalAttack || !Has("mysticpunch")) return;
            var me = Game.LocalPlayer;
            if (me == null || hit.Source != me) return;
            float now = Time.time;
            if (now < punchAt) return;
            punchAt = now + PunchEvery;
            foreach (var slot in me.ActiveSkills())
                if (!slot.IsToggledOn && slot.CooldownRemaining > 0f) slot.ReduceCooldown(PunchSeconds);
        }

        // 俯冲轰炸：死亡时在原地爆炸
        public static void DiveBomb(Player me)
        {
            me.DamageArea((float)Math.Min(2e9, me.MaxHp * DiveDamage), me.Position, DiveRadius, "Effect/DeathExplosion", 1.5f, spareTeammates: true);
        }
    }
}
