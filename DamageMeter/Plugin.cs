using BepInEx;
using BepInEx.Configuration;
using DBModKit;

namespace DamageMeter
{
    // 伤害统计：按普攻、技能、召唤物分开统计本波和本局的伤害与 DPS。只看自己打出的伤害，不改游戏，联机也能用
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ModKitPlugin.Guid, "0.13.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.damagemeter";
        public const string Name = "DamageMeter";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.1.0";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> ShowRun;
        internal static ConfigEntry<int> MaxRows;
        internal static ConfigEntry<float> OffsetX;
        internal static ConfigEntry<float> OffsetY;
        internal static ConfigEntry<float> Scale;
        internal static ConfigEntry<float> Opacity;
        internal static ConfigEntry<bool> LogSummary;

        // 必须直接继承 BaseUnityPlugin（不能继承 ModKit 里的类），否则 ScriptEngine 热重载会出错
        private ModContext mod;

        private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);

        private void OnDestroy() => mod?.Dispose();

        private void OnLoad(ModContext mod)
        {
            Enabled = Config.Bind("General", "Enabled", true, "战斗中在屏幕右侧显示伤害统计");
            ToggleKey = Config.Bind("General", "ToggleKey", KeyboardShortcut.Empty, "显示/隐藏面板的快捷键（默认不设）");
            ShowRun = Config.Bind("General", "ShowRun", false, "显示整局的统计（关掉时显示当前波次）；面板上的按钮也能切换");
            LogSummary = Config.Bind("General", "LogSummary", true, "每波结束和本局结束时把统计写进 BepInEx 日志");
            MaxRows = Config.Bind("Panel", "MaxRows", 6,
                new ConfigDescription("最多显示几个伤害来源", new AcceptableValueRange<int>(1, 12)));
            OffsetX = Config.Bind("Panel", "OffsetX", 20f,
                new ConfigDescription("离屏幕右边的距离", new AcceptableValueRange<float>(0f, 1500f)));
            OffsetY = Config.Bind("Panel", "OffsetY", 120f,
                new ConfigDescription("相对屏幕中线往上的距离（负数往下）", new AcceptableValueRange<float>(-500f, 500f)));
            Scale = Config.Bind("Panel", "Scale", 1f,
                new ConfigDescription("面板大小", new AcceptableValueRange<float>(0.5f, 1.5f)));
            Opacity = Config.Bind("Panel", "Opacity", 0.75f,
                new ConfigDescription("背景不透明度", new AcceptableValueRange<float>(0f, 1f)));
            foreach (var entry in new ConfigEntryBase[] { MaxRows, OffsetX, OffsetY, Scale, Opacity })
                Config.SettingChanged += (_, e) =>
                {
                    if (e.ChangedSetting == entry) Panel.Rebuild();
                };

            mod.OnHit(Meter.OnHit);
            mod.OnGameEvent(e =>
            {
                bool midWave = Meter.Wave.Running;
                Meter.OnEvent(e);
                if (!LogSummary.Value) return;
                if (e.Kind == GameEventKind.WaveEnded) Summary.Log(Logger, $"第 {Meter.WaveNumber} 波", Meter.Wave);
                if (e.Kind == GameEventKind.GameOver)
                {
                    // 在波次中途结束时，最后这一波也记一下
                    if (midWave) Summary.Log(Logger, $"第 {Meter.WaveNumber} 波（未打完）", Meter.Wave);
                    Summary.Log(Logger, "本局", Meter.Run);
                }
            });
            mod.OnBattleStarted(Meter.Reset);
            mod.OnUpdate(() =>
            {
                if (ToggleKey.Value.IsDown()) Enabled.Value = !Enabled.Value;
                Panel.Tick();
            });
        }

        private void OnUnload() => Panel.Destroy();
    }
}
