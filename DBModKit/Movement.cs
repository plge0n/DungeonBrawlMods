using System.Collections.Generic;
using UnityEngine;

namespace DBModKit
{
    /// <summary>移动处理器被询问时的情况。</summary>
    public readonly struct MoveContext
    {
        public Player Player { get; }
        /// <summary>
        /// 正在普攻动作中。此时返回移动会打断攻击：出手前打断会丢掉这一下（冷却立刻转好），
        /// 出手后打断只是提前结束收招（见 <see cref="Player.AttackHitDone"/>）。
        /// </summary>
        public bool InAttack { get; }

        internal MoveContext(Player player, bool inAttack)
        {
            Player = player;
            InAttack = inAttack;
        }
    }

    /// <summary>移动决策。</summary>
    public readonly struct MoveCommand
    {
        /// <summary>为 false 时不干预，走游戏原版逻辑（自动战斗、普攻等）。</summary>
        public bool Override { get; }
        /// <summary>水平移动方向（单位向量）。</summary>
        public Vector3 Direction { get; }
        public string Reason { get; }

        private MoveCommand(bool @override, Vector3 direction, string reason)
        {
            Override = @override;
            Direction = direction;
            Reason = reason ?? "";
        }

        public static MoveCommand None => default;

        public static MoveCommand Move(Vector3 direction, string reason = null)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 1e-6f ? None : new MoveCommand(true, direction.normalized, reason);
        }
    }

    public delegate MoveCommand MoveHandler(MoveContext context);

    /// <summary>
    /// 接管本地玩家没有移动输入时的走位：处理器每帧返回一个方向，效果等同于玩家按住这个方向键
    /// （游戏自己的转向、移速、碰撞、打断攻击逻辑都照常生效）。玩家一按方向键就完全听玩家的。
    /// 同一时间只有一个处理器生效，规则和 <see cref="AutoCast"/> 一样：先注册的优先，玩家可以指定（<see cref="Prefer"/>）。
    /// </summary>
    public static class MoveControl
    {
        internal static readonly HandlerSlot<MoveHandler> Slot = new HandlerSlot<MoveHandler>("MoveControl", "走位");

        /// <summary>钩子已安装。</summary>
        public static bool Available { get; internal set; }

        internal static MoveHandler Handler => Slot.Active;

        /// <summary>现在由哪个 Mod 处理（GUID）；没有时为 null。</summary>
        public static string Owner => Slot.Owner;

        /// <summary>注册了处理器的 Mod（GUID，按注册顺序）。</summary>
        public static IReadOnlyList<string> Contenders => Slot.Owners;

        /// <summary>指定由哪个 Mod 处理（玩家的选择，会保存）；null 表示恢复"先注册的优先"。</summary>
        public static void Prefer(string ownerGuid) => Slot.Preferred = ownerGuid;

        /// <summary>上一次生效的移动指令（调试用）。</summary>
        public static MoveCommand Last { get; internal set; }

        /// <summary>注册处理器。已有别的 Mod 注册时排在后面，同一个 Mod 再注册时替换自己的。</summary>
        public static void SetHandler(string ownerGuid, MoveHandler handler) => Slot.Set(ownerGuid, handler);

        public static void ClearHandler(string ownerGuid) => Slot.Clear(ownerGuid);
    }
}
