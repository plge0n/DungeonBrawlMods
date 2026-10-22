using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DBModKit
{
    // 新订阅的工坊插件第一次加载前的确认窗口。放在 ModKit 里：没装 DBStudio 的玩家也要能确认
    internal static class WorkshopPrompt
    {
        private const string Warning =
            "创意工坊里的插件是别人写的程序，加载后能做任何事。只允许你信任的作者。\n选择会记住，之后可以在 DBStudio 的 Mod 页 → DBModKit 里修改。";

        private static Canvas canvas;
        private static NativeWindow window;
        private static IDisposable inputBlock;

        public static bool IsOpen => window != null && window.IsOpen;

        public static void Show()
        {
            if (IsOpen || !NativeUi.Load()) return;
            if (canvas == null) canvas = NativeUi.Overlay("DBModKit.Workshop", 6000);
            window = NativeUi.Window(canvas.transform, "新的创意工坊插件", new Vector2(1100f, 760f), Close);
            var body = window.Body.gameObject.AddComponent<VerticalLayoutGroup>();
            body.spacing = 10f;
            body.childControlWidth = body.childControlHeight = true;
            body.childForceExpandWidth = true;
            body.childForceExpandHeight = false;
            NativeUi.Label(window.Body, Warning, 20, -1f, NativeUi.Gold);
            var content = NativeUi.ScrollView(window.Body, out _, 8f);
            foreach (var item in Workshop.Items.Where(i => i.Status == WorkshopStatus.Pending).ToList())
                AddRow(content, item);
            var bottom = NativeUi.Row(window.Body, 56f, 16f);
            bottom.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            NativeUi.Button(bottom, "稍后再说", false, 200f, Close);
            window.Status.text = "稍后再说：这次不加载，下次启动游戏还会问";
            inputBlock = GameInput.Block();
        }

        public static void Close()
        {
            window?.Destroy();
            window = null;
            inputBlock?.Dispose();
            inputBlock = null;
        }

        private static void AddRow(Transform parent, WorkshopItem item)
        {
            var row = NativeUi.Row(parent, 90f, 14f);
            var texts = NativeUi.Column(row, -1f, 2f);
            var m = item.Manifest;
            NativeUi.Label(texts, $"{Plain(item.DisplayName)}  <size=18><color=#a6a6a6>{Plain(m.Version)}{(string.IsNullOrEmpty(m.Author) ? "" : "  作者 " + Plain(m.Author))}</color></size>", 24);
            if (!string.IsNullOrEmpty(m.Description)) NativeUi.Label(texts, Plain(m.Description), 18, -1f, NativeUi.DimText);
            NativeUi.Label(texts, $"工坊编号 {item.Id}", 16, -1f, NativeUi.DimText);
            Button allow = null, decline = null;
            allow = NativeUi.Button(row, "允许", true, 150f, () => Decide(item, true, allow, decline));
            decline = NativeUi.Button(row, "不加载", false, 150f, () => Decide(item, false, allow, decline));
        }

        // 条目作者写的文字不能带富文本标签，否则能伪装成"官方"或把字缩到看不见
        public static string Plain(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "＜").Replace(">", "＞");

        private static void Decide(WorkshopItem item, bool allow, Button a, Button b)
        {
            Workshop.Decide(item, allow);
            NativeUi.SetEnabled(a, false);
            NativeUi.SetEnabled(b, false);
            var label = (allow ? a : b).GetComponentInChildren<Text>();
            if (allow) label.text = item.Status == WorkshopStatus.Loaded ? "已加载" : "加载失败";
            else label.text = "不加载";
            if (Workshop.PendingCount == 0) window.Status.text = "都选好了，可以关掉这个窗口";
        }
    }

    // DBStudio 的 Mod 页 → DBModKit 里的"创意工坊插件"列表
    internal static class WorkshopSettings
    {
        private static RectTransform list;
        private static int shownVersion = -1;

        public static void Register() => ModSettings.Register(ModKitPlugin.Guid, "创意工坊插件", Build, Refresh);

        private static void Build(RectTransform container)
        {
            NativeUi.Label(container, $"订阅的插件在 {Workshop.ContentFolder}\n本地测试：把条目文件夹放进 {Workshop.DevFolder}（不用确认）", 16, -1f, NativeUi.DimText);
            var col = NativeUi.Column(container, -1f, 6f);
            col.GetComponent<LayoutElement>().flexibleHeight = 0f;
            list = col;
            shownVersion = -1;
            Refresh();
        }

        private static void Refresh()
        {
            if (list == null || shownVersion == Workshop.Version) return;
            shownVersion = Workshop.Version;
            for (int i = list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
            if (Workshop.Items.Count == 0)
            {
                NativeUi.Label(list, "没有订阅创意工坊插件", 18, -1f, NativeUi.DimText);
                return;
            }
            foreach (var item in Workshop.Items)
            {
                var row = NativeUi.Row(list, 64f, 12f);
                var texts = NativeUi.Column(row, -1f, 0f);
                NativeUi.Label(texts, $"{WorkshopPrompt.Plain(item.DisplayName)}  <size=16><color=#a6a6a6>{WorkshopPrompt.Plain(item.Manifest?.Version)}  {item.Id}</color></size>", 22);
                NativeUi.Label(texts, StatusText(item), 16, -1f, NativeUi.DimText);
                if (item.IsDev || item.Broken) continue;
                // 按玩家的选择显示，不按运行状态：已加载的条目设为不加载后，还能改回来
                bool allowed = Workshop.IsAllowed(item);
                var target = item;
                // 回调里会改 Workshop.Version，列表在下一次 Refresh 重建，不在回调里销毁按钮
                NativeUi.Button(row, allowed ? "设为不加载" : "允许并加载", !allowed, 190f, () => Workshop.Decide(target, !allowed));
            }
        }

        internal static string StatusText(WorkshopItem item)
        {
            string status;
            switch (item.Status)
            {
                case WorkshopStatus.Loaded: status = "<color=#9fd08a>已加载</color>"; break;
                case WorkshopStatus.Pending: status = "<color=#d6b67d>等你确认</color>"; break;
                case WorkshopStatus.Declined: status = "不加载"; break;
                default: status = "<color=#e07a5f>没有加载</color>"; break;
            }
            if (item.IsDev) status += "（本地测试）";
            return string.IsNullOrEmpty(item.Message) ? status : $"{status}：{WorkshopPrompt.Plain(item.Message)}";
        }
    }
}
