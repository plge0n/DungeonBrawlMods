using System.Collections.Generic;
using System.Linq;
using DBModKit;

namespace SmartSkill
{
    // 判断技能能不能一击打死目标。ModKit 的估算没算目标减伤，所以用实际命中校准：
    // 记录每个技能最近几次"实际伤害 / 估算伤害"，取最小值（保守）乘到估算上
    internal static class KillPredictor
    {
        private const int Samples = 8;

        private class Stats
        {
            public readonly Queue<float> Ratios = new Queue<float>();
            public int Kills;
            public int Hits;
            public string LastHit;
        }

        private static readonly Dictionary<int, Stats> stats = new Dictionary<int, Stats>();

        public static void Reset() => stats.Clear();

        // 没有命中数据时按 1 算，由 KillMarginPercent 兜底
        public static float Calibration(int skillId)
            => stats.TryGetValue(skillId, out var s) && s.Ratios.Count > 0 ? s.Ratios.Min() : 1f;

        public static bool CanKill(SkillSlot slot, Player player, Role target, float marginPercent)
        {
            if (target.IsInvulnerable) return false;
            var dmg = slot.EstimateDamage(player, target);
            if (dmg == null) return false;
            double expected = dmg.Value * Calibration(slot.Info.Id) * marginPercent / 100.0;
            return expected >= target.EffectiveHp;
        }

        public static string Describe(SkillInfo info)
        {
            if (!stats.TryGetValue(info.Id, out var s) || s.Hits == 0) return "还没有命中记录，按估算伤害判断";
            return $"命中 {s.Hits} 次、斩杀 {s.Kills} 次，校准系数 {Calibration(info.Id):F2}；{s.LastHit}";
        }

        public static void OnHit(HitInfo hit)
        {
            if (hit.SkillId is not int id) return;
            var player = Game.LocalPlayer;
            if (player == null || hit.Source != player || hit.Target == null) return;
            var slot = SkillBar.Find(id);
            if (slot?.Info == null || !slot.Info.HasKillReward) return;

            if (!stats.TryGetValue(id, out var s)) stats[id] = s = new Stats();
            s.Hits++;
            if (hit.Lethal) s.Kills++;
            s.LastHit = hit.Lethal ? "上次命中：斩杀" : $"上次命中：未斩杀（差 {hit.TargetHp + hit.TargetShield - hit.Damage}）";

            // 打死时伤害可能被截断成剩余血量，暴击会偏高，这两种不参与校准
            var estimate = slot.EstimateDamage(player, hit.Target);
            if (hit.Critical || hit.Lethal || estimate == null || estimate.Value <= 0) return;
            float ratio = UnityEngine.Mathf.Clamp(hit.Damage / (float)estimate.Value, 0.1f, 2f);
            s.Ratios.Enqueue(ratio);
            while (s.Ratios.Count > Samples) s.Ratios.Dequeue();
        }
    }
}
