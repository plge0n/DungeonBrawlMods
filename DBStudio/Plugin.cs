using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DBModKit;
using UnityEngine;

namespace DBStudio
{
    // 游戏内的 Mod 工作台：调试面板、规则、强化、数值编辑器、英雄皮肤
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.8.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.studio";
        public const string Name = "DBStudio";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.6.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<KeyboardShortcut> Hotkey;
        internal static ConfigEntry<bool> PauseWhenSolo;

        // 必须直接继承 BaseUnityPlugin（不能继承 ModKit 里的类），否则 ScriptEngine 热重载会出错
        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);

        private void OnDestroy() => mod?.Dispose();

        private void OnLoad(ModContext mod)
        {
            Log = Logger;
            Hotkey = Config.Bind("General", "Hotkey", new KeyboardShortcut(KeyCode.F10), "打开/关闭工作台（F9 是 SmartMove 的开关）");
            PauseWhenSolo = Config.Bind("General", "PauseWhenSolo", true, "单人战斗中打开工作台时暂停游戏（联机时不会暂停）");

            Studio.Init(new Pages.DebugPage(), new Pages.RulesPage(), new Pages.AugmentsPage(), new Pages.ValuesPage(), new Pages.SkinsPage(), new Pages.ModsPage());
            EventLog.Attach(mod);
            Packs.Init(mod);
            RuleEngine.Attach(mod);
            AugmentEngine.Attach(mod);
            Skins.Init(mod);
            mod.OnUpdate(Studio.Tick);
        }

        // 窗口挂在自己的 Canvas 上，不会随插件销毁；关窗口同时释放输入屏蔽和暂停
        private void OnUnload() => Studio.Shutdown();
    }
}
