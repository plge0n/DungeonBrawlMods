using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio.Pages
{
    // Mod：运行环境、已加载的插件；选一个插件改它的设置（.cfg 里的项 + Mod 自己登记的设置区块）
    internal sealed class ModsPage : IStudioPage
    {
        private sealed class PluginEntry
        {
            public string Guid, Name, Version;
            public bool HotReload;
            public ConfigFile Config;
        }

        private static string selectedGuid;

        private Text environment;
        private RectTransform listContent, settingsContent;
        private readonly Dictionary<string, Image> listBackgrounds = new Dictionary<string, Image>();
        private readonly List<SettingsSection> shownSections = new List<SettingsSection>();
        private int sectionsVersion = -1;
        private bool rebuild;

        public string Title => "Mod";

        public void Build(RectTransform body)
        {
            var columns = Studio.Columns(body);

            var left = NativeUi.Column(columns, 520f);
            NativeUi.Heading(left, "运行环境");
            environment = Studio.Block(left, 18);
            NativeUi.Separator(left);
            NativeUi.Heading(left, "插件（点一个看它的设置）");
            listContent = NativeUi.ScrollView(left, out _, 4f);

            var right = NativeUi.Column(columns);
            settingsContent = NativeUi.ScrollView(right, out _, 8f, boxed: false);

            var plugins = Plugins();
            if (selectedGuid == null || plugins.All(p => p.Guid != selectedGuid))
                selectedGuid = plugins.FirstOrDefault(p => p.Guid == Plugin.Guid)?.Guid ?? plugins.FirstOrDefault()?.Guid;
            BuildList(plugins);
            BuildSettings();
        }

        public void Refresh()
        {
            environment.text = EnvironmentText();
            // 热重载会撤下再登记设置区块，跟着重建
            if (rebuild || sectionsVersion != ModSettings.Version)
            {
                rebuild = false;
                BuildList(Plugins());
                BuildSettings();
                return;
            }
            foreach (var s in shownSections)
                if (s.Refresh != null) Safe(s, s.Refresh);
        }

        private void BuildList(List<PluginEntry> plugins)
        {
            Widgets.Clear(listContent);
            listBackgrounds.Clear();
            foreach (var p in plugins)
            {
                var go = new GameObject(p.Guid, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(listContent, false);
                go.GetComponent<LayoutElement>().minHeight = 58f;
                var bg = go.GetComponent<Image>();
                NativeUi.ListItemBackground(bg);
                var btn = go.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                string guid = p.Guid;
                btn.onClick.AddListener(() =>
                {
                    if (selectedGuid == guid) return;
                    KeyCapture.Cancel();
                    selectedGuid = guid;
                    rebuild = true;
                    Studio.RefreshSoon();
                });
                int custom = ModSettings.Sections.Count(s => s.OwnerGuid == p.Guid);
                string hot = p.HotReload ? "  <color=#d6b67d>[热重载]</color>" : "";
                string extra = custom > 0 ? "  <color=#d6b67d>有自定义设置</color>" : "";
                var label = NativeUi.Label(go.transform, $"{p.Name}  <size=16><color=#a6a6a6>{p.Version}</color></size>{hot}\n<size=15><color=#a6a6a6>{p.Guid}</color>{extra}</size>", 20);
                var rt = label.rectTransform;
                NativeUi.Stretch(rt);
                rt.offsetMin = new Vector2(12f, 4f);
                rt.offsetMax = new Vector2(-12f, -4f);
                listBackgrounds[p.Guid] = bg;
            }
            foreach (var kv in listBackgrounds)
                kv.Value.color = kv.Key == selectedGuid ? NativeUi.BoxColor : new Color(0f, 0f, 0f, 0f);
        }

        private void BuildSettings()
        {
            KeyCapture.Cancel();
            Widgets.Clear(settingsContent);
            shownSections.Clear();
            sectionsVersion = ModSettings.Version;
            var p = Plugins().FirstOrDefault(x => x.Guid == selectedGuid);
            if (p == null)
            {
                Studio.Block(settingsContent).text = "在左边选一个插件";
                return;
            }
            var title = NativeUi.Label(settingsContent, $"{p.Name}  <size=18><color=#a6a6a6>{p.Version}</color></size>", 30, -1f, NativeUi.Gold);
            title.fontStyle = FontStyle.Bold;

            // Mod 自己登记的设置
            foreach (var section in ModSettings.Sections.Where(s => s.OwnerGuid == p.Guid).ToList())
            {
                NativeUi.Separator(settingsContent);
                NativeUi.Heading(settingsContent, section.Title);
                var container = NativeUi.Column(settingsContent, -1f, 8f);
                container.GetComponent<LayoutElement>().flexibleHeight = 0f;
                if (Safe(section, () => section.Build(container))) shownSections.Add(section);
                else NativeUi.Label(container, "这块设置出错了，详情见 BepInEx/LogOutput.log", 18, -1f, NativeUi.DimText);
            }

            NativeUi.Separator(settingsContent);
            NativeUi.Heading(settingsContent, "配置文件");
            if (p.Config == null)
            {
                NativeUi.Label(settingsContent, "取不到这个插件的配置", 18, -1f, NativeUi.DimText);
                return;
            }
            NativeUi.Label(settingsContent, $"改了立刻保存到 {System.IO.Path.GetFileName(p.Config.ConfigFilePath)}；有些设置要重启游戏才生效", 16, -1f, NativeUi.DimText);
            ConfigEditor.Build(settingsContent, p.Config);
        }

        private static bool Safe(SettingsSection section, Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"设置区块「{section.Title}」（{section.OwnerGuid}）出错：{e}");
                return false;
            }
        }

        // ScriptEngine 热重载的插件不在 Chainloader 里，从场景里找；
        // Chainloader 里的插件对象可能已被切场景销毁，但它的 Config 还能用，所以不用 Unity 的 == null 判断
        private static List<PluginEntry> Plugins()
        {
            var list = new Dictionary<string, PluginEntry>();
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                var instance = info.Instance;
                list[info.Metadata.GUID] = new PluginEntry
                {
                    Guid = info.Metadata.GUID,
                    Name = info.Metadata.Name,
                    Version = info.Metadata.Version.ToString(),
                    HotReload = IsHotReloaded(info),
                    Config = ReferenceEquals(instance, null) ? null : instance.Config,
                };
            }
            foreach (var plugin in UnityEngine.Object.FindObjectsOfType<BaseUnityPlugin>())
            {
                var meta = MetadataHelper.GetMetadata(plugin);
                if (meta == null) continue;
                if (list.TryGetValue(meta.GUID, out var e) && e.Config != null) continue;
                list[meta.GUID] = new PluginEntry
                {
                    Guid = meta.GUID,
                    Name = meta.Name,
                    Version = meta.Version.ToString(),
                    HotReload = true,
                    Config = plugin.Config,
                };
            }
            return list.Values.OrderBy(p => p.Name).ToList();
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

        private static string EnvironmentText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"游戏版本  {ModKit.GameVersion}");
            sb.AppendLine($"DBModKit  {ModKit.Version}  " + (ModKit.Compatible ? Good("兼容") : Bad("不兼容，依赖它的 Mod 已停用")));
            if (ModKit.MissingMembers.Count > 0)
                sb.AppendLine(Dim("缺失：" + string.Join("，", ModKit.MissingMembers)));
            sb.AppendLine("自动释放钩子  " + (AutoCast.Available ? Good("可用") : Bad("不可用")));
            sb.AppendLine("排行榜  " + Dim("装了 Mod 不上传（游戏作者的规则）"));
            string room = Game.IsSolo ? "单人"
                : Game.PlayerCount > 0 ? $"联机，你是房主，{Game.PlayerCount} 人"
                : "联机，在别人的房间里";
            sb.Append($"房间  {room}");
            return sb.ToString();
        }

        private static string Good(string s) => $"<color=#9fd08a>{s}</color>";
        private static string Bad(string s) => $"<color=#e07a5f>{s}</color>";
        private static string Dim(string s) => $"<color=#a6a6a6>{s}</color>";
    }
}
