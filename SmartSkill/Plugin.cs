using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SmartSkill
{
    // 接管游戏自带的"右键技能图标自动释放"：每个技能的释放条件在技能图标上的齿轮弹窗里设定
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dry.dungeonbrawl.smartskill";
        public const string Name = "SmartSkill";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.4.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> VerboseLog;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true, "总开关。关闭后完全使用游戏原版自动释放逻辑");
            VerboseLog = Config.Bind("General", "VerboseLog", false, "在 BepInEx 日志中记录自动释放的决策（排查问题时打开）");

            // 不依赖 BepInEx 的管理对象跑每帧逻辑：游戏切场景时会把它销毁
            var host = new GameObject("SmartSkill.Runner") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);

            var missing = Compat.FindMissing();
            if (missing.Count > 0)
            {
                Log.LogError($"游戏版本 {Compat.GameVersion} 与 SmartSkill {Version} 不兼容，已停用。缺失：{string.Join("，", missing)}");
                host.AddComponent<Notice>().Show(
                    $"SmartSkill {Version} 已停用：游戏版本 {Compat.GameVersion} 与 Mod 不兼容，请等待 Mod 更新。", 20f);
                return;
            }
            if (!Compat.IsTestedVersion)
            {
                Log.LogWarning($"游戏版本 {Compat.GameVersion} 未经测试（测试过：{string.Join("、", Compat.TestedVersions)}），如遇异常请关闭 Mod");
                host.AddComponent<Notice>().Show(
                    $"SmartSkill {Version} 未在游戏版本 {Compat.GameVersion} 上测试过，如遇异常请在 BepInEx/config 中关闭。", 8f);
            }

            RuleStore.Load(Paths.ConfigPath);
            new Harmony(Guid).PatchAll();
            host.AddComponent<Runner>();
            Log.LogInfo($"{Name} {Version} 已加载（游戏版本 {Compat.GameVersion}）");
        }
    }

    internal class Runner : MonoBehaviour
    {
        // 每帧逻辑出错时只记一次日志，避免刷屏
        private readonly HashSet<string> failed = new HashSet<string>();

        private void Update()
        {
            RuleStore.Tick();
            Guard("SkillGear", SkillGear.Sync);
            Guard("SkillPopup", SkillPopup.Tick);
        }

        private void OnApplicationQuit() => RuleStore.Flush();

        private void Guard(string name, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                if (failed.Add(name)) Plugin.Log.LogError($"{name} 出错：{e}");
            }
        }
    }

    // 屏幕顶部的临时提示；不引用任何游戏类型，Mod 不兼容时也能用
    internal class Notice : MonoBehaviour
    {
        private string text;
        private float until;
        private GUIStyle style;

        public void Show(string message, float seconds)
        {
            text = message;
            until = Time.realtimeSinceStartup + seconds;
        }

        private void OnGUI()
        {
            if (text == null || Time.realtimeSinceStartup > until) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            float w = Mathf.Min(900f, Screen.width - 40f);
            GUI.Box(new Rect((Screen.width - w) / 2f, 20f, w, 60f), text, style);
        }
    }
}
