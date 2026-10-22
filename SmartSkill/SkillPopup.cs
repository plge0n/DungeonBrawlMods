using System.Linq;
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
        private static bht slot;
        private static Text status;
        // 填充初始值时会触发控件回调，这期间忽略
        private static bool building;
        // 在控件回调里销毁控件自身会出问题，重建推迟到下一帧
        private static bool reopenPending;

        public static bool IsOpenFor(bht s) => root != null && slot == s;

        public static void Toggle(bht s, RectTransform icon)
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
            if (anchor == null || !anchor.gameObject.activeInHierarchy || slot == null || GameApi.LocalPlayer == null)
            {
                Close();
                return;
            }
            if (status != null)
            {
                AutoCastGatePatch.LastStatus.TryGetValue(GameApi.SkillOf(slot), out var last);
                string auto = GameApi.IsAutoCastOn(slot) ? "" : "（右键技能图标开启自动释放后生效）";
                status.text = "上次：" + (last ?? "还没有触发过") + auto;
            }
        }

        private static void Open(bht s, RectTransform icon)
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
            var player = GameApi.LocalPlayer;
            var skill = GameApi.SkillOf(slot);
            var data = GameApi.SkillData(skill);
            var rule = RuleStore.Get(skill);
            bool selfCastable = GameApi.IsSelfCastable(data);

            var header = NativeUi.Row(root, 44f);
            NativeUi.Label(header, $"<b>{GameApi.SkillName(skill)}</b>  <size=18><color=#d6b67d>自动释放条件</color></size>", 28);
            var close = NativeUi.IconButton(header, NativeUi.CloseSprite, new Vector2(40f, 40f), Close);
            var closeLe = close.gameObject.AddComponent<LayoutElement>();
            closeLe.preferredWidth = closeLe.minWidth = 40f;

            string tags = data.tags == null ? "" : string.Join("、", data.tags.Select(GameApi.TagName));
            string cost = GameApi.CostsGold(slot) ? "花金币" : $"耗蓝 {GameApi.Cost(slot, player)}";
            NativeUi.Label(root, $"{cost}   冷却 {data.cd:F1}s   施法范围 {GameApi.CastRange(data):F1}   {(selfCastable ? "无需瞄准" : "需要目标")}\n{tags}", 18, -1, NativeUi.DimText);
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

            if (rule.Mode == RuleMode.Custom) BuildConditions(rule, data, selfCastable);

            var buttons = NativeUi.Row(root, 56f, 16f);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            NativeUi.Button(buttons, "按标签推荐", true, 230f, () =>
            {
                RuleStore.Set(skill, Recommend.For(skill));
                RuleStore.MarkDirty();
                Reopen();
            });
            NativeUi.Button(buttons, "恢复原版", false, 230f, () =>
            {
                RuleStore.Set(skill, new SkillRule());
                RuleStore.MarkDirty();
                Reopen();
            });
        }

        private static void BuildConditions(SkillRule rule, ActiveSkillData data, bool selfCastable)
        {
            var combineRow = NativeUi.Row(root);
            NativeUi.Label(combineRow, "条件关系", 22, LabelWidth);
            NativeUi.Dropdown(combineRow, CombineLabels, 300f, v => Set(() => rule.Combine = (Combine)v)).value = (int)rule.Combine;

            PercentRow("自身血量 ≤", rule.UseHpBelow, v => rule.UseHpBelow = v, rule.HpBelowPercent, v => rule.HpBelowPercent = v);
            PercentRow("释放后剩余蓝 ≥", rule.UseManaAfter, v => rule.UseManaAfter = v, rule.ManaAfterPercent, v => rule.ManaAfterPercent = v);

            var enemyRow = NativeUi.Row(root);
            NativeUi.Toggle(enemyRow, "范围内敌人 ≥", LabelWidth, v => Set(() => rule.UseEnemyCount = v)).isOn = rule.UseEnemyCount;
            var enemyValue = NativeUi.Label(enemyRow, "", 22, 70f);
            var enemySlider = NativeUi.Slider(enemyRow, 1, 10, true, v =>
            {
                enemyValue.text = $"{v:F0} 个";
                Set(() => rule.MinEnemies = Mathf.RoundToInt(v));
            });
            enemyValue.transform.SetAsLastSibling();
            enemySlider.value = rule.MinEnemies;
            enemyValue.text = $"{rule.MinEnemies} 个";

            NativeUi.Toggle(root, "范围内有 Boss 或精英", Width, v => Set(() => rule.UseBossOrElite = v)).isOn = rule.UseBossOrElite;

            var radiusRow = NativeUi.Row(root);
            NativeUi.Label(radiusRow, "统计范围", 22, LabelWidth);
            var radiusValue = NativeUi.Label(radiusRow, "", 20, 130f);
            string RadiusText(float r) => r <= 0f ? $"技能范围 {GameApi.CastRange(data):F1}" : $"{r:F1}";
            var radiusSlider = NativeUi.Slider(radiusRow, 0f, 20f, false, v =>
            {
                float r = Mathf.Round(v * 2f) / 2f;
                radiusValue.text = RadiusText(r);
                Set(() => rule.Radius = r);
            });
            radiusValue.transform.SetAsLastSibling();
            radiusSlider.value = rule.Radius;
            radiusValue.text = RadiusText(rule.Radius);

            if (selfCastable)
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
            var bar = cj.boyb?.dqr<bpe>();
            if (bar?.bjes == null) return;
            foreach (var button in bar.bjes)
            {
                if (button == null || button.bjgm == null) continue;
                var icon = (RectTransform)button.bjgm.transform;
                var gear = icon.Find(GearName);
                bool show = ShouldShow(button);
                if (gear == null)
                {
                    if (!show || !NativeUi.Load()) continue;
                    gear = Create(icon, button).transform;
                }
                if (gear.gameObject.activeSelf != show) gear.gameObject.SetActive(show);
            }
        }

        private static bool ShouldShow(bpf button)
        {
            var slot = button.bjgk;
            if (slot == null || GameApi.SkillOf(slot) == ActiveSkillEnum.None) return false;
            return GameApi.SkillData(GameApi.SkillOf(slot)).canAuto && (GameApi.IsAutoCastOn(slot) || SkillPopup.IsOpenFor(slot));
        }

        private static GameObject Create(RectTransform icon, bpf button)
        {
            var btn = NativeUi.IconButton(icon, NativeUi.GearSprite, new Vector2(90f, 90f), () =>
            {
                if (button.bjgk != null) SkillPopup.Toggle(button.bjgk, icon);
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
