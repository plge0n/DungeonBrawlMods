using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio.Pages
{
    // 数值：改技能、英雄、怪物表里的数值。左边选一项，右边逐个字段改，每个字段都显示原版值、可以单独恢复
    internal sealed class ValuesPage : IStudioPage
    {
        private static readonly DataTable[] Tables = { DataTable.Skill, DataTable.Hero, DataTable.Enemy };
        private static readonly string[] TableLabels = { "技能", "英雄", "怪物" };

        private static readonly Dictionary<DataTable, string> TimingHints = new Dictionary<DataTable, string>
        {
            [DataTable.Skill] = "伤害、范围：下次释放就生效。冷却、耗蓝：已经学会的技能要重新获得才生效。",
            [DataTable.Hero] = "下一局开局时生效，当前这局不变。",
            [DataTable.Enemy] = "按百分比调整，100% 是原版。之后刷出来的怪物生效。",
        };

        // 切页再回来时保持选择
        private static DataTable table = DataTable.Skill;
        private static string selectedKey;
        private static string filter = "";

        private sealed class FieldRow
        {
            public DataField Field;
            public Text Original;
            public InputField Input;
            public Button Reset;
        }

        private RectTransform listContent, fieldsContent;
        private Image headIcon;
        private Text headTitle, timingHint, state;
        private Button resetEntry, resetAll;
        private Toggle enabledToggle;
        private float resetAllArmedUntil;
        private bool listBuilt;
        private readonly Dictionary<string, Text> entryLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, Image> entryBackgrounds = new Dictionary<string, Image>();
        private readonly List<FieldRow> rows = new List<FieldRow>();

        public string Title => "数值";

        public void Build(RectTransform body)
        {
            listBuilt = false;
            resetAllArmedUntil = 0f;
            var columns = Studio.Columns(body);

            var left = NativeUi.Column(columns, 470f);
            var tabs = NativeUi.Tabs(left, TableLabels, i =>
            {
                if (Tables[i] == table && listBuilt) return;
                if (Tables[i] != table) selectedKey = null;
                table = Tables[i];
                RebuildList();
                ShowEntry();
            }, 145f);
            var search = NativeUi.Input(left, "搜索名字", -1f, null);
            search.text = filter;
            search.onValueChanged.AddListener(v =>
            {
                filter = v ?? "";
                RebuildList();
            });
            listContent = NativeUi.ScrollView(left, out _, 4f);

            var right = NativeUi.Column(columns);
            var head = NativeUi.Row(right, 64f, 14f);
            var icon = new GameObject("icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(head, false);
            var iconLe = icon.GetComponent<LayoutElement>();
            iconLe.preferredWidth = iconLe.minWidth = 60f;
            headIcon = icon.GetComponent<Image>();
            headIcon.preserveAspect = true;
            headTitle = NativeUi.Label(head, "", 30, -1f, NativeUi.Gold);
            headTitle.fontStyle = FontStyle.Bold;
            timingHint = NativeUi.Label(right, "", 18, -1f, NativeUi.DimText);
            NativeUi.Separator(right);
            fieldsContent = NativeUi.ScrollView(right, out _, 6f, boxed: false);

            NativeUi.Separator(right);
            var bottom = NativeUi.Row(right, 56f, 16f);
            enabledToggle = NativeUi.Toggle(bottom, "启用玩法包", 230f, v => Packs.SetEnabled(v));
            enabledToggle.isOn = Packs.Current.Enabled;
            state = NativeUi.Label(bottom, "", 18, -1f, NativeUi.DimText);
            resetEntry = NativeUi.Button(bottom, "恢复这一项", false, 190f, () =>
            {
                if (selectedKey == null) return;
                GameData.Reset(table, selectedKey);
                UpdateValues();
            });
            resetAll = NativeUi.Button(bottom, "恢复全部", false, 190f, ResetAll);

            tabs.Select(System.Array.IndexOf(Tables, table));
        }

        public void Refresh()
        {
            // 主菜单之前打开时表还没加载
            if (!listBuilt && GameData.Ready)
            {
                RebuildList();
                ShowEntry();
            }
            string reason = Packs.InactiveReason;
            state.text = reason == null ? "<color=#9fd08a>生效中（单人）</color>" : reason;
            if (resetAllArmedUntil > 0f && Time.unscaledTime > resetAllArmedUntil) DisarmResetAll();
        }

        private void RebuildList()
        {
            for (int i = listContent.childCount - 1; i >= 0; i--)
                Object.Destroy(listContent.GetChild(i).gameObject);
            entryLabels.Clear();
            entryBackgrounds.Clear();
            listBuilt = GameData.Ready;
            if (!listBuilt)
            {
                Studio.Block(listContent).text = "游戏还没加载完，进入主菜单后再看";
                return;
            }

            var entries = GameData.Entries(table)
                .Where(e => filter == "" || e.Name.Contains(filter) || e.Key.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (entries.Count == 0)
            {
                Studio.Block(listContent).text = "没有找到";
                return;
            }
            // 改过的排在前面
            foreach (var e in entries.OrderByDescending(e => GameData.IsModified(table, e.Key)))
                AddEntry(e);
            UpdateSelection();
        }

        private void AddEntry(DataEntry e)
        {
            var go = new GameObject(e.Key, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(listContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 52f;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(8, 8, 4, 4);
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            var bg = go.GetComponent<Image>();
            bg.sprite = NativeUi.BoxSprite;
            bg.type = Image.Type.Sliced;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            string key = e.Key;
            btn.onClick.AddListener(() =>
            {
                selectedKey = key;
                UpdateSelection();
                ShowEntry();
            });

            var icon = new GameObject("icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(go.transform, false);
            var le = icon.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 44f;
            var img = icon.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.sprite = e.Icon;
            img.enabled = e.Icon != null;

            entryLabels[key] = NativeUi.Label(go.transform, EntryText(e), 22);
            entryBackgrounds[key] = bg;
        }

        private string EntryText(DataEntry e)
            => GameData.IsModified(table, e.Key) ? $"{e.Name}  <color=#d6b67d>● 已改</color>" : e.Name;

        private void UpdateSelection()
        {
            foreach (var kv in entryBackgrounds)
                kv.Value.color = kv.Key == selectedKey ? NativeUi.BoxColor : new Color(0f, 0f, 0f, 0f);
        }

        // 右边：选中这一项的所有字段
        private void ShowEntry()
        {
            for (int i = fieldsContent.childCount - 1; i >= 0; i--)
                Object.Destroy(fieldsContent.GetChild(i).gameObject);
            rows.Clear();
            var entry = selectedKey == null ? null : GameData.Entries(table).FirstOrDefault(e => e.Key == selectedKey);
            timingHint.text = TimingHints[table] + (table == DataTable.Enemy && !GameData.Available
                ? "\n<color=#e07a5f>怪物倍率的钩子没装上：按出场次数单独设定数值的怪物（多见于挑战、Boss）不会乘倍率。</color>" : "");
            headIcon.enabled = entry?.Icon != null;
            headIcon.sprite = entry?.Icon;
            NativeUi.SetEnabled(resetEntry, entry != null);
            if (entry == null)
            {
                headTitle.text = "";
                Studio.Block(fieldsContent).text = GameData.Ready ? "在左边选一项" : "";
                return;
            }
            headTitle.text = $"{entry.Name}  <size=18><color=#a6a6a6>{entry.Key}</color></size>";

            foreach (var field in GameData.Fields(table))
            {
                var row = NativeUi.Row(fieldsContent, 66f, 14f);
                var labels = NativeUi.Column(row, 380f, 0f);
                NativeUi.Label(labels, field.Label, 22);
                NativeUi.Label(labels, field.Hint, 16, -1f, NativeUi.DimText);

                var r = new FieldRow { Field = field };
                r.Original = NativeUi.Label(row, "", 18, 170f, NativeUi.DimText);
                r.Input = NativeUi.Input(row, "", 170f, v => OnEdit(r, v), InputField.ContentType.DecimalNumber);
                r.Input.GetComponent<LayoutElement>().preferredHeight = 44f;
                NativeUi.Label(row, field.IsScale ? "%" : "", 22, 30f);
                r.Reset = NativeUi.Button(row, "恢复", false, 110f, () =>
                {
                    GameData.Reset(table, selectedKey, field.Key);
                    UpdateValues();
                });
                rows.Add(r);
            }
            UpdateValues();
        }

        private void OnEdit(FieldRow r, string text)
        {
            if (selectedKey == null) return;
            // 失去焦点也会触发：文字没动就别把四舍五入后的显示值当成新值
            if (text == Format(r.Field, GameData.Get(table, selectedKey, r.Field.Key))) return;
            if (TryParse(text, out double v)) GameData.Set(table, selectedKey, r.Field.Key, r.Field.IsScale ? v / 100.0 : v);
            UpdateValues(); // 输入无效时也会把文字改回当前值
        }

        // 字段数值、"已改"标记、恢复按钮的状态
        private void UpdateValues()
        {
            if (selectedKey == null) return;
            foreach (var r in rows)
            {
                double original = GameData.Original(table, selectedKey, r.Field.Key);
                double current = GameData.Get(table, selectedKey, r.Field.Key);
                bool modified = GameData.IsModified(table, selectedKey, r.Field.Key);
                r.Original.text = "原版 " + Format(r.Field, original);
                r.Input.SetTextWithoutNotify(Format(r.Field, current));
                r.Input.textComponent.color = modified ? NativeUi.Gold : NativeUi.TextColor;
                NativeUi.SetEnabled(r.Reset, modified);
            }
            var entry = GameData.Entries(table).FirstOrDefault(e => e.Key == selectedKey);
            if (entry != null && entryLabels.TryGetValue(selectedKey, out var label)) label.text = EntryText(entry);
        }

        // 点一次变成"再点一次确认"，3 秒内再点才清空
        private void ResetAll()
        {
            if (resetAllArmedUntil <= 0f)
            {
                resetAllArmedUntil = Time.unscaledTime + 3f;
                resetAll.GetComponentInChildren<Text>().text = "再点一次确认";
                return;
            }
            DisarmResetAll();
            GameData.Load(null);
            RebuildList();
            ShowEntry();
            EventLog.Add("数值改动已全部恢复原版");
        }

        private void DisarmResetAll()
        {
            resetAllArmedUntil = 0f;
            resetAll.GetComponentInChildren<Text>().text = "恢复全部";
        }

        private static string Format(DataField f, double v)
        {
            if (double.IsNaN(v)) return "—";
            if (f.IsScale) return (v * 100.0).ToString("0.#", CultureInfo.InvariantCulture);
            return f.Whole ? v.ToString("0", CultureInfo.InvariantCulture) : v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool TryParse(string text, out double v)
            => double.TryParse((text ?? "").Trim().Replace('，', '.').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
}
