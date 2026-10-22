using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBModKit
{
    /// <summary>大窗口：全屏半透明遮罩 + 居中面板（标题栏、正文、底部状态栏）。</summary>
    public sealed class NativeWindow
    {
        /// <summary>全屏遮罩，销毁它就关掉整个窗口。</summary>
        public RectTransform Root { get; internal set; }
        public RectTransform Frame { get; internal set; }
        /// <summary>标题栏（标题文字、关闭按钮），可以往里加按钮。</summary>
        public RectTransform Header { get; internal set; }
        /// <summary>正文区域，填满标题栏和状态栏之间的空间；子物体自己设锚点铺满。</summary>
        public RectTransform Body { get; internal set; }
        public Text Title { get; internal set; }
        /// <summary>底部的一行灰色提示。</summary>
        public Text Status { get; internal set; }

        public bool IsOpen => Root != null;

        public void Destroy()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
            Root = null;
        }
    }

    /// <summary>一排页签，同时只有一个选中。</summary>
    public sealed class NativeTabs
    {
        private readonly List<(Image bg, Text label, GameObject underline)> tabs = new List<(Image, Text, GameObject)>();
        private readonly Action<int> onSelect;

        public int Selected { get; private set; } = -1;
        public RectTransform Root { get; internal set; }

        internal NativeTabs(Action<int> onSelect) => this.onSelect = onSelect;

        internal void Add(Image bg, Text label, GameObject underline) => tabs.Add((bg, label, underline));

        /// <summary>选中第 index 个页签并触发回调（已选中时不重复触发）。</summary>
        public void Select(int index)
        {
            if (index == Selected || index < 0 || index >= tabs.Count) return;
            Selected = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i == index;
                tabs[i].bg.color = on ? NativeUi.BoxColor : new Color(0f, 0f, 0f, 0f);
                tabs[i].label.color = on ? NativeUi.Gold : NativeUi.DimText;
                tabs[i].underline.SetActive(on);
            }
            onSelect?.Invoke(index);
        }
    }

    public static partial class NativeUi
    {
        /// <summary>
        /// 自己的全屏 Canvas，总是盖在游戏 UI 上面。缩放设置照抄游戏 UI，所以字号、尺寸和游戏界面一致。
        /// 切场景不会销毁；不用了自己 Destroy。
        /// </summary>
        public static Canvas Overlay(string name, int sortingOrder = 5000)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            var game = ModKit.Compatible ? GameRaw.UiCanvas() : null;
            var gameScaler = game != null ? game.GetComponentInParent<CanvasScaler>() : null;
            if (gameScaler != null)
            {
                scaler.uiScaleMode = gameScaler.uiScaleMode;
                scaler.referenceResolution = gameScaler.referenceResolution;
                scaler.screenMatchMode = gameScaler.screenMatchMode;
                scaler.matchWidthOrHeight = gameScaler.matchWidthOrHeight;
                scaler.scaleFactor = gameScaler.scaleFactor;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            return canvas;
        }

        /// <summary>居中的大窗口，带全屏遮罩（挡住背后的点击）。size 是参考分辨率下的尺寸，超出屏幕时自动缩小。</summary>
        public static NativeWindow Window(Transform parent, string title, Vector2 size, UnityAction onClose)
        {
            var backdrop = new GameObject("window", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(parent, false);
            var root = (RectTransform)backdrop.transform;
            Stretch(root);
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var frame = Panel(root, "frame");
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            var parentSize = parent is RectTransform prt ? prt.rect.size : size;
            frame.sizeDelta = new Vector2(
                parentSize.x > 0f ? Mathf.Min(size.x, parentSize.x - 40f) : size.x,
                parentSize.y > 0f ? Mathf.Min(size.y, parentSize.y - 40f) : size.y);
            var layout = frame.gameObject.AddComponent<VerticalLayoutGroup>();
            // 面板贴图的上下边缘有装饰，内容要让开，否则标题和底部状态栏会压在边框上
            layout.padding = new RectOffset(28, 28, 30, 36);
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = Row(frame, 52f);
            var titleText = Label(header, title, 30, -1f, Gold);
            titleText.fontStyle = FontStyle.Bold;
            var close = IconButton(header, CloseSprite, new Vector2(44f, 44f), onClose);
            var closeLe = close.gameObject.AddComponent<LayoutElement>();
            closeLe.preferredWidth = closeLe.minWidth = 44f;
            Separator(frame);

            var body = new GameObject("body", typeof(RectTransform), typeof(LayoutElement));
            body.transform.SetParent(frame, false);
            var bodyLe = body.GetComponent<LayoutElement>();
            bodyLe.flexibleHeight = bodyLe.flexibleWidth = 1f;

            var status = Label(frame, "", 18, -1f, DimText);
            status.GetComponent<LayoutElement>().preferredHeight = 26f;

            return new NativeWindow
            {
                Root = root,
                Frame = frame,
                Header = header,
                Body = (RectTransform)body.transform,
                Title = titleText,
                Status = status,
            };
        }

        /// <summary>一排页签。创建后调用 Select(0) 选中第一个。</summary>
        public static NativeTabs Tabs(Transform parent, IList<string> labels, Action<int> onSelect, float tabWidth = 170f)
        {
            var bar = Row(parent, 46f, 4f);
            var tabs = new NativeTabs(onSelect) { Root = bar };
            for (int i = 0; i < labels.Count; i++)
            {
                int index = i;
                var go = new GameObject("tab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(bar, false);
                var le = go.GetComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = tabWidth;
                var bg = go.GetComponent<Image>();
                bg.sprite = BoxSprite;
                bg.type = Image.Type.Sliced;
                var btn = go.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => tabs.Select(index));

                var label = Label(go.transform, labels[i], 24);
                label.alignment = TextAnchor.MiddleCenter;
                Stretch(label.rectTransform);

                var underline = new GameObject("underline", typeof(RectTransform), typeof(Image));
                underline.transform.SetParent(go.transform, false);
                var ul = (RectTransform)underline.transform;
                ul.anchorMin = new Vector2(0.1f, 0f);
                ul.anchorMax = new Vector2(0.9f, 0f);
                ul.pivot = new Vector2(0.5f, 0f);
                ul.sizeDelta = new Vector2(0f, 3f);
                underline.GetComponent<Image>().color = Gold;

                tabs.Add(bg, label, underline);
            }
            return tabs;
        }

        /// <summary>
        /// 竖向滚动列表，返回放内容的容器（子物体自上而下排列，高度自动撑开）。
        /// 自己设置返回容器父级（scroll.transform）的布局：在 LayoutGroup 里默认填满剩余空间。
        /// </summary>
        public static RectTransform ScrollView(Transform parent, out ScrollRect scroll, float spacing = 6f, bool boxed = true)
        {
            var go = new GameObject("scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = le.flexibleHeight = 1f;
            var bg = go.GetComponent<Image>();
            if (boxed) StyleBox(bg, 0.35f);
            else bg.color = new Color(0f, 0f, 0f, 0f); // 透明但接收滚轮

            var viewport = new GameObject("viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(go.transform, false);
            var vp = (RectTransform)viewport.transform;
            Stretch(vp);
            vp.offsetMin = new Vector2(10f, 8f);
            vp.offsetMax = new Vector2(-22f, -8f);

            var content = new GameObject("content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(vp, false);
            var ct = (RectTransform)content.transform;
            ct.anchorMin = new Vector2(0f, 1f);
            ct.anchorMax = new Vector2(1f, 1f);
            ct.pivot = new Vector2(0.5f, 1f);
            ct.sizeDelta = Vector2.zero;
            var v = content.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = new GameObject("scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            bar.transform.SetParent(go.transform, false);
            var barRt = (RectTransform)bar.transform;
            barRt.anchorMin = new Vector2(1f, 0f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(1f, 0.5f);
            barRt.sizeDelta = new Vector2(8f, -16f);
            barRt.anchoredPosition = new Vector2(-8f, 0f);
            bar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            var handle = new GameObject("handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(barRt, false);
            var handleRt = (RectTransform)handle.transform;
            Stretch(handleRt);
            handle.GetComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.7f);
            var scrollbar = bar.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handle.GetComponent<Image>();

            scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = vp;
            scroll.content = ct;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return ct;
        }

        /// <summary>单行输入框（支持中文输入法）。onEndEdit 在回车或失去焦点时触发。</summary>
        public static InputField Input(Transform parent, string placeholder, float width, Action<string> onEndEdit,
            InputField.ContentType type = InputField.ContentType.Standard)
        {
            var go = new GameObject("input", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = le.minWidth = width;
            else le.flexibleWidth = 1f;
            le.preferredHeight = 38f;
            var bg = go.GetComponent<Image>();
            StyleBox(bg);

            Text MakeText(string name, Color color)
            {
                var t = Label(go.transform, "", 22, -1f, color);
                t.name = name;
                t.supportRichText = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                Destroy(t.GetComponent<LayoutElement>());
                var rt = t.rectTransform;
                Stretch(rt);
                rt.offsetMin = new Vector2(12f, 2f);
                rt.offsetMax = new Vector2(-12f, -2f);
                return t;
            }

            var text = MakeText("text", TextColor);
            var hint = MakeText("placeholder", DimText);
            hint.text = placeholder ?? "";
            hint.fontStyle = FontStyle.Italic;

            var input = go.GetComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = text;
            input.placeholder = hint;
            input.contentType = type;
            input.caretColor = Gold;
            input.customCaretColor = true;
            input.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            if (onEndEdit != null) input.onEndEdit.AddListener(v => onEndEdit(v));
            return input;
        }

        /// <summary>金色小标题。</summary>
        public static Text Heading(Transform parent, string text)
        {
            var t = Label(parent, text, 24, -1f, Gold);
            t.fontStyle = FontStyle.Bold;
            t.GetComponent<LayoutElement>().preferredHeight = 34f;
            return t;
        }

        /// <summary>一条淡金色横线。</summary>
        public static Image Separator(Transform parent)
        {
            var go = new GameObject("separator", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = 2f;
            var img = go.GetComponent<Image>();
            img.color = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>竖向排列的容器。width ≤ 0 时平分剩余宽度。</summary>
        public static RectTransform Column(Transform parent, float width = -1f, float spacing = 8f)
        {
            var go = new GameObject("column", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var le = go.GetComponent<LayoutElement>();
            // 定宽时 flexibleWidth 必须显式设 0，否则会继承子控件（比如滚动框）的可拉伸，一起分剩余宽度
            if (width > 0)
            {
                le.preferredWidth = le.minWidth = width;
                le.flexibleWidth = 0f;
            }
            else le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
            return (RectTransform)go.transform;
        }

        /// <summary>启用/禁用按钮，禁用时变暗（克隆的原生按钮禁用后外观不变）。</summary>
        public static void SetEnabled(Selectable control, bool enabled)
        {
            control.interactable = enabled;
            var group = control.GetComponent<CanvasGroup>() ?? control.gameObject.AddComponent<CanvasGroup>();
            group.alpha = enabled ? 1f : 0.4f;
        }

        /// <summary>正在往输入框里打字（这时不要响应快捷键）。</summary>
        public static bool IsTyping
        {
            get
            {
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                return selected != null && selected.GetComponent<InputField>() is InputField f && f.isFocused;
            }
        }

        /// <summary>让 RectTransform 铺满父物体。</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Destroy(UnityEngine.Object o)
        {
            if (o != null) UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
