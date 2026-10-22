using System;
using System.Linq;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;

namespace DBModKit
{
    // 主菜单"创意工坊"下面的"Mod"按钮和它打开的 Mod 列表：装了哪些 Mod、有没有在运行、哪些没加载上。
    // 放在 ModKit 里：没装 DBStudio 的玩家也要看得到；ModKit 不兼容时也照常显示（正是最需要看的时候）
    internal static class ModMenu
    {
        private const string ButtonName = "btn_dbmods";
        private const float LabelInterval = 0.5f;

        private static Text label;
        private static float nextScan, nextLabel;

        private static Canvas canvas;
        private static NativeWindow window;
        private static RectTransform content;
        private static IDisposable inputBlock;
        private static int shownWorkshopVersion, shownContexts;

        public static bool IsOpen => window != null && window.IsOpen;

        public static void Tick()
        {
            float now = Time.unscaledTime;
            // 主菜单面板换场景会重建，按钮跟着没了就重新找
            if (label == null && now >= nextScan)
            {
                nextScan = now + 1f;
                Attach();
            }
            if (label != null && now >= nextLabel)
            {
                nextLabel = now + LabelInterval;
                string text = LabelText();
                if (label.text != text) label.text = text;
            }
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape) && !NativeUi.IsTyping)
            {
                Close();
                return;
            }
            // 在工坊确认窗口里做了选择、热重载了 Mod 时跟着刷新
            if (shownWorkshopVersion != Workshop.Version || shownContexts != ModContext.All.Count) Rebuild();
        }

        private static void Attach()
        {
            var template = GameRaw.MainMenuWorkshopButton();
            if (template == null) return;
            var parent = template.transform.parent;
            var go = parent.Find(ButtonName)?.gameObject;
            if (go == null)
            {
                // 照抄"创意工坊"按钮：背景、悬停动画、选中箭头都和原版一样，竖排布局自动往下排
                go = UnityEngine.Object.Instantiate(template, parent, false);
                go.name = ButtonName;
                go.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
                // 本地化组件会把文字改回"创意工坊"
                foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (c != null && c.GetType().Name == "LocalizationText")
                        UnityEngine.Object.DestroyImmediate(c);
                // 不换掉 onClick：克隆时原按钮代码里加的监听不会带过来，悬停动画组件自己加的点击音效等要留着
                go.GetComponent<Button>().onClick.AddListener(Open);
            }
            label = go.GetComponentInChildren<Text>(true);
            label.supportRichText = true;
            nextLabel = 0f;
        }

        // "Mod 6"，有要处理的问题时换成红色的"!"
        private static string LabelText()
        {
            int problems = ModList.ProblemCount();
            if (problems > 0) return "Mod  <color=#e07a5f>!</color>";
            int running = ModContext.All.Values.Count(c => c.Loaded);
            return running > 0 ? $"Mod  <size=20><color=#d6b67d>{running}</color></size>" : "Mod";
        }

        public static void Open()
        {
            if (IsOpen || !NativeUi.Load()) return;
            if (canvas == null) canvas = NativeUi.Overlay("DBModKit.ModMenu", 5500);
            window = NativeUi.Window(canvas.transform, "Mod", new Vector2(1180f, 860f), Close);
            var body = window.Body.gameObject.AddComponent<VerticalLayoutGroup>();
            body.spacing = 10f;
            body.childControlWidth = body.childControlHeight = true;
            body.childForceExpandWidth = true;
            body.childForceExpandHeight = false;
            content = NativeUi.ScrollView(window.Body, out _, 8f);

            var bottom = NativeUi.Row(window.Body, 56f, 16f);
            bottom.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            NativeUi.Button(bottom, "打开 Mod 文件夹", false, 240f, OpenPluginFolder);
            if (ModSettings.CanOpen) NativeUi.Button(bottom, "Mod 设置", true, 200f, () => OpenSettings(null));
            NativeUi.Button(bottom, "关闭", false, 160f, Close);
            window.Status.text = "新装的 Mod 放进 BepInEx/plugins 后重启游戏生效  ·  ESC 关闭";

            inputBlock = GameInput.Block();
            Rebuild();
        }

        public static void Close()
        {
            window?.Destroy();
            window = null;
            content = null;
            inputBlock?.Dispose();
            inputBlock = null;
        }

        private static void Rebuild()
        {
            shownWorkshopVersion = Workshop.Version;
            shownContexts = ModContext.All.Count;
            for (int i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(content.GetChild(i).gameObject);

            var plugins = ModList.Plugins();
            Block(content, Summary(), 20);

            var mods = plugins.Where(p => p.UsesModKit).ToList();
            NativeUi.Separator(content);
            NativeUi.Heading(content, $"已安装的 Mod（{mods.Count}）");
            foreach (var p in mods) AddModRow(p);
            if (mods.All(p => p.Guid == ModKitPlugin.Guid))
                Block(content, "还没有装 Mod。把 Mod 的文件夹放进 BepInEx/plugins，重启游戏。", 18, NativeUi.DimText);

            var errors = ModList.LoadErrors;
            if (errors.Count > 0)
            {
                NativeUi.Separator(content);
                NativeUi.Heading(content, $"没能加载（{errors.Count}）");
                Block(content, "通常是缺少前置 Mod（比如 DBModKit）或版本太旧：把 Mod 的压缩包重新完整解压覆盖一次。", 18, NativeUi.DimText);
                foreach (var e in errors) Block(content, Bad(WorkshopPrompt.Plain(e)), 16);
            }

            var items = Workshop.Items;
            if (items.Count > 0)
            {
                NativeUi.Separator(content);
                NativeUi.Heading(content, $"创意工坊插件（{items.Count}）");
                if (Workshop.PendingCount > 0)
                {
                    var row = NativeUi.Row(content, 56f, 12f);
                    Block(row, Warn($"有 {Workshop.PendingCount} 个新订阅的插件等你确认才会加载"), 20);
                    NativeUi.Button(row, "去确认", true, 160f, () =>
                    {
                        Close();
                        WorkshopPrompt.Show();
                    });
                }
                foreach (var item in items)
                    Block(content, $"{WorkshopPrompt.Plain(item.DisplayName)}  <size=16><color=#a6a6a6>{WorkshopPrompt.Plain(item.Manifest?.Version)}</color></size>\n<size=16>{WorkshopSettings.StatusText(item)}</size>", 22);
            }

            var others = plugins.Where(p => !p.UsesModKit).ToList();
            if (others.Count > 0)
            {
                NativeUi.Separator(content);
                NativeUi.Heading(content, $"其他插件（{others.Count}）");
                Block(content, string.Join("\n", others.Select(p => $"{p.Name}  <size=16>{p.Version}</size>")), 18, NativeUi.DimText);
            }
        }

        private static string Summary()
        {
            string modKit = ModKit.Compatible
                ? Good("和游戏兼容")
                : Bad("和这个游戏版本不兼容，所有 Mod 已停用，请等 Mod 更新");
            return $"游戏版本 {ModKit.GameVersion}  ·  {ModKitPlugin.Name} {ModKit.Version} {modKit}\n"
                + Dim("装了 Mod 后成绩不上传排行榜（游戏作者的规则），其他功能不受影响。");
        }

        private static void AddModRow(ModInfo p)
        {
            var row = NativeUi.Row(content, 64f, 12f);
            var texts = NativeUi.Column(row, -1f, 0f);
            string tags = (p.HotReload ? "  " + Warn("[热重载]") : "") + (p.Workshop != null ? "  " + Dim("[创意工坊]") : "");
            NativeUi.Label(texts, $"{p.Name}  <size=16><color=#a6a6a6>{p.Version}</color></size>{tags}", 22);
            NativeUi.Label(texts, StateText(p), 16, -1f, NativeUi.DimText);
            if (ModSettings.CanOpen && p.State == ModState.Running)
            {
                string guid = p.Guid;
                NativeUi.Button(row, "设置", false, 130f, () => OpenSettings(guid));
            }
        }

        private static string StateText(ModInfo p)
        {
            switch (p.State)
            {
                case ModState.Running: return Good("运行中");
                case ModState.Failed: return Bad("启动出错，没有运行（详情见 BepInEx/LogOutput.log）");
                case ModState.Unloaded: return Bad("已卸载（热重载中，或被游戏销毁；重启游戏后恢复）");
                case ModState.Disabled: return Bad("已停用：DBModKit 和游戏版本不兼容");
                default: return Dim("不用 DBModKit 的插件");
            }
        }

        private static void OpenSettings(string guid)
        {
            Close();
            ModSettings.Open(guid);
        }

        private static void OpenPluginFolder()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{Paths.PluginPath}\"");
            }
            catch (Exception e)
            {
                Log.Error($"打开 Mod 文件夹失败：{e.Message}");
            }
        }

        // 多行文字：在竖排布局里高度随内容变化
        private static Text Block(Transform parent, string text, int size, Color? color = null)
        {
            var t = NativeUi.Label(parent, text, size, -1f, color);
            t.alignment = TextAnchor.UpperLeft;
            t.lineSpacing = 1.15f;
            return t;
        }

        private static string Good(string s) => $"<color=#9fd08a>{s}</color>";
        private static string Bad(string s) => $"<color=#e07a5f>{s}</color>";
        private static string Warn(string s) => $"<color=#d6b67d>{s}</color>";
        private static string Dim(string s) => $"<color=#a6a6a6>{s}</color>";
    }
}
