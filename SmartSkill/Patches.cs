using System.Collections.Generic;
using HarmonyLib;

namespace SmartSkill
{
    // ez.dxx(ActiveSkillEnum, bbe, int bookId)：自动释放入口，由 bht.kcac92... 在冷却好、蓝够时调用
    [HarmonyPatch(typeof(ez), "dxx")]
    internal static class AutoCastGatePatch
    {
        // 窗口里显示的"上一次决策"
        internal static readonly Dictionary<ActiveSkillEnum, string> LastStatus = new Dictionary<ActiveSkillEnum, string>();

        // 没放出来的技能每帧都会进来，日志只在状态变化时写；真正放出来的每次都写
        private static readonly Dictionary<ActiveSkillEnum, string> lastLogged = new Dictionary<ActiveSkillEnum, string>();

        private struct State
        {
            public bool Handled;
            public Decision Decision;
            public string Reason;
        }

        private static bool Prefix(ActiveSkillEnum __0, bbe __1, ref bool __result, out State __state)
        {
            __state = default;
            if (!Plugin.Enabled.Value || __1 == null || __1 != GameApi.LocalPlayer) return true;

            var decision = SkillBrain.Decide(__0, __1, out var reason);
            __state = new State { Handled = true, Decision = decision, Reason = reason };

            if (decision == Decision.Skip)
            {
                __result = false;
                return false;
            }
            if (decision != Decision.Vanilla) SkillBrain.ActiveRule = RuleStore.Get(__0);
            SkillBrain.CastOnSelf = decision == Decision.CastOnSelf;
            return true;
        }

        private static void Postfix(ActiveSkillEnum __0, bool __result, State __state)
        {
            SkillBrain.ActiveRule = null;
            SkillBrain.CastOnSelf = false;
            if (!__state.Handled) return;

            string outcome = __result ? "已释放"
                : __state.Decision == Decision.Skip ? "跳过"
                : "原版未放(范围内无目标或状态不对)";
            string status = $"{outcome}：{__state.Reason}";
            LastStatus[__0] = status;

            if (!Plugin.VerboseLog.Value) return;
            if (!__result)
            {
                // 原因里带实时数值，只按结果判断是否变化，避免刷屏
                if (lastLogged.TryGetValue(__0, out var last) && last == outcome) return;
                lastLogged[__0] = outcome;
            }
            else
            {
                lastLogged.Remove(__0);
            }
            Plugin.Log.LogInfo($"{__0}: {status}");
        }
    }

    // ez.dxy(bbe, ActiveSkillData)：原版找施法范围内最近的敌人
    [HarmonyPatch(typeof(ez), "dxy")]
    internal static class AutoCastTargetPatch
    {
        private static bool Prefix(bbe __0, ActiveSkillData __1, ref yb __result)
        {
            var rule = SkillBrain.ActiveRule;
            if (rule == null || __0 == null || __0 != GameApi.LocalPlayer) return true;

            if (SkillBrain.CastOnSelf)
            {
                __result = __0;
                return false;
            }
            __result = SkillBrain.PickTarget(__0, __1.castingRange, rule.Target);
            return false;
        }
    }
}
