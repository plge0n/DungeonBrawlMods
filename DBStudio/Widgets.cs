using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio
{
    // 各页共用的编辑控件
    internal static class Widgets
    {
        public static Dropdown Choice(Transform parent, IEnumerable<string> options, int index, float width, Action<int> onChange)
        {
            var dd = NativeUi.Dropdown(parent, options, width, onChange);
            dd.SetValueWithoutNotify(Mathf.Max(0, index));
            return dd;
        }

        // 非负数输入框；set 返回实际存下的值（可能被限制过），输入框显示它；输入无效时恢复原来的值
        public static InputField Number(Transform parent, double value, float width, Func<double, double> set, Action changed, bool whole = false)
        {
            double current = value;
            InputField field = null;
            field = NativeUi.Input(parent, "", width, text =>
            {
                if (TryParse(text, out double v))
                {
                    v = Math.Max(0.0, whole ? Math.Round(v) : v);
                    if (v != current)
                    {
                        current = set(v);
                        changed();
                    }
                }
                field.SetTextWithoutNotify(RuleText.Num(current));
            }, InputField.ContentType.DecimalNumber);
            field.SetTextWithoutNotify(RuleText.Num(current));
            return field;
        }

        public static bool TryParse(string text, out double v)
            => double.TryParse((text ?? "").Trim().Replace('，', '.').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
               && !double.IsNaN(v) && !double.IsInfinity(v);

        // 文字输入框；空白时用 fallback
        public static InputField Text(Transform parent, string value, string placeholder, float width, Action<string> set, string fallback = null)
        {
            InputField field = null;
            field = NativeUi.Input(parent, placeholder, width, v =>
            {
                string text = string.IsNullOrWhiteSpace(v) ? fallback ?? "" : v.Trim();
                set(text);
                field.SetTextWithoutNotify(text);
            });
            field.SetTextWithoutNotify(value ?? "");
            return field;
        }

        // 一个动作：[类型▼][数值][单位][持续 x 秒]……[删除]；"获得遗物"用选择器挑遗物
        public static void ActionRow(Transform parent, RuleAction a, Action changed, Action structureChanged, Action delete)
        {
            var row = NativeUi.Row(parent, 44f, 12f);
            var (_, unit) = RuleText.Label(a.Type);
            Choice(row, RuleText.Actions.Select(x => x.label), Array.FindIndex(RuleText.Actions, x => x.value == a.Type), 260f, i =>
            {
                var type = RuleText.Actions[i].value;
                // 遗物用 Value 存编号，和数值互不相通：切换时清掉，免得默认值 10 变成"已选 10 号遗物"
                if ((type == ActionType.Relic) != (a.Type == ActionType.Relic)) a.Value = type == ActionType.Relic ? 0 : 10;
                a.Type = type;
                structureChanged();
            });
            switch (a.Type)
            {
                case ActionType.Notice:
                    Text(row, a.Text, "要显示的文字", -1f, v =>
                    {
                        a.Text = v;
                        changed();
                    });
                    break;
                case ActionType.Relic:
                    var relic = RelicInfo.Get((int)a.Value);
                    NativeUi.Button(row, relic?.Name ?? "选一个遗物", relic == null, 300f, () => Picker.Show("选择遗物", Picker.Relics(), item =>
                    {
                        a.Value = int.Parse(item.Key);
                        structureChanged();
                    }));
                    Spacer(row);
                    break;
                default:
                    Number(row, a.Value, 110f, v => a.Value = v, changed);
                    NativeUi.Label(row, unit, 22, 30f);
                    if (RuleText.IsStat(a.Type))
                    {
                        NativeUi.Label(row, "持续", 22, 50f);
                        Number(row, a.Seconds, 90f, v => a.Seconds = v, changed);
                        NativeUi.Label(row, "秒（0 = 本局）", 18, 130f, NativeUi.DimText);
                    }
                    Spacer(row);
                    break;
            }
            NativeUi.Button(row, "删除", false, 100f, () => delete());
        }

        public static void AddButton(Transform parent, string text, Action onClick)
        {
            var row = NativeUi.Row(parent, 52f);
            NativeUi.Button(row, text, false, 220f, () => onClick());
        }

        public static void Spacer(Transform parent)
        {
            var go = new GameObject("spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        // 固定大小的图标
        public static Image Icon(Transform parent, Sprite sprite, float size)
        {
            var go = new GameObject("icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size;
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.sprite = sprite;
            img.enabled = sprite != null;
            return img;
        }
    }

    // 带搜索的图标选择器，盖在工作台窗口上；选完、点关闭或按 ESC 关闭
    internal static class Picker
    {
        public sealed class Item
        {
            public string Key, Name, IconPath;
            public Sprite Icon;
        }

        private static RectTransform root;

        public static bool IsOpen => root != null;

        public static void Close()
        {
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null;
        }

        public static IEnumerable<Item> Relics()
            => RelicInfo.All.Select(r => new Item { Key = r.Id.ToString(), Name = r.Name, IconPath = r.IconPath, Icon = r.Icon });

        public static IEnumerable<Item> SkillIcons()
            => GameData.Entries(DataTable.Skill).Where(e => e.Icon != null)
                .Select(e => new Item { Key = e.Key, Name = e.Name, IconPath = e.IconPath, Icon = e.Icon });

        public static void Show(string title, IEnumerable<Item> items, Action<Item> onPick)
        {
            Close();
            var host = Studio.ModalHost;
            if (host == null) return;
            var all = items.ToList();
            var window = NativeUi.Window(host, title, new Vector2(1100f, 760f), Close);
            root = window.Root;
            root.SetAsLastSibling();
            var body = window.Body.gameObject.AddComponent<VerticalLayoutGroup>();
            body.spacing = 10f;
            body.childControlWidth = body.childControlHeight = true;
            body.childForceExpandWidth = true;
            body.childForceExpandHeight = false;

            var search = NativeUi.Input(window.Body, "搜索名字", -1f, null);
            var content = NativeUi.ScrollView(window.Body, out _, 0f);
            var gridGo = new GameObject("grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(content, false);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(120f, 128f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            // 8 列的最小宽度会超过视口一点，最右一列会被裁掉
            grid.constraintCount = 7;

            // 格子只建一次，搜索时只切换显示
            var cells = new List<(Item item, GameObject cell)>();
            foreach (var item in all)
            {
                var picked = item;
                var cell = new GameObject(item.Key, typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup));
                cell.transform.SetParent(gridGo.transform, false);
                var bg = cell.GetComponent<Image>();
                bg.sprite = NativeUi.BoxSprite;
                bg.type = Image.Type.Sliced;
                bg.color = NativeUi.BoxColor;
                var v = cell.GetComponent<VerticalLayoutGroup>();
                v.padding = new RectOffset(6, 6, 8, 4);
                v.spacing = 4f;
                v.childAlignment = TextAnchor.UpperCenter;
                v.childControlWidth = v.childControlHeight = true;
                v.childForceExpandWidth = false;
                v.childForceExpandHeight = false;
                Widgets.Icon(cell.transform, item.Icon, 72f);
                var label = NativeUi.Label(cell.transform, item.Name, 16);
                label.alignment = TextAnchor.UpperCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.GetComponent<LayoutElement>().preferredHeight = 38f;
                var btn = cell.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() =>
                {
                    Close();
                    onPick(picked);
                });
                cells.Add((item, cell));
            }

            search.onValueChanged.AddListener(filter =>
            {
                foreach (var (item, cell) in cells)
                    cell.SetActive(string.IsNullOrEmpty(filter) || (item.Name ?? "").Contains(filter));
            });
            window.Status.text = $"共 {all.Count} 个";
        }
    }
}
