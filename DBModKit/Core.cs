using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace DBModKit
{
    internal static class Log
    {
        internal static ManualLogSource Source;

        // 同时进 BepInEx 日志和运行记录
        public static void Info(string msg) { Source?.LogInfo(msg); RunLog.WriteCore(ModKitPlugin.Name, "", RunLogLevel.Info, msg, null, mirror: false); }
        public static void Warning(string msg) { Source?.LogWarning(msg); RunLog.WriteCore(ModKitPlugin.Name, "", RunLogLevel.Warning, msg, null, mirror: false); }
        public static void Error(string msg) { Source?.LogError(msg); RunLog.WriteCore(ModKitPlugin.Name, "", RunLogLevel.Error, msg, null, mirror: false); }
    }

    /// <summary>游戏生命周期事件。每个订阅者单独做异常保护：一个 Mod 出错不影响其他 Mod，同一处错误只记一次日志。</summary>
    public static class ModEvents
    {
        /// <summary>每帧。ModKit 不兼容时不触发。</summary>
        public static event Action Update;

        /// <summary>本地玩家出现（进入战斗）。</summary>
        public static event Action BattleStarted;

        /// <summary>本地玩家消失（离开战斗）。</summary>
        public static event Action BattleEnded;

        /// <summary>游戏退出，适合落盘。</summary>
        public static event Action Quitting;

        /// <summary>
        /// 游戏拍下了重试波次的快照（单人时每次进入休息阶段，早于这一波的遗物选择）。
        /// 游戏只还原它自己的对象（英雄、技能实例、遗物等），Mod 自己的状态要在这里存一份。
        /// </summary>
        public static event Action CheckpointSaved;

        /// <summary>玩家点了"重试"，游戏已经还原到快照（随后会重新弹出这一波的遗物选择）。Mod 在这里还原自己的状态。</summary>
        public static event Action CheckpointRestored;

        internal static void RaiseUpdate() => Safe.InvokeEach(Update, nameof(Update));
        internal static void RaiseCheckpointSaved() => Safe.InvokeEach(CheckpointSaved, nameof(CheckpointSaved));
        internal static void RaiseCheckpointRestored() => Safe.InvokeEach(CheckpointRestored, nameof(CheckpointRestored));
        internal static void RaiseBattleStarted() => Safe.InvokeEach(BattleStarted, nameof(BattleStarted));
        internal static void RaiseBattleEnded() => Safe.InvokeEach(BattleEnded, nameof(BattleEnded));
        internal static void RaiseQuitting() => Safe.InvokeEach(Quitting, nameof(Quitting));
    }

    internal static class Safe
    {
        private static readonly HashSet<string> reported = new HashSet<string>();

        public static void InvokeEach(Action evt, string name)
        {
            if (evt == null) return;
            foreach (Action handler in evt.GetInvocationList())
                Run($"{name} → {handler.Method.DeclaringType?.FullName}.{handler.Method.Name}", handler);
        }

        public static void InvokeEach<T>(Action<T> evt, string name, T arg)
        {
            if (evt == null) return;
            foreach (Action<T> handler in evt.GetInvocationList())
                Run($"{name} → {handler.Method.DeclaringType?.FullName}.{handler.Method.Name}", () => handler(arg));
        }

        public static void Run(string where, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Report(where, e);
            }
        }

        public static void Report(string where, Exception e)
        {
            if (reported.Add(where)) Log.Error($"{where} 出错（同一处只记一次）：{e}");
        }
    }

    // 不依赖 BepInEx 的管理对象跑每帧逻辑：游戏切场景时会把它销毁
    internal static class Host
    {
        public static GameObject Object { get; private set; }

        public static void Create()
        {
            if (Object != null) return;
            Object = new GameObject("DBModKit.Host") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(Object);
            Object.AddComponent<Runner>();
            Object.AddComponent<ScreenNotice.Renderer>();
        }
    }

    internal class Runner : MonoBehaviour
    {
        private bool wasInBattle;
        private int lastPlayers = 1;

        private void Update()
        {
            GamePause.Tick();
            RunLog.Tick();
            if (!ModKit.Compatible) return;
            // 第一帧：Chainloader 已经加载完 plugins 目录，再加载工坊插件（依赖检查才准）
            Workshop.Start();
            Safe.Run("联机握手", Multiplayer.Tick);
            bool inBattle = InBattle();
            if (inBattle != wasInBattle)
            {
                wasInBattle = inBattle;
                if (inBattle)
                {
                    Safe.Run("运行记录（进入战斗）", LogBattleStarted);
                    // gad 的补丁没装上时，怪物行在主菜单还不存在，进入战斗时补写倍率
                    Safe.Run("怪物倍率（进入战斗）", GameData.OnEnemyTableLoaded);
                    Safe.Run("联机握手（锁定）", Multiplayer.Lock);
                    ModEvents.RaiseBattleStarted();
                }
                else
                {
                    RunLog.Info(ModKitPlugin.Name, "战斗", "离开战斗");
                    Multiplayer.Unlock();
                    ModEvents.RaiseBattleEnded();
                }
            }
            int players = Game.PlayerCount;
            if (players != lastPlayers)
            {
                RunLog.Info(ModKitPlugin.Name, "房间", players < 0 ? "加入了别人的房间" : players > lastPlayers ? "有玩家加入" : "有玩家离开",
                    ("人数", players), ("单人", Game.IsSolo));
                lastPlayers = players;
            }
            Safe.Run("GameEvents", GameEvents.Tick);
            if (SkillTuning.Available) Safe.Run("技能调整", SkillTuning.Tick);
            Safe.Run("技能提示", SkillTooltips.Tick);
            Safe.Run("语言", Localization.Tick);
            ModEvents.RaiseUpdate();
            Safe.Run("皮肤同步", HeroSkinSync.Tick);
            JsonConfigRegistry.Tick();
        }

        private static void LogBattleStarted()
        {
            var p = Game.LocalPlayer;
            RunLog.Info(ModKitPlugin.Name, "战斗", "进入战斗",
                ("单人", Game.IsSolo), ("人数", Game.PlayerCount), ("生命", p.MaxHp), ("蓝", p.MaxMana), ("攻击", p.Attack),
                ("力/敏/体", $"{p.Strength}/{p.Agility}/{p.Stamina}"), ("自动战斗", Game.AutoBattle));
        }

        private void OnApplicationQuit()
        {
            ModEvents.RaiseQuitting();
            JsonConfigRegistry.FlushAll();
        }

        // 主菜单阶段游戏的管理器可能还没创建，读取本地玩家会抛异常
        private static bool InBattle()
        {
            try
            {
                return Game.LocalPlayer != null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>屏幕顶部的临时提示。不依赖游戏代码，ModKit 不兼容时也能用。</summary>
    public static class ScreenNotice
    {
        private static string text;
        private static float until;

        public static void Show(string message, float seconds = 6f)
        {
            text = message;
            until = Time.realtimeSinceStartup + seconds;
        }

        internal class Renderer : MonoBehaviour
        {
            private GUIStyle style;

            private void OnGUI()
            {
                if (text == null || Time.realtimeSinceStartup > until) return;
                style ??= new GUIStyle(GUI.skin.box) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                float w = Mathf.Min(900f, Screen.width - 40f);
                GUI.Box(new Rect((Screen.width - w) / 2f, 20f, w, 60f), text, style);
            }
        }
    }
}
