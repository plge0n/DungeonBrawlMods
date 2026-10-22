using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using DBModKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBStudio
{
    // 把一个插件的 BepInEx 配置（.cfg）按类型做成游戏风格的控件；改了立刻写回配置文件
    internal static class ConfigEditor
    {
        private const float LabelWidth = 360f;

        public static void Build(Transform parent, ConfigFile config)
        {
            var entries = config.Select(kv => kv.Value).Where(e => e != null)
                .OrderBy(e => e.Definition.Section).ThenBy(e => e.Definition.Key).ToList();
            if (entries.Count == 0)
            {
                NativeUi.Label(parent, "这个插件没有配置项", 18, -1f, NativeUi.DimText);
                return;
            }
            foreach (var group in entries.GroupBy(e => e.Definition.Section))
            {
                if (entries.Select(e => e.Definition.Section).Distinct().Count() > 1)
                    NativeUi.Label(parent, $"[{group.Key}]", 18, -1f, NativeUi.Gold);
                foreach (var entry in group) Row(parent, entry);
            }
        }

        private static void Row(Transform parent, ConfigEntryBase entry)
        {
            var row = NativeUi.Row(parent, 48f, 12f);
            // 说明可能有好几行：高度跟着左边的名字和说明走，至少 48；控件不拉伸、竖直居中
            var rowLe = row.GetComponent<LayoutElement>();
            rowLe.preferredHeight = -1f;
            rowLe.minHeight = 48f;
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 4, 4);
            var labels = NativeUi.Column(row, LabelWidth, 0f);
            NativeUi.Label(labels, entry.Definition.Key, 22);
            string desc = entry.Description?.Description;
            if (!string.IsNullOrEmpty(desc)) NativeUi.Label(labels, desc, 16, -1f, NativeUi.DimText);

            var type = entry.SettingType;
            var acceptable = entry.Description?.AcceptableValues;
            if (type.IsEnum && type.IsDefined(typeof(FlagsAttribute), false))
            {
                // 组合值没法用下拉框表示，只显示
                NativeUi.Label(row, entry.GetSerializedValue() + "  <size=16><color=#a6a6a6>（在 .cfg 文件里修改）</color></size>", 20);
            }
            else if (type == typeof(bool))
            {
                NativeUi.Toggle(row, "", 40f, v => Set(entry, v)).SetIsOnWithoutNotify((bool)entry.BoxedValue);
                Widgets.Spacer(row);
            }
            // 有限定选项时只列允许的值（枚举也是），否则 BepInEx 会把列表外的值悄悄改掉
            else if (acceptable != null && IsList(acceptable, out var options))
            {
                var labelsList = options.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture)).ToList();
                Widgets.Choice(row, labelsList, options.FindIndex(o => Equals(o, entry.BoxedValue)), 300f, i => Set(entry, options[i]));
                Widgets.Spacer(row);
            }
            else if (type.IsEnum)
            {
                var names = Enum.GetNames(type);
                Widgets.Choice(row, names, Array.IndexOf(names, entry.BoxedValue.ToString()), 300f,
                    i => Set(entry, Enum.Parse(type, names[i])));
                Widgets.Spacer(row);
            }
            else if (IsNumber(type) && acceptable != null && IsRange(acceptable, out double min, out double max))
            {
                bool whole = type != typeof(float) && type != typeof(double);
                var value = NativeUi.Label(row, "", 22, 90f);
                // 拖动时只更新数字，松手再写入：ConfigFile 改值就存盘，还会触发插件的 SettingChanged
                var slider = NativeUi.Slider(row, (float)min, (float)max, whole, v => value.text = Format(v, whole));
                var trigger = slider.gameObject.AddComponent<EventTrigger>();
                var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                up.callback.AddListener(_ => Set(entry, Convert.ChangeType(slider.value, type, CultureInfo.InvariantCulture)));
                trigger.triggers.Add(up);
                value.transform.SetAsLastSibling();
                double current = Convert.ToDouble(entry.BoxedValue, CultureInfo.InvariantCulture);
                slider.SetValueWithoutNotify((float)current);
                value.text = Format(current, whole);
            }
            else if (IsNumber(type))
            {
                bool whole = type != typeof(float) && type != typeof(double);
                InputField field = null;
                field = NativeUi.Input(row, "", 200f, text =>
                {
                    if (Widgets.TryParse(text, out double v))
                    {
                        try
                        {
                            Set(entry, Convert.ChangeType(whole ? Math.Round(v) : v, type, CultureInfo.InvariantCulture));
                        }
                        catch (OverflowException) { }
                    }
                    field.SetTextWithoutNotify(Convert.ToString(entry.BoxedValue, CultureInfo.InvariantCulture));
                }, whole ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber);
                field.SetTextWithoutNotify(Convert.ToString(entry.BoxedValue, CultureInfo.InvariantCulture));
                Widgets.Spacer(row);
            }
            else if (type == typeof(string))
            {
                Widgets.Text(row, (string)entry.BoxedValue, "", -1f, v => Set(entry, v));
            }
            else if (type == typeof(KeyboardShortcut))
            {
                Button button = null;
                button = NativeUi.Button(row, KeyText((KeyboardShortcut)entry.BoxedValue), false, 260f, () =>
                {
                    var label = button.GetComponentInChildren<Text>();
                    label.text = "按下新按键…";
                    KeyCapture.Start(shortcut =>
                    {
                        // 工作台自己的快捷键清空了就再也打不开工作台
                        bool own = ReferenceEquals(entry, Plugin.Hotkey);
                        if (shortcut is KeyboardShortcut k && !(own && k.MainKey == KeyCode.None)) Set(entry, k);
                        if (label != null) label.text = KeyText((KeyboardShortcut)entry.BoxedValue);
                    });
                });
                NativeUi.Label(row, ReferenceEquals(entry, Plugin.Hotkey) ? "ESC 取消" : "ESC 取消，Backspace 清除", 16, -1f, NativeUi.DimText);
            }
            else
            {
                // 其他类型（颜色、向量等）只显示，到 .cfg 文件里改
                NativeUi.Label(row, entry.GetSerializedValue() + "  <size=16><color=#a6a6a6>（在 .cfg 文件里修改）</color></size>", 20);
            }
        }

        private static void Set(ConfigEntryBase entry, object value)
        {
            try
            {
                // ConfigFile 默认改值就保存（SaveOnConfigSet），插件订阅的 SettingChanged 也会触发
                entry.BoxedValue = value;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"设置 {entry.Definition} 失败：{e.Message}");
            }
        }

        private static bool IsNumber(Type t)
            => t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(short) || t == typeof(byte);

        // AcceptableValueRange<T> / AcceptableValueList<T> 是泛型，用反射取值
        private static bool IsRange(AcceptableValueBase a, out double min, out double max)
        {
            min = max = 0;
            var t = a.GetType();
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(AcceptableValueRange<>)) return false;
            min = Convert.ToDouble(t.GetProperty("MinValue").GetValue(a), CultureInfo.InvariantCulture);
            max = Convert.ToDouble(t.GetProperty("MaxValue").GetValue(a), CultureInfo.InvariantCulture);
            return max > min;
        }

        private static bool IsList(AcceptableValueBase a, out List<object> options)
        {
            options = null;
            var t = a.GetType();
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(AcceptableValueList<>)) return false;
            options = ((Array)t.GetProperty("AcceptableValues").GetValue(a)).Cast<object>().ToList();
            return options.Count > 0;
        }

        private static string Format(double v, bool whole)
            => whole ? v.ToString("0", CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);

        public static string KeyText(KeyboardShortcut k)
            => k.MainKey == KeyCode.None ? "（未设置）" : k.ToString();
    }

    // 录制快捷键：工作台每帧先交给它，录制期间不响应其他快捷键
    internal static class KeyCapture
    {
        private static Action<KeyboardShortcut?> done;
        private static readonly KeyCode[] Modifiers =
        {
            KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftAlt, KeyCode.RightAlt,
        };
        private static readonly KeyCode[] Candidates = ((KeyCode[])Enum.GetValues(typeof(KeyCode)))
            .Where(k => k != KeyCode.None && k < KeyCode.Mouse0 && !Modifiers.Contains(k)).Distinct().ToArray();

        public static bool Active => done != null;

        // 上一次没录完的先取消（让它的按钮恢复文字）；取消选中，免得 Enter/空格 又触发按钮再录一次
        public static void Start(Action<KeyboardShortcut?> onDone)
        {
            Cancel();
            done = onDone;
            Deselect();
        }

        private static void Deselect()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public static void Cancel() => Finish(null);

        public static void Tick()
        {
            if (!Active || !Input.anyKeyDown) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Finish(null);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
            {
                Finish(KeyboardShortcut.Empty);
                return;
            }
            foreach (var key in Candidates)
            {
                if (!Input.GetKeyDown(key)) continue;
                var held = Modifiers.Where(Input.GetKey).ToArray();
                Finish(new KeyboardShortcut(key, held));
                return;
            }
        }

        private static void Finish(KeyboardShortcut? result)
        {
            var d = done;
            done = null;
            Deselect();
            d?.Invoke(result);
        }
    }
}
