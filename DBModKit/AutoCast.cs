using System;
using System.Collections.Generic;

namespace DBModKit
{
    public enum AutoCastAction
    {
        /// <summary>不干预，走游戏原版逻辑。</summary>
        Vanilla,
        /// <summary>这一帧不放。</summary>
        Skip,
        /// <summary>放，目标由 <see cref="AutoCastVerdict.TargetScore"/> 决定（为 null 时打最近的）。</summary>
        Cast,
        /// <summary>放，不要求附近有敌人（仅对无需瞄准的技能有效，其余技能按 Cast 处理）。</summary>
        CastOnSelf,
    }

    /// <summary>自动释放决策。</summary>
    public readonly struct AutoCastVerdict
    {
        public AutoCastAction Action { get; }
        /// <summary>给玩家看的原因，会出现在 <see cref="AutoCastResult"/> 里。</summary>
        public string Reason { get; }
        /// <summary>选目标：施法范围内分数最低的敌人。</summary>
        public Func<Role, float> TargetScore { get; }

        private AutoCastVerdict(AutoCastAction action, string reason, Func<Role, float> targetScore)
        {
            Action = action;
            Reason = reason ?? "";
            TargetScore = targetScore;
        }

        public static AutoCastVerdict Vanilla(string reason = null) => new AutoCastVerdict(AutoCastAction.Vanilla, reason, null);
        public static AutoCastVerdict Skip(string reason = null) => new AutoCastVerdict(AutoCastAction.Skip, reason, null);
        public static AutoCastVerdict Cast(string reason = null, Func<Role, float> targetScore = null) => new AutoCastVerdict(AutoCastAction.Cast, reason, targetScore);
        public static AutoCastVerdict CastOnSelf(string reason = null) => new AutoCastVerdict(AutoCastAction.CastOnSelf, reason, null);
    }

    /// <summary>一次自动释放尝试的结果。</summary>
    public readonly struct AutoCastResult
    {
        public SkillSlot Slot { get; }
        public AutoCastVerdict Verdict { get; }
        /// <summary>技能真正放出去了。Verdict 允许释放但游戏没放，通常是范围内没有目标或角色状态不允许。</summary>
        public bool Casted { get; }

        internal AutoCastResult(SkillSlot slot, AutoCastVerdict verdict, bool casted)
        {
            Slot = slot;
            Verdict = verdict;
            Casted = casted;
        }
    }

    public delegate AutoCastVerdict AutoCastHandler(SkillSlot slot, Player player);

    /// <summary>
    /// 接管游戏"右键技能图标自动释放"。游戏在技能冷却好、蓝够时每帧询问一次是否释放，
    /// 注册的处理器决定放不放、打谁。同一时间只有一个处理器生效：先注册的优先，玩家可以在
    /// Mod 设置 → DBModKit → 依赖和冲突里指定（<see cref="Prefer"/>）；生效的那个注销后自动轮到下一个。
    /// </summary>
    public static class AutoCast
    {
        internal static readonly HandlerSlot<AutoCastHandler> Slot = new HandlerSlot<AutoCastHandler>("AutoCast", "自动释放");

        /// <summary>钩子已安装（ModKit 兼容且补丁成功）。</summary>
        public static bool Available { get; internal set; }

        internal static AutoCastHandler Handler => Slot.Active;

        /// <summary>现在由哪个 Mod 处理（GUID）；没有时为 null。</summary>
        public static string Owner => Slot.Owner;

        /// <summary>注册了处理器的 Mod（GUID，按注册顺序）。</summary>
        public static IReadOnlyList<string> Contenders => Slot.Owners;

        /// <summary>指定由哪个 Mod 处理（玩家的选择，会保存）；null 表示恢复"先注册的优先"。</summary>
        public static void Prefer(string ownerGuid) => Slot.Preferred = ownerGuid;

        /// <summary>每次自动释放尝试后触发（只针对本地玩家、且有处理器时）。</summary>
        public static event Action<AutoCastResult> Resolved;

        /// <summary>注册处理器。已有别的 Mod 注册时排在后面（日志里有提示），同一个 Mod 再注册时替换自己的。</summary>
        public static void SetHandler(string ownerGuid, AutoCastHandler handler) => Slot.Set(ownerGuid, handler);

        public static void ClearHandler(string ownerGuid) => Slot.Clear(ownerGuid);

        // 每个技能上一次的结果：每帧都会询问，只在结果变化或真正放出时记录
        private static readonly Dictionary<string, string> lastResult = new Dictionary<string, string>();

        internal static void RaiseResolved(AutoCastResult result)
        {
            Safe.Run("AutoCast 运行记录", () => LogResult(result));
            var evt = Resolved;
            if (evt != null) Safe.Run("AutoCast.Resolved", () => evt(result));
        }

        private static void LogResult(AutoCastResult r)
        {
            var info = r.Slot?.Info;
            if (info == null) return;
            string text = r.Casted ? "已释放" : r.Verdict.Action == AutoCastAction.Skip ? "不放" : "没放出";
            if (!r.Casted && lastResult.TryGetValue(info.Key, out var last) && last == text + r.Verdict.Reason) return;
            lastResult[info.Key] = text + r.Verdict.Reason;
            RunLog.Debug(ModKitPlugin.Name, "自动释放", $"{info.Name}：{text}", ("原因", r.Verdict.Reason), ("决定", r.Verdict.Action.ToString()), ("处理器", Slot.Owner));
        }
    }
}
