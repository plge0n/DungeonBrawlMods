using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DBModKit
{
    /// <summary>
    /// 原生风格 UI 控件：从游戏自己的 UI 预制体里取模板和贴图拼出来，外观、字体与游戏一致。
    /// 创建控件前先调用 <see cref="Load"/>。
    /// </summary>
    public static partial class NativeUi
    {
        public static readonly Color Gold = new Color(0.839f, 0.714f, 0.490f, 1f);
        public static readonly Color TextColor = new Color(0.92f, 0.90f, 0.85f, 1f);
        public static readonly Color DimText = new Color(0.65f, 0.65f, 0.65f, 1f);

        private static bool loaded;
        private static GameObject dropdownTemplate, sliderTemplate, confirmButton, cancelButton;
        // 取自游戏 UI 的贴图和字体，Load 成功后可用
        public static Sprite PanelSprite { get; private set; }
        public static Sprite BoxSprite { get; private set; }
        public static Sprite CheckSprite { get; private set; }
        public static Sprite GearSprite { get; private set; }
        public static Sprite CloseSprite { get; private set; }
        public static Color PanelColor { get; private set; }
        public static Font Font { get; private set; }

        private static bool failed;

        /// <summary>加载模板；失败时返回 false 且不再重试（原因写入日志）。</summary>
        public static bool Load()
        {
            if (loaded) return true;
            if (failed) return false;
            try
            {
                LoadTemplates();
            }
            catch (Exception e)
            {
                failed = true;
                Log.Error($"加载原生 UI 模板失败，原生风格控件不可用：{e.Message}");
            }
            return loaded;
        }

        private static void LoadTemplates()
        {
            var set = Resources.Load<GameObject>("Bundles/UI/Prefabs/ui_set");
            var battle = Resources.Load<GameObject>("Bundles/UI/Prefabs/ui_battleSetting");
            var confirm = Resources.Load<GameObject>("Bundles/UI/Prefabs/ui_confirm");
            if (set == null || battle == null || confirm == null)
                throw new Exception("找不到原生 UI 预制体，游戏可能更新了");

            dropdownTemplate = Find(set, "bg/trans_language");
            sliderTemplate = Find(set, "bg/slider_music");
            confirmButton = Find(confirm, "trans_bg/trans_main/btn_confirm");
            cancelButton = Find(confirm, "trans_bg/trans_main/btn_cancel");

            var teamItem = Find(battle, "trans_teamUI/trans_teamItem").GetComponent<Image>();
            PanelSprite = teamItem.sprite;
            PanelColor = teamItem.color;
            BoxSprite = Find(battle, "trans_teamUI/trans_teamItem/skillParent/skillItem/SPR_Background").GetComponent<Image>().sprite;
            CheckSprite = Find(set, "bg/trans_language/Template/Viewport/Content/Item/Item Checkmark").GetComponent<Image>().sprite;
            GearSprite = Find(battle, "trans_bg/trans_main/btn_set/Icon").GetComponent<Image>().sprite;
            CloseSprite = Find(battle, "trans_teamUI/trans_teamItem/KickOut/back").GetComponent<Image>().sprite;
            Font = dropdownTemplate.GetComponentInChildren<Text>(true).font;

            loaded = true;
        }

        private static GameObject Find(GameObject prefab, string path)
        {
            var t = prefab.transform.Find(path);
            if (t == null) throw new Exception($"原生 UI 中找不到 {prefab.name}/{path}");
            return t.gameObject;
        }

        // 克隆的原生控件带着原界面的点击事件和本地化组件，必须清掉
        private static GameObject Clone(GameObject template, Transform parent)
        {
            var go = UnityEngine.Object.Instantiate(template, parent, false);
            go.SetActive(true);
            // 原界面里控件带着缩放，搬过来会整体放大
            go.transform.localScale = Vector3.one;
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (c != null && c.GetType().Name == "LocalizationText")
                    UnityEngine.Object.DestroyImmediate(c);
            return go;
        }

        public static RectTransform Panel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = PanelSprite;
            img.type = Image.Type.Sliced;
            img.color = PanelColor;
            return (RectTransform)go.transform;
        }

        public static RectTransform Row(Transform parent, float height = 40f, float spacing = 10f)
        {
            var go = new GameObject("row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return (RectTransform)go.transform;
        }

        public static Text Label(Transform parent, string text, int size = 22, float width = -1f, Color? color = null)
        {
            var go = new GameObject("label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color ?? TextColor;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.text = text;
            var le = go.GetComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = width;
            else le.flexibleWidth = 1f;
            return t;
        }

        public static readonly Color BoxColor = new Color(0.133f, 0.153f, 0.196f, 1f);

        private static void StyleBox(Image img, float outlineAlpha = 0.6f)
        {
            img.sprite = BoxSprite;
            img.type = Image.Type.Sliced;
            img.color = BoxColor;
            var outline = img.GetComponent<Outline>() ?? img.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, outlineAlpha);
        }

        public static Toggle Toggle(Transform parent, string text, float width, Action<bool> onChange)
        {
            var row = Row(parent, 40f, 8f);
            var rowLe = row.GetComponent<LayoutElement>();
            rowLe.preferredWidth = rowLe.minWidth = width;
            rowLe.flexibleWidth = 0f;

            var box = new GameObject("box", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            box.transform.SetParent(row, false);
            var boxImg = box.GetComponent<Image>();
            StyleBox(boxImg, 1f);
            var le = box.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 32f;
            le.preferredHeight = 32f;

            var check = new GameObject("check", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(box.transform, false);
            var checkRt = (RectTransform)check.transform;
            checkRt.anchorMin = Vector2.zero;
            checkRt.anchorMax = Vector2.one;
            checkRt.offsetMin = new Vector2(-6f, -6f);
            checkRt.offsetMax = new Vector2(6f, 6f);
            var checkImg = check.GetComponent<Image>();
            checkImg.sprite = CheckSprite;
            checkImg.color = Gold;

            Label(row, text);

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            toggle.onValueChanged.AddListener(v => onChange(v));
            return toggle;
        }

        public static Slider Slider(Transform parent, float min, float max, bool whole, Action<float> onChange)
        {
            var go = Clone(sliderTemplate, parent);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = 32f;
            var slider = go.GetComponent<Slider>();
            slider.onValueChanged = new Slider.SliderEvent();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            // 音量滑条是 Unity 默认的灰白素材，换成游戏的深色底框 + 金色填充 + 金色菱形滑块
            var background = go.transform.Find("Background")?.GetComponent<Image>();
            if (background != null) StyleBox(background);
            if (slider.fillRect != null)
            {
                var fill = slider.fillRect.GetComponent<Image>();
                fill.color = new Color(Gold.r, Gold.g, Gold.b, 0.85f);
            }
            if (slider.handleRect != null)
            {
                slider.handleRect.sizeDelta = new Vector2(34f, 18f);
                var handle = slider.handleRect.GetComponent<Image>();
                handle.sprite = CheckSprite;
                handle.preserveAspect = true;
                handle.color = Gold;
            }
            slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        public static Dropdown Dropdown(Transform parent, IEnumerable<string> options, float width, Action<int> onChange)
        {
            var go = Clone(dropdownTemplate, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 38f;
            var dd = go.GetComponent<Dropdown>();
            // 原背景是一条装饰线，换成底框
            StyleBox(go.GetComponent<Image>());
            if (dd.captionText != null)
            {
                var cap = dd.captionText.rectTransform;
                cap.localScale = Vector3.one;
                cap.anchorMin = Vector2.zero;
                cap.anchorMax = Vector2.one;
                cap.offsetMin = new Vector2(14f, 0f);
                cap.offsetMax = new Vector2(-40f, 0f);
                dd.captionText.alignment = TextAnchor.MiddleLeft;
                dd.captionText.color = TextColor;
            }
            if (dd.template != null) StyleList(dd, options.Count());
            var arrow = go.transform.Find("Arrow") as RectTransform;
            if (arrow != null)
            {
                arrow.localScale = Vector3.one;
                arrow.anchorMin = arrow.anchorMax = new Vector2(1f, 0.5f);
                arrow.anchoredPosition = new Vector2(-20f, 0f);
                arrow.sizeDelta = new Vector2(22f, 22f);
            }
            dd.onValueChanged = new Dropdown.DropdownEvent();
            dd.ClearOptions();
            dd.AddOptions(options.ToList());
            foreach (var t in new[] { dd.captionText, dd.itemText })
            {
                if (t == null) continue;
                t.resizeTextForBestFit = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.fontSize = 22;
            }
            dd.onValueChanged.AddListener(v => onChange(v));
            return dd;
        }

        // 展开后的选项列表：原模板各层按 3 倍缩放设计，这里统一归一化后重新摆放
        private static void StyleList(Dropdown dd, int count)
        {
            const float itemHeight = 44f;
            var template = dd.template;
            foreach (var t in template.GetComponentsInChildren<RectTransform>(true))
                t.localScale = Vector3.one;

            var bg = template.GetComponent<Image>();
            if (bg != null)
            {
                bg.enabled = true;
                StyleBox(bg, 1f);
            }
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -4f);
            // Dropdown.Show 会把列表高度收缩到正好容纳所有选项，所以视口不能留内边距
            template.sizeDelta = new Vector2(0f, itemHeight * count);

            // 选项不多，不需要滚动条，视口铺满
            var scroll = template.GetComponent<ScrollRect>();
            if (scroll != null && scroll.verticalScrollbar != null)
            {
                scroll.verticalScrollbar.gameObject.SetActive(false);
                scroll.verticalScrollbar = null;
            }
            var viewport = template.Find("Viewport") as RectTransform;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            }
            var content = viewport?.Find("Content") as RectTransform;
            if (content != null) content.sizeDelta = new Vector2(0f, itemHeight);

            var item = template.GetComponentInChildren<Toggle>(true);
            if (item == null) return;
            // 原模板选项行向下偏移 7.25，会让所有选项错位、最后一项被裁掉
            var itemRt = (RectTransform)item.transform;
            itemRt.sizeDelta = new Vector2(0f, itemHeight);
            itemRt.anchoredPosition = Vector2.zero;

            // 原选项背景是一条装饰线，换成整行的淡金色高亮
            var itemBg = item.transform.Find("Item Background") as RectTransform;
            if (itemBg != null)
            {
                itemBg.anchorMin = Vector2.zero;
                itemBg.anchorMax = Vector2.one;
                itemBg.offsetMin = itemBg.offsetMax = Vector2.zero;
                var img = itemBg.GetComponent<Image>();
                img.sprite = null;
                img.color = Color.white;
                var colors = item.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0f);
                colors.highlightedColor = new Color(Gold.r, Gold.g, Gold.b, 0.25f);
                colors.pressedColor = new Color(Gold.r, Gold.g, Gold.b, 0.4f);
                colors.selectedColor = colors.highlightedColor;
                item.colors = colors;
            }
            var check = item.transform.Find("Item Checkmark") as RectTransform;
            if (check != null)
            {
                check.anchorMin = check.anchorMax = new Vector2(0f, 0.5f);
                check.anchoredPosition = new Vector2(20f, 0f);
                check.sizeDelta = new Vector2(22f, 22f);
                check.GetComponent<Image>().color = Gold;
            }
            if (dd.itemText != null)
            {
                var label = dd.itemText.rectTransform;
                label.anchorMin = Vector2.zero;
                label.anchorMax = Vector2.one;
                label.offsetMin = new Vector2(42f, 0f);
                label.offsetMax = new Vector2(-8f, 0f);
                dd.itemText.alignment = TextAnchor.MiddleLeft;
                dd.itemText.color = TextColor;
            }
        }

        public static Button Button(Transform parent, string text, bool primary, float width, UnityAction onClick)
        {
            var go = Clone(primary ? confirmButton : cancelButton, parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 52f;
            var label = go.GetComponentInChildren<Text>(true);
            label.text = text;
            label.fontSize = 22;
            var btn = go.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener(onClick);
            return btn;
        }

        public static Button IconButton(Transform parent, Sprite sprite, Vector2 size, UnityAction onClick)
        {
            var go = new GameObject("iconButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            return btn;
        }
    }
}
