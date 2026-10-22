using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace DBModKit
{
    internal static class Log
    {
        internal static ManualLogSource Source;

        public static void Info(string msg) => Source?.LogInfo(msg);
        public static void Warning(string msg) => Source?.LogWarning(msg);
        public static void Error(string msg) => Source?.LogError(msg);
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

        internal static void RaiseUpdate() => Safe.InvokeEach(Update, nameof(Update));
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

        private void Update()
        {
            if (!ModKit.Compatible) return;
            bool inBattle = InBattle();
            if (inBattle != wasInBattle)
            {
                wasInBattle = inBattle;
                if (inBattle) ModEvents.RaiseBattleStarted();
                else ModEvents.RaiseBattleEnded();
            }
            ModEvents.RaiseUpdate();
            JsonConfigRegistry.Tick();
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
