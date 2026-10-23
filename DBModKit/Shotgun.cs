using System;
using System.Collections.Generic;

namespace DBModKit
{
    /// <summary>
    /// 男枪（冲锋装填）的弹药：普攻要消耗弹药，原版上限写死是 2 发、冲锋装填 1 发。
    /// 设置后本地玩家的弹药上限和冲锋装填数按 Mod 给的值算，头顶的弹药条也会补上多出来的弹壳。只对本地玩家生效；
    /// 多个 Mod 同时设置时取最大值。用 <see cref="ModContext.SetShotgunAmmo"/> 设置，卸载时自动撤销。
    /// </summary>
    public static class Shotgun
    {
        public const int BaseMax = 2;
        /// <summary>弹药上限最多这么多（弹药条再长就挡视线了）。</summary>
        public const int Cap = 12;

        public static bool Available { get; internal set; }

        private static readonly Dictionary<string, (Func<int> max, int dash)> entries = new Dictionary<string, (Func<int>, int)>();

        internal static bool Active => entries.Count > 0;

        internal static void Set(string owner, Func<int> max, int dashLoad)
        {
            if (max == null) entries.Remove(owner);
            else entries[owner] = (max, dashLoad);
            RunLog.Info(owner, "男枪弹药", max == null ? "恢复原版" : "设置弹药", ("冲锋装填", max == null ? (int?)null : dashLoad));
        }

        internal static void ClearOwner(string owner) => entries.Remove(owner);

        /// <summary>本地玩家现在的弹药上限（没有 Mod 设置时是 2）。</summary>
        public static int Max
        {
            get
            {
                int m = BaseMax;
                foreach (var e in entries.Values)
                {
                    int v;
                    try { v = e.max(); }
                    catch (Exception ex) { Safe.Report("男枪弹药上限", ex); continue; }
                    m = Math.Max(m, v);
                }
                return Math.Min(Cap, m);
            }
        }

        internal static int DashLoad
        {
            get
            {
                int d = 1;
                foreach (var e in entries.Values) d = Math.Max(d, e.dash);
                return d;
            }
        }

        /// <summary>本地玩家现在有几发（不是男枪时为 -1）。</summary>
        public static int Ammo
        {
            get
            {
                if (!ModKit.Compatible) return -1;
                var a = GameRaw.LocalShotgunAmmo();
                return a == null ? -1 : GameRaw.AmmoCount(a);
            }
        }

        // 补丁用：这个弹药对象是本地玩家的，并且有 Mod 设置
        internal static bool Mine(object ammo) => Active && ammo != null && ReferenceEquals(ammo, GameRaw.LocalShotgunAmmo());
    }

    // bcc 的真方法：本地玩家的弹药换成 Mod 的上限，前缀返回 false 跳过原版；其他人的照旧
    internal static class ShotgunHooks
    {
        internal static bool StartReloadPrefix(object __instance)
        {
            if (!Shotgun.Mine(__instance)) return true;
            return Safe.Skip("男枪弹药（装填）", () => GameRaw.AmmoStartReload(__instance, Shotgun.Max));
        }

        internal static bool AdvanceReloadPrefix(object __instance, float __0, float __1, float __2, ref bool __result)
        {
            if (!Shotgun.Mine(__instance)) return true;
            bool done = false;
            if (Safe.Skip("男枪弹药（装填进度）", () => done = GameRaw.AmmoAdvanceReload(__instance, __0, __1, __2, Shotgun.Max))) return true;
            __result = done;
            return false;
        }

        internal static bool DashPrefix(object __instance)
        {
            if (!Shotgun.Mine(__instance)) return true;
            return Safe.Skip("男枪弹药（冲锋）", () => GameRaw.AmmoAdd(__instance, Shotgun.DashLoad, Shotgun.Max));
        }

        internal static bool FillPrefix(object __instance)
        {
            if (!Shotgun.Mine(__instance)) return true;
            return Safe.Skip("男枪弹药（补满）", () => GameRaw.AmmoFill(__instance, Shotgun.Max));
        }

        internal static void TickPostfix(object __instance)
        {
            if (!Shotgun.Active || !GameRaw.IsLocalShotgun(__instance)) return;
            Safe.Run("男枪弹药（战术装填）", () => GameRaw.AmmoTacticalReload(__instance, Shotgun.Max));
        }

        internal static bool SyncPrefix(object __instance)
        {
            if (!Shotgun.Active || !GameRaw.IsLocalShotgun(__instance)) return true;
            return Safe.Skip("男枪弹药（同步）", () => GameRaw.AmmoSync(__instance));
        }

        internal static void UiPrefix(object __instance)
        {
            if (!ModKit.Compatible || !GameRaw.IsLocalAmmoUi(__instance)) return;
            Safe.Run("男枪弹药条", () => GameRaw.AmmoUi(__instance, Shotgun.Active ? Shotgun.Max : Shotgun.BaseMax));
        }
    }
}
