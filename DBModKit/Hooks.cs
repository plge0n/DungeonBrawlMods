using System;
using System.Reflection;
using HarmonyLib;

namespace DBModKit
{
    // Harmony 补丁统一在这里按签名查找并安装；找不到或安装失败只停用对应功能，不影响其他部分
    internal static class Hooks
    {
        public static void Install(Harmony harmony)
        {
            AutoCast.Available =
                TryPatch(harmony, "自动释放决策",
                    AccessTools.Method(typeof(ez), "dxx", new[] { typeof(ActiveSkillEnum), typeof(bbe), typeof(int) }),
                    prefix: nameof(AutoCastHooks.DecidePrefix), postfix: nameof(AutoCastHooks.DecidePostfix))
                && TryPatch(harmony, "自动释放选目标",
                    AccessTools.Method(typeof(ez), "dxy", new[] { typeof(bbe), typeof(ActiveSkillData) }),
                    prefix: nameof(AutoCastHooks.TargetPrefix));
        }

        private static bool TryPatch(Harmony harmony, string feature, MethodBase target, string prefix = null, string postfix = null)
        {
            if (target == null)
            {
                Log.Error($"{feature}：找不到目标方法，该功能停用");
                return false;
            }
            try
            {
                harmony.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(typeof(AutoCastHooks), prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(typeof(AutoCastHooks), postfix));
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"{feature}：补丁安装失败，该功能停用：{e}");
                return false;
            }
        }
    }

    // ez.dxx(ActiveSkillEnum, bbe, int bookId)：游戏的自动释放入口，技能冷却好、蓝够时每帧调用
    // ez.dxy(bbe, ActiveSkillData)：dxx 里选目标，原版找施法范围内最近的敌人
    internal static class AutoCastHooks
    {
        // dxx 执行期间的决策，dxy 据此选目标
        [ThreadStatic] private static AutoCastVerdict? current;

        internal struct State
        {
            public SkillSlot Slot;
            public AutoCastVerdict Verdict;
        }

        internal static bool DecidePrefix(ActiveSkillEnum __0, bbe __1, ref bool __result, out State? __state)
        {
            __state = null;
            var handler = AutoCast.Handler;
            if (handler == null || __1 == null || __1 != GameRaw.LocalPlayer()) return true;

            var slot = SkillBar.Find((int)__0);
            var player = Game.LocalPlayer;
            if (slot == null || player == null) return true;

            AutoCastVerdict verdict;
            try
            {
                verdict = handler(slot, player);
            }
            catch (Exception e)
            {
                Safe.Report("AutoCast 处理器", e);
                return true;
            }
            __state = new State { Slot = slot, Verdict = verdict };

            switch (verdict.Action)
            {
                case AutoCastAction.Skip:
                    __result = false;
                    return false;
                case AutoCastAction.Vanilla:
                    return true;
                default:
                    current = verdict;
                    return true;
            }
        }

        internal static void DecidePostfix(bool __result, State? __state)
        {
            current = null;
            if (__state is State s) AutoCast.RaiseResolved(new AutoCastResult(s.Slot, s.Verdict, __result));
        }

        internal static bool TargetPrefix(bbe __0, ActiveSkillData __1, ref yb __result)
        {
            if (!(current is AutoCastVerdict v) || __0 == null) return true;
            var player = Game.LocalPlayer;
            if (player == null || player.Native != __0) return true;

            if (v.Action == AutoCastAction.CastOnSelf && (__1.indicator == "None" || __1.indicator == "Switch"))
            {
                // 无需瞄准的技能，dxx 拿到非空目标后直接释放，目标本身不会被使用
                __result = __0;
                return false;
            }
            if (v.TargetScore == null) return true;

            var target = player.PickEnemy(__1.castingRange, v.TargetScore);
            __result = (yb)target?.Native;
            return false;
        }
    }
}
