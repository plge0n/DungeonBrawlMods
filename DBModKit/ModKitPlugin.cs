using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace DBModKit
{
    /// <summary>地下城竞技场 Mod 依赖库。其他 Mod 在 Awake 里调用 <see cref="ModContext.Start"/>，并用 [BepInDependency(ModKitPlugin.Guid)] 声明依赖。</summary>
    [BepInPlugin(Guid, Name, Version)]
    public class ModKitPlugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.modkit";
        public const string Name = "DBModKit";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.18.0";

        private void Awake()
        {
            Log.Source ??= Logger;
            RunLog.Configure(Config);
            ModKit.EnsureInitialized();
        }
    }

    /// <summary>ModKit 的运行状态。</summary>
    public static class ModKit
    {
        public const string Version = ModKitPlugin.Version;

        private static bool initialized;
        private static bool compatible;

        /// <summary>
        /// 当前游戏版本与 ModKit 兼容；为 false 时不要调用任何游戏相关接口。
        /// 第一次访问时完成初始化，所以 ScriptEngine 先于 ModKit 加载 Mod 时也能正常使用。
        /// </summary>
        public static bool Compatible
        {
            get
            {
                EnsureInitialized();
                return compatible;
            }
        }

        public static string GameVersion => Application.version;

        /// <summary>不兼容时缺失的游戏成员。</summary>
        public static IReadOnlyList<string> MissingMembers { get; private set; } = new string[0];

        internal static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            Log.Source ??= BepInEx.Logging.Logger.CreateLogSource(ModKitPlugin.Name);
            Host.Create();

            var missing = Compat.FindMissing();
            if (missing.Count > 0)
            {
                MissingMembers = missing;
                Log.Error($"游戏版本 {GameVersion} 与 {ModKitPlugin.Name} {Version} 不兼容，所有依赖 ModKit 的 Mod 已停用。缺失：{string.Join("，", missing)}");
                ScreenNotice.Show($"{ModKitPlugin.Name} {Version} 与游戏版本 {GameVersion} 不兼容，相关 Mod 已停用，请等待更新。", 20f);
                return;
            }
            if (!Compat.IsTestedVersion)
            {
                Log.Warning($"游戏版本 {GameVersion} 未经测试（测试过：{string.Join("、", Compat.TestedVersions)}）");
                ScreenNotice.Show($"{ModKitPlugin.Name} 未在游戏版本 {GameVersion} 上测试过，如遇异常请关闭相关 Mod。", 8f);
            }

            if (!Hooks.Install(new Harmony(ModKitPlugin.Guid)))
            {
                Log.Error("排行榜上传拦截安装失败，为遵守游戏规则，所有依赖 ModKit 的 Mod 已停用");
                ScreenNotice.Show($"{ModKitPlugin.Name}：无法拦截排行榜上传，相关 Mod 已停用，请等待更新。", 20f);
                return;
            }
            compatible = true;
            WorkshopSettings.Register();
            Log.Info($"{ModKitPlugin.Name} {Version} 已加载（游戏版本 {GameVersion}）");
        }
    }
}
