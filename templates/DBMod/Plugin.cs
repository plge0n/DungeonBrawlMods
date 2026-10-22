using BepInEx;
using BepInEx.Configuration;
using DBModKit;

namespace DBModTemplate
{
    // MOD_DESCRIPTION
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, ModKit.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.MOD_GUID_LOWER";
        public const string Name = "DBModTemplate";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.1.0";

        internal static ConfigEntry<bool> Enabled;

        // 必须直接继承 BaseUnityPlugin（不能继承 ModKit 里的类），否则 ScriptEngine 热重载会出错
        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);

        private void OnDestroy() => mod?.Dispose();

        // 通过 mod.OnUpdate / OnBattleStarted / UseAutoCast / LoadJson 等注册的内容，卸载（热重载）时会自动清理
        private void OnLoad(ModContext mod)
        {
            Enabled = Config.Bind("General", "Enabled", true, "总开关");

            mod.OnBattleStarted(() => Logger.LogInfo("进入战斗"));
            mod.OnUpdate(() =>
            {
                if (!Enabled.Value) return;
                var player = Game.LocalPlayer;
                if (player == null) return;
                // TODO
            });
        }

        // 自己创建、挂在游戏对象上的东西在这里销毁
        private void OnUnload()
        {
        }
    }
}
