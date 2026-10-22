using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DBModKit
{
    // Harmony 补丁统一在这里按签名查找并安装；找不到或安装失败只停用对应功能，不影响其他部分
    internal static class Hooks
    {
        /// <summary>安装补丁。排行榜拦截装不上时返回 false，ModKit 整体停用。</summary>
        public static bool Install(Harmony harmony)
        {
            if (!TryPatch(harmony, typeof(LeaderboardHooks), "排行榜上传拦截",
                    AccessTools.Method(typeof(bwu), "quj", new[]
                    {
                        typeof(int), typeof(int), typeof(de.TeamBuildData), typeof(Action<bws>),
                        typeof(Action<LeaderboardUploadStage>), typeof(string),
                    }),
                    prefix: nameof(LeaderboardHooks.UploadPrefix)))
                return false;

            AutoCast.Available =
                TryPatch(harmony, typeof(AutoCastHooks), "自动释放决策",
                    AccessTools.Method(typeof(ez), "dxx", new[] { typeof(ActiveSkillEnum), typeof(bbe), typeof(int) }),
                    prefix: nameof(AutoCastHooks.DecidePrefix), postfix: nameof(AutoCastHooks.DecidePostfix))
                && TryPatch(harmony, typeof(AutoCastHooks), "自动释放选目标",
                    AccessTools.Method(typeof(ez), "dxy", new[] { typeof(bbe), typeof(ActiveSkillData) }),
                    prefix: nameof(AutoCastHooks.TargetPrefix));
            // 开关技能开着时，自动释放直接把它关掉，不经过 dxx
            if (AutoCast.Available)
                TryPatch(harmony, typeof(AutoCastHooks), "开关技能自动关闭",
                    AccessTools.Method(typeof(bht), "kcac92c2266be41d3979b385715ce318e", new Type[0]),
                    prefix: nameof(AutoCastHooks.TogglePrefix), postfix: nameof(AutoCastHooks.TogglePostfix));

            MoveControl.Available =
                TryPatch(harmony, typeof(MoveHooks), "走位（待机/移动）",
                    AccessTools.Method(typeof(bbe), "kqi", new[] { typeof(bool) }),
                    prefix: nameof(MoveHooks.Prefix), postfix: nameof(MoveHooks.Postfix))
                && TryPatch(harmony, typeof(MoveHooks), "走位（攻击中）",
                    AccessTools.Method(typeof(bbe), "kqh", new Type[0]),
                    prefix: nameof(MoveHooks.AttackPrefix), postfix: nameof(MoveHooks.Postfix));

            try
            {
                Combat.Install();
            }
            catch (Exception e)
            {
                Log.Error($"命中事件：订阅失败，该功能停用：{e}");
            }
            return true;
        }

        private static bool TryPatch(Harmony harmony, Type hooks, string feature, MethodBase target, string prefix = null, string postfix = null)
        {
            if (target == null)
            {
                Log.Error($"{feature}：找不到目标方法，该功能停用");
                return false;
            }
            try
            {
                harmony.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(hooks, prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(hooks, postfix));
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"{feature}：补丁安装失败，该功能停用：{e}");
                return false;
            }
        }
    }

    // 游戏作者的规则：装了 Mod 不能上传排行榜（违规可能被拉进按 Steam 账号判定的黑名单，连累整队）。
    // bwu.quj 是排行榜上传入口（由主机 MyServerNetworkManager 调用；bwu.nlq 是同签名的诱饵）。
    // 强制拦截，不提供开关；按游戏自己的失败流程回调，结算界面会显示上传失败。
    internal static class LeaderboardHooks
    {
        private static readonly MethodInfo makeFailure =
            AccessTools.Method(typeof(bwu), "qut", new[] { typeof(LeaderboardUploadFailure), typeof(string) });

        internal static bool UploadPrefix(Action<bws> __3)
        {
            Log.Warning("已安装 Mod：按游戏作者的规则，本局成绩不上传排行榜");
            ScreenNotice.Show("已安装 Mod：按游戏作者的规则，本局成绩不上传排行榜。", 8f);
            if (__3 != null)
                Safe.Run("排行榜拦截回调", () =>
                    __3((bws)makeFailure.Invoke(null, new object[] { LeaderboardUploadFailure.InvalidData, "Mods installed: leaderboard upload disabled." })));
            return false;
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

        // bht.kcac92…()：bpf.opk 每帧在冷却好时调用的自动释放入口。开关技能开着（bhnv）时它会直接关掉技能，
        // 这里先问处理器：Skip = 保持开着，其余 = 照原版关掉
        internal static bool TogglePrefix(bht __instance, ref bool __result, out State? __state)
        {
            __state = null;
            var handler = AutoCast.Handler;
            var native = __instance.bhng?.bjgk;
            if (handler == null || native == null || !native.bhnv || __instance.bhnd == null || __instance.bhnd != GameRaw.LocalPlayer())
                return true;

            var slot = SkillSlot.Wrap(native);
            var player = Game.LocalPlayer;
            if (slot == null || player == null) return true;

            AutoCastVerdict verdict;
            try
            {
                verdict = handler(slot, player);
            }
            catch (Exception e)
            {
                Safe.Report("AutoCast 处理器（开关技能）", e);
                return true;
            }
            __state = new State { Slot = slot, Verdict = verdict };
            if (verdict.Action != AutoCastAction.Skip) return true;
            __result = false;
            return false;
        }

        internal static void TogglePostfix(bool __result, State? __state)
        {
            if (__state is State s) AutoCast.RaiseResolved(new AutoCastResult(s.Slot, s.Verdict, __result));
        }
    }

    // bbe.kqi(bool)：待机/移动状态每帧的控制逻辑（含官方自动战斗）；bbe.kqh()：攻击状态每帧，玩家有移动输入时会打断攻击。
    // 两者都从 os.bctl / os.bctm 读移动输入。处理器要移动时，在调用期间把方向写进输入、结束后还原，
    // 游戏就会按"玩家按住方向键"处理：转向、移速、碰撞、打断攻击都走原版逻辑。
    internal static class MoveHooks
    {
        internal struct Saved
        {
            public float X, Z;
        }

        internal static void Prefix(bbe __instance, bool __0, out Saved? __state)
        {
            __state = __0 ? Inject(__instance, false) : null;
        }

        internal static void AttackPrefix(bbe __instance, out Saved? __state)
        {
            __state = Inject(__instance, true);
        }

        internal static void Postfix(Saved? __state)
        {
            if (__state is Saved s)
            {
                os.bctl = s.X;
                os.bctm = s.Z;
            }
        }

        private static Saved? Inject(bbe player, bool inAttack)
        {
            var handler = MoveControl.Handler;
            if (handler == null || player == null || player != GameRaw.LocalPlayer()) return null;
            if (player.roleType == RoleType.King || nn.bbtm) return null;
            // 玩家自己在操作
            if (!Mathf.Approximately(os.bctl, 0f) || !Mathf.Approximately(os.bctm, 0f)) return null;

            var p = Game.LocalPlayer;
            if (p == null) return null;
            MoveCommand cmd;
            try
            {
                cmd = handler(new MoveContext(p, inAttack));
            }
            catch (Exception e)
            {
                Safe.Report("移动处理器", e);
                return null;
            }
            if (!cmd.Override) return null;

            MoveControl.Last = cmd;
            var saved = new Saved { X = os.bctl, Z = os.bctm };
            os.bctl = cmd.Direction.x;
            os.bctm = cmd.Direction.z;
            return saved;
        }
    }
}
