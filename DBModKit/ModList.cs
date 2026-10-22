using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace DBModKit
{
    public enum ModState
    {
        /// <summary>用 ModKit 的 Mod，已启动。</summary>
        Running,
        /// <summary>不用 ModKit 的插件（UnityExplorer 等），BepInEx 已加载，状态未知。</summary>
        Plain,
        /// <summary>ModKit 与当前游戏版本不兼容，Mod 没有启动。</summary>
        Disabled,
        /// <summary>启动时出错（详情在 BepInEx/LogOutput.log）。</summary>
        Failed,
        /// <summary>启动过，后来被卸载了（热重载中，或者插件对象被销毁）。</summary>
        Unloaded,
    }

    /// <summary>一个已加载进 BepInEx 的插件。</summary>
    public sealed class ModInfo
    {
        public string Guid { get; internal set; }
        public string Name { get; internal set; }
        public string Version { get; internal set; }
        public ModState State { get; internal set; }
        /// <summary>用 <see cref="ModContext.Start"/> 启动的（依赖 ModKit 的 Mod）。</summary>
        public bool UsesModKit { get; internal set; }
        /// <summary>ScriptEngine 从 BepInEx/scripts 加载的，可以热重载。</summary>
        public bool HotReload { get; internal set; }
        /// <summary>来自创意工坊的条目；不是工坊插件时为 null。</summary>
        public WorkshopItem Workshop { get; internal set; }
        /// <summary>插件的 .cfg 配置；取不到时为 null。</summary>
        public ConfigFile Config { get; internal set; }
    }

    /// <summary>已安装的插件和它们的状态，Mod 列表、设置界面共用。</summary>
    public static class ModList
    {
        /// <summary>BepInEx 已加载的插件（含 ScriptEngine 热重载的），按名字排序；DBModKit 自己也在里面。</summary>
        public static List<ModInfo> Plugins()
        {
            var list = new Dictionary<string, ModInfo>();
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                // Chainloader 里的插件对象可能已被切场景销毁，但它的 Config 还能用，所以不用 Unity 的 == null 判断
                var instance = info.Instance;
                list[info.Metadata.GUID] = Make(info.Metadata, IsHotReloaded(info), ReferenceEquals(instance, null) ? null : instance.Config);
            }
            // ScriptEngine 热重载的插件不在 Chainloader 里，从场景里找
            foreach (var plugin in UnityEngine.Object.FindObjectsOfType<BaseUnityPlugin>())
            {
                var meta = MetadataHelper.GetMetadata(plugin);
                if (meta == null) continue;
                if (list.TryGetValue(meta.GUID, out var e) && e.Config != null) continue;
                list[meta.GUID] = Make(meta, true, plugin.Config);
            }
            return list.Values.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// BepInEx 没能加载的插件（缺少依赖、依赖版本太低、和别的插件冲突），每条是 BepInEx 的原话（英文）。
        /// </summary>
        public static IReadOnlyList<string> LoadErrors => Chainloader.DependencyErrors;

        /// <summary>需要玩家注意的问题数：ModKit 不兼容、Mod 启动失败、没能加载的插件、等确认的工坊插件。</summary>
        public static int ProblemCount()
        {
            int n = LoadErrors.Count + Workshop.PendingCount + Workshop.Items.Count(i => i.Status == WorkshopStatus.Failed);
            if (!ModKit.Compatible) n++;
            n += ModContext.All.Values.Count(c => c.Failed);
            return n;
        }

        private static ModInfo Make(BepInPlugin meta, bool hotReload, ConfigFile config)
        {
            var info = new ModInfo
            {
                Guid = meta.GUID,
                Name = meta.Name,
                Version = meta.Version.ToString(),
                HotReload = hotReload,
                Config = config,
                Workshop = DBModKit.Workshop.Items.FirstOrDefault(i => i.Plugins.Contains(meta.GUID)),
                State = ModState.Plain,
            };
            if (meta.GUID == ModKitPlugin.Guid)
            {
                info.UsesModKit = true;
                info.State = ModKit.Compatible ? ModState.Running : ModState.Disabled;
            }
            else if (ModContext.All.TryGetValue(meta.GUID, out var ctx))
            {
                info.UsesModKit = true;
                info.State = ctx.Loaded ? ModState.Running : ctx.Failed ? ModState.Failed : ctx.Unloaded ? ModState.Unloaded : ModState.Disabled;
            }
            return info;
        }

        // ScriptEngine 也会把插件登记进 Chainloader，只能看 dll 是不是从 BepInEx/scripts 加载的
        private static bool IsHotReloaded(PluginInfo info)
        {
            if (string.IsNullOrEmpty(info.Location)) return false;
            try
            {
                string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(Paths.BepInExRootPath, "scripts"));
                return System.IO.Path.GetFullPath(info.Location).StartsWith(scripts, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
