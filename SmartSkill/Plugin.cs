using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DBModKit;

namespace SmartSkill
{
    // 接管游戏自带的"右键技能图标自动释放"：每个技能的释放条件在技能图标上的齿轮弹窗里设定，
    // 也可以在 DBStudio 的 Mod 页里改模式、套推荐
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.8.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.smartskill";
        public const string Name = "SmartSkill";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.7.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> VerboseLog;

        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);

        private void OnDestroy() => mod?.Dispose();

        private void OnLoad(ModContext mod)
        {
            Log = Logger;
            if (!AutoCast.Available) throw new System.Exception("ModKit 的自动释放钩子不可用");

            Enabled = Config.Bind("General", "Enabled", true, "总开关。关闭后完全使用游戏原版自动释放逻辑");
            VerboseLog = Config.Bind("General", "VerboseLog", false, "在 BepInEx 日志中记录自动释放的决策（排查问题时打开）");

            RuleStore.Init(mod.LoadJson<RuleStore.Rules>("SmartSkill.rules.json"));
            mod.UseAutoCast(SkillBrain.Decide);
            mod.OnAutoCastResolved(StatusLog.OnResolved);
            mod.OnHit(KillPredictor.OnHit);
            mod.OnBattleStarted(KillPredictor.Reset);
            mod.OnUpdate(SkillGear.Sync);
            mod.OnUpdate(SkillPopup.Tick);
            mod.OnBattleEnded(SkillPopup.Close);
            // 设置界面（DBStudio 的 Mod 页）里的技能规则列表；没装 DBStudio 时不显示，不影响使用
            mod.AddSettings("每个技能的自动释放规则", SkillSettings.Build, SkillSettings.Refresh);
        }

        // 弹窗和齿轮挂在游戏 UI 上，不会随插件销毁
        private void OnUnload()
        {
            SkillPopup.Close();
            SkillGear.RemoveAll();
        }
    }
}
