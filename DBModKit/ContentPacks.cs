using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace DBModKit
{
    /// <summary>一个内容包：JSON 定义的技能、被动、遗物、变体英雄，加上图片和翻译，不含代码。</summary>
    public sealed class ContentPack
    {
        internal readonly List<Action> Cleanups = new List<Action>();
        internal readonly List<string> ErrorList = new List<string>();
        // dbmod.json 本身的问题（格式太新、DBModKit 版本不够）：整个包不能加载
        internal string Blocked;

        /// <summary>注册技能用的所有者，"content:" + 包的 Id（dbmod.json 的 Id，没写时是文件夹名）。</summary>
        public string Guid { get; internal set; }
        public string Folder { get; internal set; }
        /// <summary>从哪来："创意工坊"、"本地"（BepInEx/content）或"工坊测试"（BepInEx/workshop-dev）。</summary>
        public string Source { get; internal set; }
        public WorkshopManifest Manifest { get; internal set; }
        /// <summary>正在生效。</summary>
        public bool Loaded { get; internal set; }
        /// <summary>加载了什么，比如"2 个主动技能、1 个被动"。</summary>
        public string Summary { get; internal set; } = "";
        /// <summary>写错的地方（文件和位置）；写错的条目没有加载，其他照常。</summary>
        public IReadOnlyList<string> Errors => ErrorList;
        /// <summary>缺的依赖（dbmod.json 的 Dependencies 里没满足的）：有缺的时候整个包不加载。</summary>
        public List<ManifestDependency> MissingDependencies { get; } = new List<ManifestDependency>();

        public string DisplayName => string.IsNullOrEmpty(Manifest?.Name) ? Path.GetFileName(Folder) : Manifest.Name;
    }

    /// <summary>
    /// 内容包加载器：扫描创意工坊、BepInEx/workshop-dev/ 和 BepInEx/content/ 下 dbmod.json 里 "Type": "content" 的文件夹，
    /// 按里面的 JSON 注册技能、被动和变体英雄。内容包不执行代码，所以不用逐个确认；玩家可以在 Mod 设置里关掉。
    /// JSON 写错时只跳过写错的条目，错误写进运行记录并显示在 Mod 设置里。
    /// </summary>
    public static class ContentPacks
    {
        private static readonly List<ContentPack> packs = new List<ContentPack>();
        // 实际加载的顺序（依赖在前），卸载时倒过来
        private static readonly List<ContentPack> loadOrder = new List<ContentPack>();
        private static JsonConfig<Dictionary<string, bool>> enabled;
        private static bool started;

        public static IReadOnlyList<ContentPack> All => packs;

        // Id 重复、没有用的包：(这个包, 用了的那个)，给 ModConflicts
        internal static readonly List<(string pack, string used)> Duplicates = new List<(string, string)>();

        /// <summary>列表或状态变化时加 1。</summary>
        public static int Version { get; private set; }

        /// <summary>本地内容包文件夹：每个包一个子文件夹。</summary>
        public static string LocalFolder => Path.Combine(Paths.BepInExRootPath, "content");

        /// <summary>玩家没关掉这个包。</summary>
        public static bool IsEnabled(ContentPack pack) => enabled == null || !enabled.Data.TryGetValue(pack.Guid, out bool on) || on;

        // Runner 第一次 Update 时调用（和工坊插件一起）
        internal static void Start()
        {
            if (started || !ModKit.Compatible) return;
            started = true;
            Safe.Run("内容包", () =>
            {
                enabled = new JsonConfig<Dictionary<string, bool>>("DBModKit.content.json");
                Scan();
                LoadAll();
                int broken = packs.Count(p => p.ErrorList.Count > 0);
                Log.Info($"内容包：{packs.Count} 个，已加载 {packs.Count(p => p.Loaded)} 个，有错误的 {broken} 个");
                if (broken > 0)
                    ScreenNotice.Show($"{broken} 个内容包有写错的地方，写错的条目没有加载。详情见 Mod 设置 → DBModKit → 内容包，或 BepInEx/DBModKit/trace.log", 10f);
                Version++;
            });
        }

        /// <summary>
        /// 打开或关掉一个内容包（记住选择）。战斗中不能切换：技能会从技能栏里消失。
        /// 所有内容包会重新读一遍（作者可能刚改过文件），依赖它的包跟着加载或卸载。
        /// </summary>
        public static bool SetEnabled(ContentPack pack, bool on)
        {
            if (enabled == null || Game.InBattle) return false;
            enabled.Data[pack.Guid] = on;
            enabled.MarkDirty();
            for (int i = loadOrder.Count - 1; i >= 0; i--) Unload(loadOrder[i]);
            loadOrder.Clear();
            LoadAll();
            Version++;
            return true;
        }

        // 按依赖排序加载打开的包：依赖的包还没加载就等它；依赖不满足的整个包不加载；剩下互相等的是循环依赖
        private static void LoadAll()
        {
            foreach (var p in packs)
            {
                p.ErrorList.Clear();
                p.MissingDependencies.Clear();
            }
            var pending = packs.Where(IsEnabled).ToList();
            while (pending.Count > 0)
            {
                bool progress = false;
                foreach (var pack in pending.ToList())
                {
                    var deps = pack.Manifest.Dependencies.Where(d => !string.IsNullOrEmpty(d.Guid)).ToList();
                    if (deps.Any(d => pending.Any(o => o != pack && o.Guid == ModConflicts.ContentGuid(d.Guid)))) continue;
                    pending.Remove(pack);
                    progress = true;
                    foreach (var dep in deps)
                    {
                        string why = ModConflicts.Check(dep);
                        if (why == null) continue;
                        pack.ErrorList.Add(why);
                        pack.MissingDependencies.Add(dep);
                    }
                    if (pack.MissingDependencies.Count > 0)
                    {
                        RunLog.WriteCore(ModKitPlugin.Name, "内容包", RunLogLevel.Warning, "缺依赖，没有加载",
                            new (string, object)[] { ("包", pack.DisplayName), ("原因", string.Join("；", pack.ErrorList)) }, mirror: false);
                        continue;
                    }
                    Load(pack);
                    loadOrder.Add(pack);
                }
                if (progress) continue;
                foreach (var pack in pending) pack.ErrorList.Add("依赖之间有循环");
                break;
            }
            ModConflicts.Touch();
        }

        private static void Scan()
        {
            packs.Clear();
            Duplicates.Clear();
            foreach (var (root, source) in new[] { (Workshop.ContentFolder, "创意工坊"), (Workshop.DevFolder, "工坊测试"), (LocalFolder, "本地") })
            {
                if (!Directory.Exists(root)) continue;
                foreach (var folder in Directory.GetDirectories(root))
                {
                    var pack = Read(folder, source);
                    if (pack == null) continue;
                    var same = packs.FirstOrDefault(p => p.Guid == pack.Guid);
                    if (same != null)
                    {
                        Log.Warning($"内容包 {pack.Folder} 和 {same.Folder} 的 Id 都是 {pack.Guid}，只用前一个");
                        Duplicates.Add(($"{pack.DisplayName}（{pack.Source} {Path.GetFileName(pack.Folder)}）", $"{same.DisplayName}（{same.Source} {Path.GetFileName(same.Folder)}）"));
                        continue;
                    }
                    packs.Add(pack);
                }
            }
        }

        // 不是内容包时返回 null
        private static ContentPack Read(string folder, string source)
        {
            string path = Path.Combine(folder, WorkshopManifest.FileName);
            if (!File.Exists(path)) return null;
            WorkshopManifest manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<WorkshopManifest>(File.ReadAllText(path)) ?? new WorkshopManifest();
            }
            catch (Exception)
            {
                return null; // 读不了的条目由工坊加载器报告
            }
            if (manifest.Type != "content") return null;
            string id = string.IsNullOrEmpty(manifest.Id) ? Path.GetFileName(folder) : manifest.Id;
            var pack = new ContentPack
            {
                Folder = folder,
                Source = source,
                Manifest = manifest,
                Guid = "content:" + new string(id.Select(c => c < 128 && (char.IsLetterOrDigit(c) || ".-_+".IndexOf(c) >= 0) ? c : '_').ToArray()),
            };
            if (manifest.Format > WorkshopManifest.CurrentFormat) pack.Blocked = "这个内容包需要更新版本的 DBModKit";
            else if (!string.IsNullOrEmpty(manifest.MinModKit) && System.Version.TryParse(manifest.MinModKit, out var need)
                     && System.Version.TryParse(ModKit.Version, out var have) && have < need)
                pack.Blocked = $"需要 DBModKit {need}，当前是 {have}";
            return pack;
        }

        private static void Load(ContentPack pack)
        {
            if (pack.Blocked != null)
            {
                pack.ErrorList.Add(pack.Blocked);
                return;
            }
            var m = pack.Manifest;
            string root = Path.GetFullPath(pack.Folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            // 包里的文件只能在包的文件夹里：别人的包不能读到本机任意文件
            string Inside(string relative)
            {
                string full = Path.GetFullPath(Path.Combine(root, relative ?? ""));
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"路径不在内容包文件夹里：{relative}");
                return full;
            }

            var parsed = new ParsedContent();
            uint fingerprint = 2166136261;
            string LoadIcon(string key, string value)
            {
                string ext = Path.GetExtension(value).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") return value; // 游戏图标名
                string file = Inside(value);
                if (!File.Exists(file)) throw new FileNotFoundException($"找不到图片 {value}");
                string name = ModAssets.RegisterIcon(pack.Guid, key, ModAssets.LoadSprite(file));
                pack.Cleanups.Add(() => ModAssets.UnregisterIcon(name));
                return name;
            }

            if (m.Content.Count == 0) parsed.Errors.Add("dbmod.json 的 Content 里没有列出 JSON 文件");
            foreach (string file in m.Content)
            {
                string full;
                try
                {
                    full = Inside(file);
                }
                catch (ArgumentException e)
                {
                    parsed.Errors.Add(e.Message);
                    continue;
                }
                if (!File.Exists(full)) parsed.Errors.Add($"找不到 {file}");
                else
                {
                    ContentReader.ReadFile(full, file, LoadIcon, parsed);
                    fingerprint = Fnv(fingerprint, File.ReadAllText(full));
                }
            }

            var text = new ModText(Path.Combine(pack.Folder, string.IsNullOrEmpty(m.Lang) ? "lang" : m.Lang));
            Multiplayer.AddMod(pack.Guid, pack.DisplayName, string.IsNullOrEmpty(m.Version) ? "0" : m.Version);
            pack.Cleanups.Add(() => Multiplayer.RemoveMod(pack.Guid));

            var actives = new Dictionary<string, ActiveSkillInfo>();
            var passives = new Dictionary<string, PassiveSkillInfo>();
            foreach (var (def, _) in parsed.Actives)
                Register(pack, parsed, $"主动技能 {def.Key}", () =>
                {
                    var info = ActiveSkills.Register(pack.Guid, text, def);
                    pack.Cleanups.Add(() => ActiveSkills.Unregister(info));
                    actives[def.Key] = info;
                });
            foreach (var (def, _) in parsed.Passives)
                Register(pack, parsed, $"被动 {def.Key}", () =>
                {
                    var info = PassiveSkills.Register(pack.Guid, text, def);
                    pack.Cleanups.Add(() => PassiveSkills.Unregister(info));
                    passives[def.Key] = info;
                });
            int relics = 0;
            foreach (var def in parsed.Relics)
                Register(pack, parsed, $"遗物 {def.Key}", () =>
                {
                    var info = CustomRelics.Register(pack.Guid, text, def);
                    pack.Cleanups.Add(() => CustomRelics.Unregister(info));
                    relics++;
                });
            int variants = 0;
            foreach (var (def, active, passive, where) in parsed.Variants)
            {
                if (def.ModelFolder != null)
                {
                    try
                    {
                        def.ModelFolder = Inside(def.ModelFolder);
                    }
                    catch (ArgumentException e)
                    {
                        parsed.Errors.Add($"{where}.modelFolder：{e.Message}");
                        continue;
                    }
                }
                if (active != null && !actives.TryGetValue(active, out def.ActiveSkill))
                {
                    parsed.Errors.Add($"{where}.activeSkill：这个包里没有键为 \"{active}\" 的主动技能");
                    continue;
                }
                if (passive != null && !passives.TryGetValue(passive, out def.PassiveSkill))
                {
                    parsed.Errors.Add($"{where}.passiveSkill：这个包里没有键为 \"{passive}\" 的被动");
                    continue;
                }
                Register(pack, parsed, $"变体 {def.Key}", () =>
                {
                    var info = HeroVariants.Register(pack.Guid, text, pack.Folder, def);
                    pack.Cleanups.Add(() => HeroVariants.Unregister(info));
                    variants++;
                });
            }

            // 改玩法的包按 JSON 内容的哈希比对：同一个版本号、文件被改过的包联机时也算"装的不一样"
            if (actives.Count > 0 || passives.Count > 0 || relics > 0 || HeroVariants.All.Any(v => v.Owner == pack.Guid && v.ChangesGameplay))
                Multiplayer.Declare(pack.Guid, fingerprint.ToString("x8"));

            pack.ErrorList.AddRange(parsed.Errors);
            foreach (string e in parsed.Errors)
                RunLog.WriteCore(ModKitPlugin.Name, "内容包", RunLogLevel.Warning, "写错了", new (string, object)[] { ("包", pack.DisplayName), ("位置", e) }, mirror: false);
            var parts = new List<string>();
            if (actives.Count > 0) parts.Add($"{actives.Count} 个主动技能");
            if (passives.Count > 0) parts.Add($"{passives.Count} 个被动");
            if (relics > 0) parts.Add($"{relics} 个遗物");
            if (variants > 0) parts.Add($"{variants} 个变体英雄");
            pack.Summary = parts.Count > 0 ? string.Join("、", parts) : "没有加载任何内容";
            pack.Loaded = true;
            RunLog.WriteCore(ModKitPlugin.Name, "内容包", RunLogLevel.Info, "已加载", new (string, object)[]
                { ("包", pack.DisplayName), ("Id", pack.Guid), ("版本", m.Version), ("来源", pack.Source), ("内容", pack.Summary), ("错误", parsed.Errors.Count) }, mirror: false);
        }

        private static uint Fnv(uint hash, string text)
        {
            foreach (char c in text)
                hash = (hash ^ c) * 16777619;
            return hash;
        }

        private static void Register(ContentPack pack, ParsedContent parsed, string what, Action register)
        {
            try
            {
                register();
            }
            catch (Exception e)
            {
                parsed.Errors.Add($"{what}：{e.Message}");
            }
        }

        private static void Unload(ContentPack pack)
        {
            for (int i = pack.Cleanups.Count - 1; i >= 0; i--)
                Safe.Run($"内容包 {pack.DisplayName} 卸载", pack.Cleanups[i]);
            pack.Cleanups.Clear();
            pack.Loaded = false;
            pack.Summary = "";
            RunLog.WriteCore(ModKitPlugin.Name, "内容包", RunLogLevel.Info, "已关闭", new (string, object)[] { ("包", pack.DisplayName) }, mirror: false);
        }
    }

    // DBStudio Mod 页 → DBModKit 里的"内容包"一栏
    internal static class ContentPackSettings
    {
        private static RectTransform list;
        private static int shownVersion = -1;
        private static bool shownInBattle;

        public static void Register() => ModSettings.Register(ModKitPlugin.Guid, "内容包", Build, Refresh);

        private static void Build(RectTransform container)
        {
            NativeUi.Label(container, $"不含代码的 Mod（JSON + 图片）。本地的放在 {ContentPacks.LocalFolder}\\<名字>\\，订阅的从创意工坊读。战斗中不能开关。", 16, -1f, NativeUi.DimText);
            list = NativeUi.Column(container, -1f, 6f);
            list.GetComponent<LayoutElement>().flexibleHeight = 0f;
            shownVersion = -1;
            Refresh();
        }

        private static void Refresh()
        {
            if (list == null || shownVersion == ContentPacks.Version && shownInBattle == Game.InBattle) return;
            shownVersion = ContentPacks.Version;
            shownInBattle = Game.InBattle;
            for (int i = list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
            if (ContentPacks.All.Count == 0)
            {
                NativeUi.Label(list, "没有内容包", 18, -1f, NativeUi.DimText);
                return;
            }
            foreach (var pack in ContentPacks.All)
            {
                var row = NativeUi.Row(list, 64f, 12f);
                var texts = NativeUi.Column(row, -1f, 0f);
                var m = pack.Manifest;
                NativeUi.Label(texts, $"{WorkshopPrompt.Plain(pack.DisplayName)}  <size=16><color=#a6a6a6>{WorkshopPrompt.Plain(m.Version)}" +
                                      $"{(string.IsNullOrEmpty(m.Author) ? "" : "  作者 " + WorkshopPrompt.Plain(m.Author))}  {pack.Source}</color></size>", 22);
                string state = pack.Loaded ? pack.Summary : ContentPacks.IsEnabled(pack) ? "没有加载" : "已关闭";
                NativeUi.Label(texts, state, 16, -1f, NativeUi.DimText);
                foreach (string e in pack.Errors.Take(6))
                    NativeUi.Label(texts, $"<color=#e07a5f>{WorkshopPrompt.Plain(e)}</color>", 15);
                if (pack.Errors.Count > 6) NativeUi.Label(texts, $"<color=#e07a5f>还有 {pack.Errors.Count - 6} 处，见 trace.log</color>", 15);
                foreach (var dep in pack.MissingDependencies.Where(d => d.Workshop != 0))
                {
                    ulong id = dep.Workshop;
                    NativeUi.Button(row, $"订阅 {WorkshopPrompt.Plain(dep.DisplayName)}", false, 220f, () => HeroSkins.OpenWorkshopPage(id));
                }
                bool on = ContentPacks.IsEnabled(pack);
                var target = pack;
                var button = NativeUi.Button(row, on ? "关闭" : "启用", !on, 150f, () => ContentPacks.SetEnabled(target, !on));
                button.interactable = !Game.InBattle;
            }
        }
    }
}
