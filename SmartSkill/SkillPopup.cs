using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace SmartSkill
{
    // 技能图标上方的原生风格设置弹窗
    internal static class SkillPopup
    {
        private const float Width = 600f;
        private const float LabelWidth = 230f;

        private static readonly string[] ModeLabels = { "原版", "自定义条件", "永不自动" };
        private static readonly string[] CombineLabels = { "全部满足", "任一满足" };
        private static readonly string[] TargetLabels = { "最近", "Boss优先", "血量最低", "血量最高" };

        private static RectTransform root;
        private static RectTransform anchor;
        private static SkillSlot slot;
        private static Text status;
        // 填充初始值时会触发控件回调，这期间忽略
        private static bool building;
        // 在控件回调里销毁控件自身会出问题，重建推迟到下一帧
        private static bool reopenPending;

        public static bool IsOpenFor(SkillSlot s) => root != null && slot == s;

        public static void Toggle(SkillSlot s, RectTransform icon)
        {
            if (IsOpenFor(s)) Close();
            else Open(s, icon);
        }

        public static void Close()
        {
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            reopenPending = false;
            slot = null;
            anchor = null;
            status = null;
            RuleStore.Flush();
        }

        public static void Tick()
        {
            if (root == null) return;
            if (reopenPending)
            {
                reopenPending = false;
                var s = slot;
                var icon = anchor;
                Close();
                if (s != null && icon != null) Open(s, icon);
                return;
            }
            // 战斗结束、技能被替换时图标会回收或换技能
            if (anchor == null || !anchor.gameObject.activeInHierarchy || slot == null || !Game.InBattle)
            {
                Close();
                return;
            }
            if (status != null)
            {
                string auto = slot.IsAutoCastOn ? "" : "（右键技能图标开启自动释放后生效）";
                status.text = "上次：" + (StatusLog.Last(slot.Info) ?? "还没有触发过") + auto;
            }
        }

        private static void Open(SkillSlot s, RectTransform icon)
        {
            Close();
            if (!NativeUi.Load()) return;
            slot = s;
            anchor = icon;

            var canvas = icon.GetComponentInParent<Canvas>().rootCanvas;
            root = NativeUi.Panel(canvas.transform, "SmartSkillPopup");
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(Width, 0f);
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 18, 20);
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            building = true;
            try
            {
                Build();
            }
            finally
            {
                building = false;
            }
            Tick();
            Place(canvas);
        }

        private static void Build()
        {
            var player = Game.LocalPlayer;
            var info = slot.Info;
            var rule = RuleStore.Get(info);

            var header = NativeUi.Row(root, 44f);
            NativeUi.Label(header, $"<b>{info.Name}</b>  <size=18><color=#d6b67d>自动释放条件</color></size>", 28);
            var close = NativeUi.IconButton(header, NativeUi.CloseSprite, new Vector2(40f, 40f), Close);
            var closeLe = close.gameObject.AddComponent<LayoutElement>();
            closeLe.preferredWidth = closeLe.minWidth = 40f;

            string tags = string.Join("、", info.Tags.Select(SkillInfo.TagDisplayName));
            string cost = slot.CostsGold ? "花金币" : $"耗蓝 {slot.CostFor(player)}";
            NativeUi.Label(root, $"{cost}   冷却 {info.Cooldown:F1}s   施法范围 {info.CastRange:F1}   {(info.IsSelfCastable ? "无需瞄准" : "需要目标")}\n{tags}", 18, -1, NativeUi.DimText);
            status = NativeUi.Label(root, "", 18, -1, NativeUi.DimText);

            var modeRow = NativeUi.Row(root);
            NativeUi.Label(modeRow, "模式", 22, LabelWidth);
            var mode = NativeUi.Dropdown(modeRow, ModeLabels, 300f, v =>
            {
                if (building) return;
                rule.Mode = (RuleMode)v;
                RuleStore.MarkDirty();
                Reopen();
            });
            mode.value = (int)rule.Mode;

            if (rule.Mode == RuleMode.Custom) BuildConditions(rule, info);

            var buttons = NativeUi.Row(root, 56f, 16f);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            NativeUi.Button(buttons, "按标签推荐", true, 230f, () =>
            {
                RuleStore.Set(info, Recommend.For(info));
                Reopen();
            });
            NativeUi.Button(buttons, "恢复原版", false, 230f, () =>
            {
                RuleStore.Set(info, new SkillRule());
                Reopen();
            });
        }

        private static void BuildConditions(SkillRule rule, SkillInfo info)
        {
            var combineRow = NativeUi.Row(root);
            NativeUi.Label(combineRow, "条件关系", 22, LabelWidth);
            NativeUi.Dropdown(combineRow, CombineLabels, 300f, v => Set(() => rule.Combine = (Combine)v)).value = (int)rule.Combine;

            if (info.IsToggle)
                NativeUi.Label(root, "开关技能：条件满足时打开并保持，不再满足时自动关闭", 18, -1, NativeUi.Gold);

            PercentRow("自身血量 ≤", rule.UseHpBelow, v => rule.UseHpBelow = v, rule.HpBelowPercent, v => rule.HpBelowPercent = v);
            PercentRow("自身血量 ≥", rule.UseHpAbove, v => rule.UseHpAbove = v, rule.HpAbovePercent, v => rule.HpAbovePercent = v);
            PercentRow("释放后剩余蓝 ≥", rule.UseManaAfter, v => rule.UseManaAfter = v, rule.ManaAfterPercent, v => rule.ManaAfterPercent = v);

            CountRow("范围内敌人 ≥", rule.UseEnemyCount, v => rule.UseEnemyCount = v, rule.MinEnemies, v => rule.MinEnemies = v);

            var bossRow = NativeUi.Row(root);
            NativeUi.Toggle(bossRow, "有 Boss 或精英", LabelWidth, v => Set(() => rule.UseBossOrElite = v)).isOn = rule.UseBossOrElite;
            NativeUi.Toggle(bossRow, "没有 Boss", LabelWidth, v => Set(() => rule.UseNoBoss = v)).isOn = rule.UseNoBoss;

            PercentRow("有敌人血量 ≤", rule.UseTargetHpBelow, v => rule.UseTargetHpBelow = v, rule.TargetHpBelowPercent, v => rule.TargetHpBelowPercent = v);

            if (info.HasKillReward)
            {
                CountRow("能斩杀的敌人 ≥", rule.UseKillable, v => rule.UseKillable = v, rule.MinKillable, v => rule.MinKillable = v);
                var marginRow = NativeUi.Row(root);
                NativeUi.Label(marginRow, "　伤害按估算的", 22, LabelWidth);
                var marginValue = NativeUi.Label(marginRow, "", 22, 70f);
                var marginSlider = NativeUi.Slider(marginRow, 50f, 120f, false, v =>
                {
                    float stepped = Mathf.Round(v / 5f) * 5f;
                    marginValue.text = $"{stepped:F0}%";
                    Set(() => rule.KillMarginPercent = stepped);
                });
                marginValue.transform.SetAsLastSibling();
                marginSlider.value = rule.KillMarginPercent;
                marginValue.text = $"{rule.KillMarginPercent:F0}%";
                NativeUi.Label(root, "　" + KillPredictor.Describe(info), 16, -1, NativeUi.DimText);
            }

            if (!info.IsToggle)
                NativeUi.Toggle(root, "上一次的效果结束后再放（增益、变身、持续伤害）", Width, v => Set(() => rule.UseEffectEnded = v)).isOn = rule.UseEffectEnded;

            if (slot.MaxCharges > 1)
            {
                var chargeRow = NativeUi.Row(root);
                NativeUi.Label(chargeRow, "保留充能", 22, LabelWidth);
                var chargeValue = NativeUi.Label(chargeRow, "", 22, 70f);
                var chargeSlider = NativeUi.Slider(chargeRow, 0, slot.MaxCharges - 1, true, v =>
                {
                    chargeValue.text = $"{v:F0} 层";
                    Set(() => rule.KeepCharges = Mathf.RoundToInt(v));
                });
                chargeValue.transform.SetAsLastSibling();
                chargeSlider.value = rule.KeepCharges;
                chargeValue.text = $"{rule.KeepCharges} 层";
            }

            var radiusRow = NativeUi.Row(root);
            NativeUi.Label(radiusRow, "统计范围", 22, LabelWidth);
            var radiusValue = NativeUi.Label(radiusRow, "", 20, 130f);
            string RadiusText(float r) => r <= 0f ? $"技能范围 {info.CastRange:F1}" : $"{r:F1}";
            var radiusSlider = NativeUi.Slider(radiusRow, 0f, 20f, false, v =>
            {
                float r = Mathf.Round(v * 2f) / 2f;
                radiusValue.text = RadiusText(r);
                Set(() => rule.Radius = r);
            });
            radiusValue.transform.SetAsLastSibling();
            radiusSlider.value = rule.Radius;
            radiusValue.text = RadiusText(rule.Radius);

            if (info.IsSelfCastable)
            {
                NativeUi.Toggle(root, "附近没有敌人也放（治疗、护盾等）", Width, v => Set(() => rule.CastWithoutEnemy = v)).isOn = rule.CastWithoutEnemy;
            }
            else
            {
                var targetRow = NativeUi.Row(root);
                NativeUi.Label(targetRow, "目标", 22, LabelWidth);
                NativeUi.Dropdown(targetRow, TargetLabels, 300f, v => Set(() => rule.Target = (TargetMode)v)).value = (int)rule.Target;
            }

            NativeUi.Label(root, "不勾选任何条件 = 冷却好就放", 16, -1, NativeUi.DimText);
        }

        private static void PercentRow(string label, bool enabled, System.Action<bool> setEnabled, float value, System.Action<float> setValue)
        {
            var row = NativeUi.Row(root);
            NativeUi.Toggle(row, label, LabelWidth, v => Set(() => setEnabled(v))).isOn = enabled;
            var valueLabel = NativeUi.Label(row, "", 22, 70f);
            var slider = NativeUi.Slider(row, 0f, 100f, false, v =>
            {
                float stepped = Mathf.Round(v / 5f) * 5f;
                valueLabel.text = $"{stepped:F0}%";
                Set(() => setValue(stepped));
            });
            valueLabel.transform.SetAsLastSibling();
            slider.value = value;
            valueLabel.text = $"{value:F0}%";
        }

        private static void CountRow(string label, bool enabled, System.Action<bool> setEnabled, int value, System.Action<int> setValue)
        {
            var row = NativeUi.Row(root);
            NativeUi.Toggle(row, label, LabelWidth, v => Set(() => setEnabled(v))).isOn = enabled;
            var valueLabel = NativeUi.Label(row, "", 22, 70f);
            var slider = NativeUi.Slider(row, 1, 10, true, v =>
            {
                valueLabel.text = $"{v:F0} 个";
                Set(() => setValue(Mathf.RoundToInt(v)));
            });
            valueLabel.transform.SetAsLastSibling();
            slider.value = value;
            valueLabel.text = $"{value} 个";
        }

        private static void Set(System.Action apply)
        {
            if (building) return;
            apply();
            RuleStore.MarkDirty();
        }

        // 切换模式等会改变弹窗内容，下一帧整体重建
        private static void Reopen() => reopenPending = true;

        // 放在技能图标正上方（避开按键提示），并限制在屏幕内
        private static void Place(Canvas canvas)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            var canvasRt = (RectTransform)canvas.transform;
            Vector3 iconTop = anchor.TransformPoint(new Vector3(0f, anchor.rect.yMax + 110f, 0f));
            Vector2 local = canvasRt.InverseTransformPoint(iconTop);

            Vector2 half = canvasRt.rect.size / 2f;
            float w = root.rect.width, h = root.rect.height;
            local.x = Mathf.Clamp(local.x, -half.x + w / 2f + 10f, half.x - w / 2f - 10f);
            local.y = Mathf.Min(local.y, half.y - h - 10f);
            root.localPosition = local;
            root.SetAsLastSibling();
        }
    }

    // 开启自动释放的技能图标右上角显示齿轮，点击打开 SkillPopup
    internal static class SkillGear
    {
        private const string GearName = "SmartSkillGear";

        public static void Sync()
        {
            foreach (var button in SkillBar.Buttons())
            {
                var gear = button.Icon.Find(GearName);
                bool show = ShouldShow(button.Slot);
                if (gear == null)
                {
                    if (!show || !NativeUi.Load()) continue;
                    gear = Create(button).transform;
                }
                if (gear.gameObject.activeSelf != show) gear.gameObject.SetActive(show);
            }
        }

        public static void RemoveAll()
        {
            foreach (var button in SkillBar.Buttons())
            {
                var gear = button.Icon.Find(GearName);
                if (gear != null) Object.Destroy(gear.gameObject);
            }
        }

        private static bool ShouldShow(SkillSlot slot)
        {
            if (slot?.Info == null) return false;
            return slot.Info.CanAutoCast && (slot.IsAutoCastOn || SkillPopup.IsOpenFor(slot));
        }

        // 同一个按钮上的技能可能被替换，点击时再取当前技能
        private static GameObject Create(SkillButton button)
        {
            var btn = NativeUi.IconButton(button.Icon, NativeUi.GearSprite, new Vector2(90f, 90f), () =>
            {
                if (button.Slot != null) SkillPopup.Toggle(button.Slot, button.Icon);
            });
            btn.name = GearName;
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            btn.transform.SetAsLastSibling();
            return btn.gameObject;
        }
    }
}
