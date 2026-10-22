using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace DBModKit
{
    /// <summary>工坊条目根目录的 dbmod.json。游戏只认 manifest.json，所以游戏会跳过我们的条目。</summary>
    public sealed class WorkshopManifest
    {
        public const string FileName = "dbmod.json";
        public const int CurrentFormat = 1;

        public int Format = CurrentFormat;
        /// <summary>目前只有 "plugin"（BepInEx 插件）。</summary>
        public string Type = "plugin";
        public string Name = "";
        public string Author = "";
        public string Version = "";
        public string Description = "";
        /// <summary>需要的最低 DBModKit 版本。</summary>
        public string MinModKit = "";
        /// <summary>要加载的 dll（相对条目根目录）；为空时加载根目录下所有 dll。</summary>
        public List<string> Assemblies = new List<string>();
    }

    public enum WorkshopStatus
    {
        /// <summary>第一次见到，等玩家确认。</summary>
        Pending,
        /// <summary>玩家选了不加载。</summary>
        Declined,
        Loaded,
        /// <summary>允许了但没加载成功（版本不够、缺依赖、出错等），原因见 Message。</summary>
        Failed,
        /// <summary>不是我们格式的条目（英雄模型、黑名单包等），不显示。</summary>
        Ignored,
    }

    public sealed class WorkshopItem
    {
        /// <summary>工坊条目编号；本地测试目录里的是 "dev:文件夹名"。</summary>
        public string Id { get; internal set; }
        public string Folder { get; internal set; }
        public bool IsDev { get; internal set; }
        public WorkshopManifest Manifest { get; internal set; }
        public WorkshopStatus Status { get; internal set; }
        /// <summary>给玩家看的状态说明。</summary>
        public string Message { get; internal set; } = "";
        /// <summary>加载出来的插件 GUID。</summary>
        public List<string> Plugins { get; } = new List<string>();

        public string DisplayName => string.IsNullOrEmpty(Manifest?.Name) ? Path.GetFileName(Folder) : Manifest.Name;
    }

    /// <summary>
    /// 创意工坊加载器：游戏的工坊不加载 dll，这里扫描已订阅的条目（steamapps/workshop/content/4246640/编号/），
    /// 把带 dbmod.json 的插件条目当成 BepInEx 插件加载。每个条目第一次加载前要玩家确认（工坊 dll 就是别人写的程序）。
    /// 只在 ModKit 兼容、排行榜拦截已安装时工作。本地测试目录 BepInEx/workshop-dev/ 下的条目不用确认。
    /// </summary>
    public static class Workshop
    {
        public const string AppId = "4246640";

        private static readonly List<WorkshopItem> items = new List<WorkshopItem>();
        private static JsonConfig<Dictionary<string, bool>> decisions;
        private static bool started;

        public static IReadOnlyList<WorkshopItem> Items => items;

        /// <summary>条目列表或状态变化时加 1。</summary>
        public static int Version { get; private set; }

        /// <summary>工坊内容目录（和游戏在同一个 Steam 库里）。</summary>
        public static string ContentFolder => Path.GetFullPath(Path.Combine(Paths.GameRootPath, "..", "..", "workshop", "content", AppId));

        /// <summary>本地测试目录：放进来的条目和订阅的一样加载，不用确认。</summary>
        public static string DevFolder => Path.Combine(Paths.BepInExRootPath, "workshop-dev");

        public static int PendingCount => items.Count(i => i.Status == WorkshopStatus.Pending);

        // Runner 第一次 Update 时调用：这时 Chainloader 已经加载完 plugins 目录，依赖检查才准
        internal static void Start()
        {
            if (started || !ModKit.Compatible) return;
            started = true;
            Safe.Run("工坊加载器", () =>
            {
                decisions = new JsonConfig<Dictionary<string, bool>>("DBModKit.workshop.json");
                Scan();
                LoadAll(items.Where(i => i.Status == WorkshopStatus.Pending && IsAllowed(i)).ToList());
                Log.Info($"工坊：{items.Count} 个插件条目，已加载 {items.Count(i => i.Status == WorkshopStatus.Loaded)} 个，待确认 {PendingCount} 个");
                if (PendingCount > 0) WorkshopPrompt.Show();
            });
        }

        /// <summary>允许或拒绝一个条目（记住选择）。允许时立刻加载；拒绝已加载的条目要重启游戏才卸掉。</summary>
        public static void Decide(WorkshopItem item, bool allow)
        {
            if (item.IsDev || decisions == null) return;
            decisions.Data[item.Id] = allow;
            decisions.MarkDirty();
            if (allow && (item.Status == WorkshopStatus.Pending || item.Status == WorkshopStatus.Declined))
            {
                item.Status = WorkshopStatus.Pending;
                LoadAll(new List<WorkshopItem> { item });
            }
            else if (!allow && item.Status == WorkshopStatus.Loaded)
            {
                item.Message = "已设为不加载，重启游戏后生效";
            }
            else if (!allow)
            {
                item.Status = WorkshopStatus.Declined;
                item.Message = "";
            }
            Version++;
        }

        private static bool IsAllowed(WorkshopItem item) => item.IsDev || (decisions.Data.TryGetValue(item.Id, out bool allow) && allow);

        private static void Scan()
        {
            items.Clear();
            foreach (var (root, dev) in new[] { (ContentFolder, false), (DevFolder, true) })
            {
                if (!Directory.Exists(root)) continue;
                foreach (var folder in Directory.GetDirectories(root))
                {
                    var item = Read(folder, dev);
                    if (item.Status != WorkshopStatus.Ignored) items.Add(item);
                }
            }
            Version++;
        }

        private static WorkshopItem Read(string folder, bool dev)
        {
            var item = new WorkshopItem { Folder = folder, IsDev = dev, Id = (dev ? "dev:" : "") + Path.GetFileName(folder) };
            string path = Path.Combine(folder, WorkshopManifest.FileName);
            if (!File.Exists(path))
            {
                item.Status = WorkshopStatus.Ignored;
                return item;
            }
            try
            {
                item.Manifest = JsonConvert.DeserializeObject<WorkshopManifest>(File.ReadAllText(path)) ?? new WorkshopManifest();
            }
            catch (Exception e)
            {
                return Fail(item, $"dbmod.json 格式不对：{e.Message}");
            }
            if (item.Manifest.Type != "plugin")
            {
                item.Status = WorkshopStatus.Ignored; // 以后的玩法包等由各自的 Mod 读取
                return item;
            }
            if (item.Manifest.Format > WorkshopManifest.CurrentFormat)
                return Fail(item, "这个条目需要更新版本的 DBModKit");
            item.Status = dev || !decisions.Data.TryGetValue(item.Id, out bool allow) ? WorkshopStatus.Pending
                : allow ? WorkshopStatus.Pending : WorkshopStatus.Declined;
            return item;
        }

        // ---- 加载 ----

        private sealed class Candidate
        {
            public WorkshopItem Item;
            public Type Type;
            public BepInPlugin Metadata;
        }

        private static void LoadAll(List<WorkshopItem> batch)
        {
            var candidates = new List<Candidate>();
            foreach (var item in batch)
            {
                try
                {
                    candidates.AddRange(LoadAssemblies(item));
                }
                catch (Exception e)
                {
                    Fail(item, e.Message);
                    Log.Error($"工坊条目 {item.DisplayName}（{item.Id}）加载失败：{e}");
                }
            }

            // 依赖可能在同一批里：每轮加载依赖已满足的，直到没有进展
            bool progress = true;
            while (progress && candidates.Count > 0)
            {
                progress = false;
                foreach (var c in candidates.ToList())
                {
                    string missing = MissingDependency(c, candidates);
                    if (missing == "") continue; // 依赖在本批里，下一轮再试
                    candidates.Remove(c);
                    progress = true;
                    if (missing != null) Fail(c.Item, missing);
                    else Instantiate(c);
                }
            }
            foreach (var c in candidates) Fail(c.Item, "依赖之间有循环");
            foreach (var item in batch.Where(i => i.Status == WorkshopStatus.Pending))
                Fail(item, "条目里没有找到插件");
            Version++;
        }

        private static List<Candidate> LoadAssemblies(WorkshopItem item)
        {
            if (!string.IsNullOrEmpty(item.Manifest.MinModKit)
                && System.Version.TryParse(item.Manifest.MinModKit, out var need)
                && System.Version.TryParse(ModKit.Version, out var have) && have < need)
                throw new Exception($"需要 DBModKit {need}，当前是 {have}");

            var files = item.Manifest.Assemblies.Count > 0
                ? item.Manifest.Assemblies.Select(a => Path.GetFullPath(Path.Combine(item.Folder, a))).ToList()
                : Directory.GetFiles(item.Folder, "*.dll").ToList();
            var result = new List<Candidate>();
            foreach (var file in files)
            {
                // 不能跑出条目目录
                if (!file.StartsWith(Path.GetFullPath(item.Folder), StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"dll 路径不在条目目录里：{file}");
                if (!File.Exists(file)) throw new Exception($"找不到 {Path.GetFileName(file)}");
                var assembly = Assembly.LoadFrom(file);
                foreach (var type in SafeTypes(assembly))
                {
                    if (type.IsAbstract || !typeof(BaseUnityPlugin).IsAssignableFrom(type)) continue;
                    var meta = MetadataHelper.GetMetadata(type);
                    if (meta != null) result.Add(new Candidate { Item = item, Type = type, Metadata = meta });
                }
            }
            return result;
        }

        private static IEnumerable<Type> SafeTypes(Assembly a)
        {
            try
            {
                return a.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        // null = 可以加载；"" = 依赖在同一批里还没加载；其他 = 不能加载的原因
        private static string MissingDependency(Candidate c, List<Candidate> batch)
        {
            if (Chainloader.PluginInfos.ContainsKey(c.Metadata.GUID))
                return $"已经装了同一个插件（{c.Metadata.GUID}），用的是本地那份";
            foreach (var inc in MetadataHelper.GetAttributes<BepInIncompatibility>(c.Type))
                if (Chainloader.PluginInfos.ContainsKey(inc.IncompatibilityGUID))
                    return $"和 {inc.IncompatibilityGUID} 不兼容";
            foreach (var dep in MetadataHelper.GetDependencies(c.Type))
            {
                if ((dep.Flags & BepInDependency.DependencyFlags.HardDependency) == 0) continue;
                if (Chainloader.PluginInfos.TryGetValue(dep.DependencyGUID, out var info))
                {
                    if (info.Metadata.Version < dep.MinimumVersion)
                        return $"需要 {info.Metadata.Name} {dep.MinimumVersion}，当前是 {info.Metadata.Version}";
                    continue;
                }
                if (batch.Any(o => o != c && o.Metadata.GUID == dep.DependencyGUID)) return "";
                return $"缺少依赖 {dep.DependencyGUID}";
            }
            return null;
        }

        // 和 ScriptEngine 一样：先登记 PluginInfo（基类构造时会取它），再挂到自己的常驻对象上
        // （游戏切场景会销毁 BepInEx 的管理对象）
        private static void Instantiate(Candidate c)
        {
            try
            {
                var info = new PluginInfo();
                Set(info, nameof(PluginInfo.Metadata), c.Metadata);
                Set(info, nameof(PluginInfo.Processes), MetadataHelper.GetAttributes<BepInProcess>(c.Type));
                Set(info, nameof(PluginInfo.Dependencies), MetadataHelper.GetDependencies(c.Type));
                Set(info, nameof(PluginInfo.Incompatibilities), MetadataHelper.GetAttributes<BepInIncompatibility>(c.Type));
                Set(info, nameof(PluginInfo.Location), c.Type.Assembly.Location);
                Chainloader.PluginInfos[c.Metadata.GUID] = info;

                var go = new GameObject("DBModKit.Workshop." + c.Metadata.GUID) { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);
                var plugin = (BaseUnityPlugin)go.AddComponent(c.Type);
                Set(info, nameof(PluginInfo.Instance), plugin);

                c.Item.Plugins.Add(c.Metadata.GUID);
                c.Item.Status = WorkshopStatus.Loaded;
                c.Item.Message = "";
                Log.Info($"工坊：已加载 {c.Metadata.Name} {c.Metadata.Version}（{c.Item.Id}）");
            }
            catch (Exception e)
            {
                Chainloader.PluginInfos.Remove(c.Metadata.GUID);
                Fail(c.Item, $"{c.Metadata.Name} 启动失败：{e.Message}");
                Log.Error($"工坊插件 {c.Metadata.GUID} 启动失败：{e}");
            }
        }

        private static void Set(PluginInfo info, string property, object value)
            => AccessTools.Property(typeof(PluginInfo), property).SetValue(info, value, null);

        private static WorkshopItem Fail(WorkshopItem item, string message)
        {
            // 同一条目里有插件已加载成功时保留"已加载"，原因附在后面
            if (item.Status != WorkshopStatus.Loaded) item.Status = WorkshopStatus.Failed;
            item.Message = string.IsNullOrEmpty(item.Message) ? message : item.Message + "；" + message;
            return item;
        }
    }
}
