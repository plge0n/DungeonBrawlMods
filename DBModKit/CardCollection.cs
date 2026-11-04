using System;
using BepInEx.Configuration;

namespace DBModKit
{
    /// <summary>卡牌收藏测试。在 Unity 主线程调用。卡牌没有单卡等级，全解锁表示库存至少 7、记忆容量 50。</summary>
    public static class CardCollection
    {
        private static ConfigEntry<bool> enabled;
        public static bool Available { get; internal set; }
        public static string UnavailableReason { get; internal set; } = "卡牌保护补丁尚未安装";
        public static string LastError { get; private set; }
        public static bool Ready
        {
            get
            {
                try { return Available && ModKit.Compatible && GameRaw.CardSaveReady(); }
                catch (Exception e) { Fail(e.Message); return false; }
            }
        }

        public static string Summary
        {
            get
            {
                try { return Ready ? GameRaw.CardCollectionSummary() : "存档未加载"; }
                catch (Exception e) { Fail(e.Message); return "读取失败"; }
            }
        }

        /// <summary>持久化的运行时开关，默认关闭。战斗中切换只影响下一局。卡组改动在关闭时恢复。</summary>
        public static bool Enabled
        {
            get => enabled?.Value ?? false;
            set => SetEnabled(value);
        }

        internal static void Configure(ConfigFile config)
            => enabled = config.Bind("Cards", "UnlockWithoutSaving", false, "卡牌全解锁（不改存档）。关闭恢复收藏和卡组，战斗中切换只影响下一局。开着时不能合成、分解、升级容量。");

        public static bool SetEnabled(bool value)
        {
            LastError = null;
            if (!Available || !ModKit.Compatible || enabled == null) return Fail(UnavailableReason ?? "卡牌配置尚未初始化");
            bool old = Enabled;
            try
            {
                if (value) GameRaw.EnableCardUnlock();
                else GameRaw.DisableCardUnlock();
                enabled.Value = value;
                Safe.Run("卡牌界面刷新", GameRaw.RefreshCardCollection);
                RunLog.Info(ModKitPlugin.Name, "卡牌", "运行时全解锁切换", ("原值", old), ("新值", value));
                return true;
            }
            catch (Exception e) { return Fail(e.ToString()); }
        }

        internal static void Tick()
        {
            if (!Available) return;
            try
            {
                if (Enabled) GameRaw.EnableCardUnlock();
                else GameRaw.DisableCardUnlock();
            }
            catch (Exception e)
            {
                // 保留保存保护补丁，先恢复真实数据，再停掉开关。
                GameRaw.DisableCardUnlock();
                enabled.Value = false;
                Fail(e.ToString());
            }
        }

        /// <summary>局外永久写入。调用者应先向玩家展示永久性、Steam 云同步和备份恢复的确认说明。</summary>
        public static bool WritePermanently(out string backupPath)
        {
            backupPath = null;
            LastError = null;
            try
            {
                if (!Ready || Game.InBattle) return Fail("永久写入仅在局外且存档已加载时可用");
                // 全部复制成功才允许改变内存。独占的 save.json.lock 通过游戏已持有的流复制。
                backupPath = GameRaw.BackupCardSave();
                if (!SetEnabled(false)) return false;
                if (!GameRaw.WriteCardsPermanently()) return Fail("游戏保存失败，请查看游戏日志。备份位于 " + backupPath);
                Safe.Run("卡牌界面刷新", GameRaw.RefreshCardCollection);
                RunLog.Info(ModKitPlugin.Name, "卡牌", "永久全解锁完成", ("备份", backupPath));
                return true;
            }
            catch (Exception e) { return Fail(e.ToString()); }
        }

        internal static bool BlockCrafting()
        {
            if (!Available || !Enabled) return false;
            ScreenNotice.Show("卡牌全解锁开着时不能合成/分解/升级容量");
            RunLog.Info(ModKitPlugin.Name, "卡牌", "拦截合成、分解或升级容量");
            return true;
        }

        internal static bool Fail(string message)
        {
            LastError = message;
            RunLog.Error(ModKitPlugin.Name, "卡牌", message);
            return false;
        }
    }
}
