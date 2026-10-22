using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DBModKit;

namespace SmartSkill
{
    // 接管游戏自带的"右键技能图标自动释放"：每个技能的释放条件在技能图标上的齿轮弹窗里设定
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.smartskill";
        public const string Name = "SmartSkill";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.5.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> VerboseLog;

        private void Awake()
        {
            Log = Logger;

            // ModKit 不兼容时它自己会在屏幕上提示，这里只需不启动
            if (!ModKit.Compatible || !AutoCast.Available)
            {
                Log.LogWarning($"{Name} 未启动：ModKit 不兼容或自动释放钩子不可用");
                return;
            }

            Enabled = Config.Bind("General", "Enabled", true, "总开关。关闭后完全使用游戏原版自动释放逻辑");
            VerboseLog = Config.Bind("General", "VerboseLog", false, "在 BepInEx 日志中记录自动释放的决策（排查问题时打开）");

            RuleStore.Load();
            AutoCast.SetHandler(Guid, SkillBrain.Decide);
            AutoCast.Resolved += StatusLog.OnResolved;
            ModEvents.Update += SkillGear.Sync;
            ModEvents.Update += SkillPopup.Tick;
            ModEvents.BattleEnded += SkillPopup.Close;
            Log.LogInfo($"{Name} {Version} 已加载");
        }
    }
}
