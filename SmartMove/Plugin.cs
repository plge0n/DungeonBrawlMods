using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DBModKit;
using UnityEngine;

namespace SmartMove
{
    // 没按方向键时自动躲避地面预警（敌人技能、陷阱）
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.11.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.smartmove";
        public const string Name = "SmartMove";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.3.0";

        internal static ManualLogSource Log;
        internal static ModRunLog RunLogger;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> VerboseLog;
        internal static ConfigEntry<bool> WithoutAutoBattle;
        internal static ConfigEntry<float> Margin;

        // 必须直接继承 BaseUnityPlugin（不能继承 ModKit 里的类），否则 ScriptEngine 热重载会出错
        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);

        private void OnDestroy() => mod?.Dispose();

        private void OnLoad(ModContext mod)
        {
            Log = Logger;
            RunLogger = mod.RunLog;
            if (!MoveControl.Available) throw new System.Exception("ModKit 的走位钩子不可用");

            Enabled = Config.Bind("General", "Enabled", true, "躲避地面红色预警（敌人技能、陷阱）");
            ToggleKey = Config.Bind("General", "DodgeToggleKey", new KeyboardShortcut(KeyCode.F8), "开关躲避的快捷键");
            MigrateToggleKey();
            VerboseLog = Config.Bind("General", "VerboseLog", false, "在 BepInEx 日志中记录躲避决策（排查问题时打开）");
            WithoutAutoBattle = Config.Bind("Dodge", "WithoutAutoBattle", true, "没开自动战斗时也躲（只在没按方向键时生效）");
            Margin = Config.Bind("Dodge", "Margin", 0.6f,
                new ConfigDescription("躲避时离预警边缘多留的距离（米），联机延迟高时调大", new AcceptableValueRange<float>(0f, 3f)));

            mod.UseMovement(Planner.Decide);
            mod.OnBattleEnded(Planner.Reset);
            mod.OnUpdate(() =>
            {
                if (!ToggleKey.Value.IsDown()) return;
                Enabled.Value = !Enabled.Value;
                ScreenNotice.Show(Enabled.Value ? "SmartMove：自动躲避已开启" : "SmartMove：自动躲避已关闭", 3f);
                RunLogger.Info("开关", Enabled.Value ? "自动躲避已开启" : "自动躲避已关闭");
            });
        }

        // 0.2.x 的快捷键是 General.ToggleKey，默认 F9，和 DBStudio 打开工作台撞键。0.3.0 换成新键 DodgeToggleKey（默认 F8）：
        // 玩家自己改过的旧键搬过来，还是 F9 的就用新默认值；旧键从配置文件里删掉
        private void MigrateToggleKey()
        {
            var old = new ConfigDefinition("General", "ToggleKey");
            var entry = Config.Bind(old, new KeyboardShortcut(KeyCode.F9));
            var oldKey = entry.Value;
            Config.Remove(old);
            Config.Save();
            if (oldKey.MainKey == KeyCode.F9 && !oldKey.Modifiers.Any()) return;
            ToggleKey.Value = oldKey;
            RunLogger.Info("配置", "沿用旧的快捷键", ("快捷键", oldKey.ToString()));
        }

        private void OnUnload()
        {
        }
    }
}
