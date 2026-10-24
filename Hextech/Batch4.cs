using System;
using DBModKit;
using UnityEngine;

namespace Hextech
{
    // 第四批（中等触发）：命中时免费放现成技能（魔法飞弹、飞剑、天雷）、残血回血（破晓决意）、免死（依然在此、不灭）。
    // 不灭的剩余次数要跟着重试波次还原，记在 Engine 的 Run 里
    internal static class Batch4
    {
        // 现成技能：100 = C 级落雷，201 = B 级剑气，300 = A 级落雷（ActiveSkillEnum）
        public const int MissileSkill = 100, SwordSkill = 201, SkySkill = 300;
        public const float MissileChance = 0.3f, MissileEvery = 0.8f;
        public const int SwordEvery = 4;
        public const float SkyEvery = 0.5f;
        public const float DawnLowHp = 0.3f, DawnHeal = 0.3f, DawnSeconds = 3f, DawnCooldown = 60f;
        public const float StillInvulnerable = 1.5f, StillHeal = 0.3f;
        public const int UndyingCharges = 3, UndyingDamage = 30;
        public const float UndyingInvulnerable = 2f;

        private static float missileAt, skyAt, dawnReadyAt, dawnTickAt;
        private static int swordHits, dawnTicks;
        private static bool stillUsed, undyingOn;

        private static bool Has(string id) => Engine.Has(id);

        public static void Reset()
        {
            missileAt = skyAt = dawnReadyAt = dawnTickAt = 0f;
            swordHits = dawnTicks = 0;
            stillUsed = false;
            undyingOn = false;
        }

        public static void Tick(Player me)
        {
            if (!me.IsAlive) return;
            float now = Time.time;
            // 破晓决意：残血时分 3 秒回血
            if (Has("dawnbringer") && dawnTicks == 0 && now >= dawnReadyAt && me.HpPercent < DawnLowHp)
            {
                dawnReadyAt = now + DawnCooldown;
                dawnTicks = (int)DawnSeconds;
                dawnTickAt = now;
            }
            if (dawnTicks > 0 && now >= dawnTickAt)
            {
                dawnTickAt = now + 1f;
                dawnTicks--;
                me.Heal(me.MaxHp * DawnHeal / DawnSeconds);
            }
        }

        private static bool IsThunder(int? id) => id.HasValue && id.Value % 100 == 0 && id.Value >= 0 && id.Value <= 400;

        public static void OnHit(HitInfo hit)
        {
            if (hit.Target == null || !hit.Target.IsAlive) return;
            var me = Game.LocalPlayer;
            if (me == null || hit.Source != me) return;
            float now = Time.time;
            // 落雷自己打出来的命中不再触发落雷，免得连锁
            bool fromThunder = IsThunder(hit.SkillId) || IsThunder(hit.SourceSkillId);
            if (Has("magicmissile") && hit.SkillId.HasValue && !fromThunder && now >= missileAt && UnityEngine.Random.value < MissileChance)
            {
                missileAt = now + MissileEvery;
                me.CastSkillFree(MissileSkill, hit.Target.Position);
            }
            if (Has("flyingsword") && hit.IsNormalAttack && ++swordHits % SwordEvery == 0)
                me.CastSkillFree(SwordSkill, me.Position, hit.Target.Position);
            if (Has("skystrike") && hit.Critical && !fromThunder && now >= skyAt)
            {
                skyAt = now + SkyEvery;
                me.CastSkillFree(SkySkill, hit.Target.Position);
            }
        }

        // 致命伤害：先用每波一次的依然在此，再用每局 3 次的不灭
        public static bool OnLethal(double damage)
        {
            var me = Game.LocalPlayer;
            if (me == null) return false;
            if (Has("stillhere") && !stillUsed)
            {
                stillUsed = true;
                me.GiveInvulnerable(StillInvulnerable);
                me.Heal(me.MaxHp * StillHeal);
                ScreenNotice.Show("依然在此：免死一次（这一波不再触发）", 2.5f);
                return true;
            }
            if (Has("undying") && Engine.TryUseUndying(UndyingCharges, out int left))
            {
                me.GiveInvulnerable(UndyingInvulnerable);
                me.Heal(me.MaxHp);
                if (!undyingOn)
                {
                    undyingOn = true;
                    me.AddAttribute("AddDamage", UndyingDamage);
                }
                ScreenNotice.Show($"不灭：免死、回满生命，这一波伤害 +{UndyingDamage}%（本局还剩 {left} 次）", 3f);
                return true;
            }
            return false;
        }

        public static void OnWaveStarted() => stillUsed = false;

        // 休息阶段：不灭的增伤结束（之后拍重试快照，不能带着）
        public static void OnWaveEnded(Player me) => Revert(me);

        public static void Revert(Player me)
        {
            if (!undyingOn || me == null) return;
            undyingOn = false;
            me.AddAttribute("AddDamage", -UndyingDamage);
        }
    }
}
