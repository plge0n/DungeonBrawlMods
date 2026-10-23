using System;
using System.Collections.Generic;

namespace DBModKit
{
    /// <summary>沙漠死神「噬魂一击」强化普攻的溅射设置。</summary>
    public sealed class SoulSplash
    {
        /// <summary>以主目标为中心的半径（游戏距离单位，和攻击距离同一种）。</summary>
        public float Radius = 3f;
        /// <summary>溅射伤害是主目标伤害的多少倍（1 = 同样的伤害）。</summary>
        public float Ratio = 0.6f;
        /// <summary>溅射命中但没打死时加几层（0 = 只有击杀才叠层，击杀照游戏原本的 4 / 12 / 75 层）。</summary>
        public int HitStacks;
    }

    /// <summary>
    /// 沙漠死神：噬魂一击让下一次普攻强化，击杀叠层。设置溅射后，强化普攻会对主目标周围的敌人再打一次（暴击各自判定），
    /// 打死的照常叠层。只对本地玩家生效；多个 Mod 同时设置时取最大的半径、倍率和层数。
    /// </summary>
    public static class SoulDevourer
    {
        public static bool Available { get; internal set; }

        private static readonly Dictionary<string, SoulSplash> entries = new Dictionary<string, SoulSplash>();

        internal static void Set(string owner, SoulSplash splash)
        {
            if (splash == null) entries.Remove(owner);
            else entries[owner] = splash;
            RunLog.Info(owner, "噬魂一击", splash == null ? "取消溅射" : "设置溅射",
                ("半径", splash?.Radius), ("倍率", splash?.Ratio), ("命中叠层", splash?.HitStacks));
        }

        internal static void ClearOwner(string owner) => entries.Remove(owner);

        internal static void OnStrikeEnd(object mode)
        {
            if (entries.Count == 0 || !ModKit.Compatible) return;
            try
            {
                if (!GameRaw.IsLocalEmpoweredStrike(mode)) return;
                float radius = 0f, ratio = 0f;
                int stacks = 0;
                foreach (var s in entries.Values)
                {
                    radius = Math.Max(radius, s.Radius);
                    ratio = Math.Max(ratio, s.Ratio);
                    stacks = Math.Max(stacks, s.HitStacks);
                }
                if (radius <= 0f || ratio <= 0f) return;
                int hits = GameRaw.SoulSplash(mode, radius, ratio, stacks);
                if (hits > 0) RunLog.Debug(ModKitPlugin.Name, "噬魂一击", "溅射", ("目标数", hits));
            }
            catch (Exception e)
            {
                Safe.Report("噬魂一击溅射", e);
            }
        }
    }
}
