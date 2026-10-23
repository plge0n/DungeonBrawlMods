using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

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

        /// <summary>补丁前缀用：action 成功时返回 false（跳过原方法），出错时记一次日志并返回 true（照常执行原方法）。</summary>
        public static bool Skip(string where, Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (Exception e)
            {
                Report(where, e);
                return true;
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
            // 主菜单的 Mod 按钮不碰混淆代码，不兼容时也要显示出来告诉玩家
            Safe.Run("主菜单 Mod 按钮", ModMenu.Tick);
            if (!ModKit.Compatible) return;
            // 第一帧：Chainloader 已经加载完 plugins 目录，再加载工坊插件（依赖检查才准）
            Workshop.Start();
            ContentPacks.Start();
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
                RunLog.Info(ModKitPlugin.Name, "房间", players < 0 ? "加入了别人的房间" : lastPlayers < 0 ? "离开了别人的房间" : players > lastPlayers ? "有玩家加入" : "有玩家离开",
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
        private static int version;

        public static void Show(string message, float seconds = 6f)
        {
            text = message;
            until = Time.realtimeSinceStartup + seconds;
            version++;
        }

        // 用 uGUI 画：这个游戏里 OnGUI 画的东西显示不出来（2026-10-22 联机测试发现，组件在、启用着，就是看不到）。
        // 画布是单独的对象（不挂在隐藏的 Host 上），切场景时可能被游戏销毁，下次要显示时重建
        internal class Renderer : MonoBehaviour
        {
            private GameObject root;
            private Text label;
            private int shown = -1;

            private void Update()
            {
                bool visible = text != null && Time.realtimeSinceStartup <= until;
                if (!visible)
                {
                    if (root != null && root.activeSelf) root.SetActive(false);
                    return;
                }
                if (root == null) Build();
                // 游戏的字体（能显示中文、和界面一致）加载好之后换上
                if (NativeUi.Font != null && label.font != NativeUi.Font) label.font = NativeUi.Font;
                if (shown != version)
                {
                    shown = version;
                    label.text = text;
                }
                if (!root.activeSelf) root.SetActive(true);
            }

            private void Build()
            {
                root = new GameObject("DBModKit.ScreenNotice", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                DontDestroyOnLoad(root);
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                // 半透明底框，宽度固定、高度随文字；不挡点击
                var panel = new GameObject("panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                panel.transform.SetParent(root.transform, false);
                var rt = (RectTransform)panel.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -24f);
                rt.sizeDelta = new Vector2(1000f, 0f);
                var bg = panel.GetComponent<Image>();
                bg.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);
                bg.raycastTarget = false;
                var layout = panel.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(24, 24, 12, 12);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var go = new GameObject("text", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(panel.transform, false);
                label = go.GetComponent<Text>();
                // 游戏字体还没加载时先用系统字体（雅黑等能显示中文）
                label.font = NativeUi.Font != null ? NativeUi.Font : Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 26);
                label.fontSize = 26;
                label.color = new Color(0.95f, 0.93f, 0.88f, 1f);
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
                shown = -1;
            }
        }
    }
}
