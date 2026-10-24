using System;
using System.Collections.Generic;

namespace DBModKit
{
    public enum GameEventKind
    {
        /// <summary>本地玩家击杀敌人（Target、IsBoss、IsElite）。</summary>
        EnemyKilled,
        /// <summary>本地玩家放出技能（Skill）。</summary>
        SkillCast,
        /// <summary>本地玩家打出暴击（Amount = 伤害）。</summary>
        Critical,
        /// <summary>本地玩家闪避了攻击。</summary>
        Dodged,
        /// <summary>
        /// 本地玩家掉血（Amount = 掉了多少）。每帧比较血量得出，同一帧的多次伤害合成一次；
        /// 生命上限降低把血量压下来的部分不算（比如体力加成到期）。
        /// </summary>
        Damaged,
        LevelUp,
        /// <summary>本地玩家复活。</summary>
        Revived,
        /// <summary>捡到东西进背包（ItemId = 游戏的 ItemType 编号）。</summary>
        ItemPicked,
        /// <summary>波次开始（Wave）。</summary>
        WaveStarted,
        /// <summary>波次结束、进入休息（Wave）。</summary>
        WaveEnded,
        /// <summary>Boss 出场提示。</summary>
        BossAppeared,
        /// <summary>本局结束。</summary>
        GameOver,
        /// <summary>队友升级（Target = 队友）。</summary>
        TeammateLevelUp,
        /// <summary>本地玩家死亡（Target = 打死你的角色，可能为 null）。复活见 <see cref="Revived"/>。</summary>
        Died,
    }

    /// <summary>一次战斗事件。只包含本地玩家相关的事件（游戏的这些回调只在本地玩家的客户端触发）。</summary>
    public readonly struct GameEvent
    {
        public GameEventKind Kind { get; }
        public Role Target { get; }
        public SkillInfo Skill { get; }
        public long Amount { get; }
        public bool IsBoss { get; }
        public bool IsElite { get; }
        public int ItemId { get; }
        /// <summary>事件发生时的波次。</summary>
        public int Wave { get; }

        internal GameEvent(GameEventKind kind, Role target = null, SkillInfo skill = null, long amount = 0, bool isBoss = false, bool isElite = false, int itemId = 0)
        {
            Kind = kind;
            Target = target;
            Skill = skill;
            Amount = amount;
            IsBoss = isBoss;
            IsElite = isElite;
            ItemId = itemId;
            Wave = Game.Wave;
        }
    }

    /// <summary>战斗事件。订阅者各自做异常保护。用 <see cref="ModContext.OnGameEvent"/> 订阅可以在卸载时自动退订。</summary>
    public static class GameEvents
    {
        /// <summary>任意战斗事件。</summary>
        public static event Action<GameEvent> Raised;

        /// <summary>波次相关事件（WaveStarted、WaveEnded、BossAppeared、GameOver）的钩子已安装。</summary>
        public static bool WaveEventsAvailable { get; internal set; }

        private static object binding;
        private static UnityEngine.Component boundPlayer;
        private static long lastHp = -1, lastMaxHp = -1;

        internal static void Raise(GameEvent e)
        {
            RunLog.Debug(ModKitPlugin.Name, "事件", e.Kind.ToString(), Describe(e));
            var evt = Raised;
            if (evt == null) return;
            foreach (Action<GameEvent> handler in evt.GetInvocationList())
                Safe.Run($"GameEvents.{e.Kind} → {handler.Method.DeclaringType?.FullName}.{handler.Method.Name}", () => handler(e));
        }

        private static (string, object)[] Describe(GameEvent e)
        {
            var d = new List<(string, object)>();
            if (e.Target != null) d.Add(("目标", e.Target.IsBoss ? "Boss" : e.Target.IsElite ? "精英" : "普通"));
            if (e.Skill != null) d.Add(("技能", e.Skill.Name));
            if (e.Amount != 0) d.Add(("数值", e.Amount));
            if (e.ItemId != 0) d.Add(("物品", e.ItemId));
            if (e.Kind == GameEventKind.Damaged && GameRaw.LocalPlayer() is UnityEngine.Component p) d.Add(("生命", $"{GameRaw.Hp(p)}/{GameRaw.MaxHp(p)}"));
            return d.ToArray();
        }

        // 每帧（Runner）：本地玩家换了就重新挂游戏的回调；比较血量得出掉血事件
        internal static void Tick()
        {
            var player = GameRaw.LocalPlayer();
            if (player != boundPlayer)
            {
                if (binding != null) Safe.Run("GameEvents 解绑", () => GameRaw.UnbindPlayerEvents(binding));
                binding = null;
                boundPlayer = player;
                lastHp = -1;
                if (player != null) Safe.Run("GameEvents 绑定", () => binding = GameRaw.BindPlayerEvents(player));
            }
            if (player == null) return;

            long hp = GameRaw.Hp(player), maxHp = GameRaw.MaxHp(player);
            if (lastHp >= 0 && hp < lastHp)
            {
                // 上限降低时血量会被压到新上限，这部分不是受伤
                long capped = maxHp < lastMaxHp ? Math.Max(0L, lastHp - maxHp) : 0L;
                long lost = lastHp - hp - capped;
                if (lost > 0) Raise(new GameEvent(GameEventKind.Damaged, amount: lost));
            }
            lastHp = hp;
            lastMaxHp = maxHp;
        }
    }
}
