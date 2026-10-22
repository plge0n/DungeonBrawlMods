using System;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio
{
    // 工作台里的一页
    internal interface IStudioPage
    {
        string Title { get; }

        // 切到这一页时调用；body 已清空，铺满正文区域
        void Build(RectTransform body);

        // 这一页显示期间每秒调用 4 次（游戏暂停时也会调用）
        void Refresh();
    }

    // 工作台窗口：页签切换、打开时屏蔽游戏输入，单人战斗中暂停
    internal static class Studio
    {
        private const float RefreshInterval = 0.25f;

        private static IStudioPage[] pages = new IStudioPage[0];
        private static Canvas canvas;
        private static NativeWindow window;
        private static RectTransform pageRoot;
        private static IStudioPage current;
        private static int lastTab;
        private static float nextRefresh;
        private static IDisposable inputBlock;
        private static IDisposable pause;

        public static bool IsOpen => window != null && window.IsOpen;

        // 弹出层（选择器等）挂在这里，盖在窗口上面；窗口没开时为 null
        public static Transform ModalHost => IsOpen ? canvas.transform : null;

        public static void Init(params IStudioPage[] all) => pages = all;

        // 下一帧就刷新当前页（页面要在控件回调之外重建自己时用）
        public static void RefreshSoon() => nextRefresh = 0f;

        public static void Tick()
        {
            if (Plugin.Hotkey.Value.IsDown() && !NativeUi.IsTyping)
            {
                if (IsOpen) Close();
                else Open();
                return;
            }
            if (!IsOpen) return;
            // 战斗中 ESC 已被输入屏蔽挡住，不会打开游戏菜单
            if (Input.GetKeyDown(KeyCode.Escape) && !NativeUi.IsTyping)
            {
                // 先关弹出层
                if (Picker.IsOpen) Picker.Close();
                else Close();
                return;
            }
            UpdatePause();
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshInterval;
                RefreshPage();
            }
        }

        public static void Open()
        {
            if (IsOpen || pages.Length == 0 || !NativeUi.Load()) return;
            if (canvas == null) canvas = NativeUi.Overlay("DBStudio");

            window = NativeUi.Window(canvas.transform, "DBStudio  <size=20><color=#a6a6a6>Mod 工作台</color></size>", new Vector2(1560f, 900f), Close);
            var body = window.Body.gameObject.AddComponent<VerticalLayoutGroup>();
            body.spacing = 10f;
            body.childControlWidth = body.childControlHeight = true;
            body.childForceExpandWidth = true;
            body.childForceExpandHeight = false;

            var tabs = NativeUi.Tabs(window.Body, pages.Select(p => p.Title).ToList(), ShowPage);
            var page = new GameObject("page", typeof(RectTransform), typeof(LayoutElement));
            page.transform.SetParent(window.Body, false);
            var le = page.GetComponent<LayoutElement>();
            le.flexibleWidth = le.flexibleHeight = 1f;
            pageRoot = (RectTransform)page.transform;

            inputBlock = GameInput.Block();
            tabs.Select(Mathf.Clamp(lastTab, 0, pages.Length - 1));
            UpdatePause();
        }

        public static void Close()
        {
            Picker.Close();
            window?.Destroy();
            window = null;
            pageRoot = null;
            current = null;
            inputBlock?.Dispose();
            inputBlock = null;
            pause?.Dispose();
            pause = null;
        }

        // 插件卸载（热重载）时调用
        public static void Shutdown()
        {
            Close();
            if (canvas != null) UnityEngine.Object.Destroy(canvas.gameObject);
            canvas = null;
        }

        private static void ShowPage(int index)
        {
            lastTab = index;
            current = pages[index];
            for (int i = pageRoot.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(pageRoot.GetChild(i).gameObject);

            var body = new GameObject(current.Title, typeof(RectTransform));
            body.transform.SetParent(pageRoot, false);
            var rt = (RectTransform)body.transform;
            NativeUi.Stretch(rt);
            try
            {
                current.Build(rt);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"工作台页面「{current.Title}」创建失败：{e}");
                NativeUi.Label(rt, $"这一页出错了，详情见 BepInEx/LogOutput.log\n{e.Message}", 22, -1f, NativeUi.DimText);
                current = null;
            }
            nextRefresh = 0f;
        }

        private static void RefreshPage()
        {
            if (window != null) window.Status.text = StatusText();
            if (current == null) return;
            try
            {
                current.Refresh();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"工作台页面「{current.Title}」刷新失败：{e}");
                current = null;
            }
        }

        private static void UpdatePause()
        {
            bool want = Plugin.PauseWhenSolo.Value && Game.IsSolo && Game.InBattle;
            if (want && pause == null) pause = GamePause.Acquire();
            else if (!want && pause != null)
            {
                pause.Dispose();
                pause = null;
            }
        }

        private static string StatusText()
        {
            string room = Game.IsSolo ? "单人"
                : Game.PlayerCount > 0 ? $"联机（{Game.PlayerCount} 人，你是房主）"
                : "联机（在别人的房间里）";
            string state = !Game.InBattle ? "不在战斗中"
                : pause != null ? "游戏已暂停"
                : Game.IsSolo ? "战斗中" : "战斗中（联机不暂停）";
            return $"{room}  ·  {state}  ·  {Plugin.Hotkey.Value} 或 ESC 关闭";
        }

        // ---- 页面共用 ----

        // 横向排列的几栏
        internal static RectTransform Columns(Transform parent, float spacing = 24f)
        {
            var go = new GameObject("columns", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            NativeUi.Stretch((RectTransform)go.transform);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return (RectTransform)go.transform;
        }

        // 多行文本块：放在 LayoutGroup 里时高度随内容变化
        internal static Text Block(Transform parent, int size = 20)
        {
            var t = NativeUi.Label(parent, "", size);
            t.alignment = TextAnchor.UpperLeft;
            t.lineSpacing = 1.15f;
            return t;
        }
    }
}
