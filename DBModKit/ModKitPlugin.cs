using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace DBModKit
{
    /// <summary>地下城竞技场 Mod 依赖库。其他 Mod 用 [BepInDependency(ModKitPlugin.Guid)] 声明依赖。</summary>
    [BepInPlugin(Guid, Name, Version)]
    public class ModKitPlugin : BaseUnityPlugin
    {
        public const string Guid = "plge0n.dungeonbrawl.modkit";
        public const string Name = "DBModKit";
        // 发布时同步修改 CHANGELOG.md；pack.ps1 从这里读取版本号
        public const string Version = "0.1.0";

        private void Awake()
        {
            Log.Source = Logger;
            Host.Create();

            var missing = Compat.FindMissing();
            if (missing.Count > 0)
            {
                ModKit.MarkIncompatible(missing);
                Log.Error($"游戏版本 {ModKit.GameVersion} 与 {Name} {Version} 不兼容，所有依赖 ModKit 的 Mod 已停用。缺失：{string.Join("，", missing)}");
                ScreenNotice.Show($"{Name} {Version} 与游戏版本 {ModKit.GameVersion} 不兼容，相关 Mod 已停用，请等待更新。", 20f);
                return;
            }
            if (!Compat.IsTestedVersion)
            {
                Log.Warning($"游戏版本 {ModKit.GameVersion} 未经测试（测试过：{string.Join("、", Compat.TestedVersions)}）");
                ScreenNotice.Show($"{Name} 未在游戏版本 {ModKit.GameVersion} 上测试过，如遇异常请关闭相关 Mod。", 8f);
            }

            ModKit.MarkCompatible();
            Hooks.Install(new Harmony(Guid));
            Log.Info($"{Name} {Version} 已加载（游戏版本 {ModKit.GameVersion}）");
        }
    }

    /// <summary>ModKit 的运行状态。依赖它的 Mod 应在 Awake 里先检查 <see cref="Compatible"/>。</summary>
    public static class ModKit
    {
        public const string Version = ModKitPlugin.Version;

        /// <summary>当前游戏版本与 ModKit 兼容；为 false 时不要调用任何游戏相关接口。</summary>
        public static bool Compatible { get; private set; }

        public static string GameVersion => Application.version;

        /// <summary>不兼容时缺失的游戏成员。</summary>
        public static IReadOnlyList<string> MissingMembers { get; private set; } = new string[0];

        internal static void MarkCompatible() => Compatible = true;

        internal static void MarkIncompatible(IReadOnlyList<string> missing)
        {
            Compatible = false;
            MissingMembers = missing;
        }
    }
}
