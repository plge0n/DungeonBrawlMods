using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio.Pages
{
    // 规则：当……时，如果……，就……。左边是规则列表，右边按句子编辑
    internal sealed class RulesPage : IStudioPage
    {
        private const float DeleteConfirmSeconds = 3f;

        private static Rule selected;

        private RectTransform listContent, editorContent;
        private Text state, summary;
        private Button deleteButton;
        private float deleteArmedUntil;
        // 增删条件、换类型等会改变编辑区的结构：在控件回调里销毁控件自己会出问题，下一帧再重建
        private bool rebuildList, rebuildEditor;
        private readonly Dictionary<Rule, Text> listLabels = new Dictionary<Rule, Text>();

        public string Title => "规则";

        private static List<Rule> Rules => Packs.Current.Rules;

        public void Build(RectTransform body)
        {
            deleteArmedUntil = 0f;
            if (selected != null && !Rules.Contains(selected)) selected = null;
            if (selected == null) selected = Rules.FirstOrDefault();
            var columns = Studio.Columns(body);

            var left = NativeUi.Column(columns, 520f);
            var toggleRow = NativeUi.Row(left, 44f);
            NativeUi.Toggle(toggleRow, "启用玩法包", 200f, Packs.SetEnabled).SetIsOnWithoutNotify(Packs.Current.Enabled);
            state = NativeUi.Label(toggleRow, "", 18, -1f, NativeUi.DimText);
            var templates = new[] { "＋ 新建规则（选一个模板）" }.Concat(RuleTemplates.All.Select(t => t.label)).ToList();
            Dropdown create = null;
            create = NativeUi.Dropdown(left, templates, -1f, i =>
            {
                if (i <= 0) return;
                // 选完回到提示文字，下次还能选同一个模板
                create.SetValueWithoutNotify(0);
                var rule = RuleTemplates.All[i - 1].make();
                Rules.Add(rule);
                selected = rule;
                Packs.MarkDirty();
                EventLog.Add($"新建规则「{rule.Name}」");
                RequestRebuild(list: true);
            });
            create.GetComponent<LayoutElement>().flexibleWidth = 1f;
            listContent = NativeUi.ScrollView(left, out _, 4f);

            var right = NativeUi.Column(columns);
            summary = Studio.Block(right, 22);
            summary.color = NativeUi.Gold;
            NativeUi.Separator(right);
            editorContent = NativeUi.ScrollView(right, out _, 8f, boxed: false);
            NativeUi.Separator(right);
            var bottom = NativeUi.Row(right, 56f, 16f);
            NativeUi.Label(bottom, "规则只在单人时生效；冷却和持续时间按游戏时间算，暂停时不走", 16, -1f, NativeUi.DimText);
            NativeUi.Button(bottom, "复制", false, 150f, Duplicate);
            deleteButton = NativeUi.Button(bottom, "删除规则", false, 170f, Delete);

            BuildList();
            BuildEditor();
        }

        public void Refresh()
        {
            if (rebuildList)
            {
                rebuildList = false;
                BuildList();
            }
            if (rebuildEditor)
            {
                rebuildEditor = false;
                BuildEditor();
            }
            string reason = Packs.InactiveReason;
            state.text = reason == null ? "<color=#9fd08a>生效中（单人）</color>" : reason;
            if (deleteArmedUntil > 0f && Time.unscaledTime > deleteArmedUntil) DisarmDelete();
        }

        private void RequestRebuild(bool list)
        {
            rebuildEditor = true;
            rebuildList |= list;
            Studio.RefreshSoon();
        }

        // ---- 左边：规则列表 ----

        private void BuildList()
        {
            Widgets.Clear(listContent);
            listLabels.Clear();
            if (Rules.Count == 0)
            {
                Studio.Block(listContent).text = "还没有规则。\n点上面的「新建规则」，选一个模板开始。";
                return;
            }
            foreach (var rule in Rules) AddListItem(rule);
        }

        private void AddListItem(Rule rule)
        {
            var go = new GameObject("rule", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(listContent, false);
            go.GetComponent<LayoutElement>().minHeight = 64f;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 6, 6);
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var bg = go.GetComponent<Image>();
            bg = NativeUi.ListItemBackground(bg);
            bg.color = rule == selected ? NativeUi.BoxColor : new Color(0f, 0f, 0f, 0f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (selected == rule) return;
                selected = rule;
                DisarmDelete();
                RequestRebuild(list: true);
            });

            var toggle = NativeUi.Toggle(go.transform, "", 40f, v =>
            {
                rule.Enabled = v;
                Changed(rule);
            });
            toggle.SetIsOnWithoutNotify(rule.Enabled);
            var label = NativeUi.Label(go.transform, "", 20);
            listLabels[rule] = label;
            UpdateListLabel(rule);
        }

        private void UpdateListLabel(Rule rule)
        {
            if (!listLabels.TryGetValue(rule, out var label)) return;
            string name = rule.Enabled ? rule.Name : $"<color=#a6a6a6>{rule.Name}（已停用）</color>";
            var owner = AugmentEngine.OwnerOf(rule);
            if (owner != null) name += $"  <size=16><color=#d6b67d>强化「{owner.Name}」专属</color></size>";
            label.text = $"{name}\n<size=16><color=#a6a6a6>{RuleText.Sentence(rule)}</color></size>";
        }

        // ---- 右边：编辑选中的规则 ----

        private void BuildEditor()
        {
            Widgets.Clear(editorContent);
            NativeUi.SetEnabled(deleteButton, selected != null);
            if (selected == null)
            {
                summary.text = "";
                Studio.Block(editorContent).text = "在左边选一条规则，或者新建一条。";
                return;
            }
            var rule = selected;
            UpdateSummary();

            var nameRow = NativeUi.Row(editorContent, 44f, 12f);
            NativeUi.Label(nameRow, "名字", 22, 90f);
            Widgets.Text(nameRow, rule.Name, "给规则起个名字", -1f, v =>
            {
                rule.Name = v;
                Changed(rule);
            }, "新规则");

            NativeUi.Heading(editorContent, "当");
            var whenRow = NativeUi.Row(editorContent, 44f, 12f);
            Widgets.Choice(whenRow, RuleText.Triggers.Select(t => t.label), Array.FindIndex(RuleText.Triggers, t => t.value == rule.Trigger), 360f, i =>
            {
                rule.Trigger = RuleText.Triggers[i].value;
                RuleEngine.Forget(rule);
                Changed(rule, structure: true);
            });
            if (rule.Trigger == Trigger.Interval)
            {
                NativeUi.Label(whenRow, "每隔", 22, 60f);
                Widgets.Number(whenRow, rule.Every, 110f, v => rule.Every = Math.Max(0.2, v), () => Changed(rule));
                NativeUi.Label(whenRow, "秒", 22, 40f);
            }

            NativeUi.Heading(editorContent, "如果（全部满足）");
            if (rule.Conditions.Count == 0)
                NativeUi.Label(editorContent, "没有条件：每次都会触发", 18, -1f, NativeUi.DimText);
            foreach (var c in rule.Conditions.ToList()) ConditionRow(rule, c);
            AddButton("＋ 添加条件", () => rule.Conditions.Add(new Condition()), rule);

            NativeUi.Heading(editorContent, "就");
            if (rule.Actions.Count == 0)
                NativeUi.Label(editorContent, "还没有动作：触发了也什么都不做", 18, -1f, NativeUi.DimText);
            foreach (var a in rule.Actions.ToList())
                Widgets.ActionRow(editorContent, a, () => Changed(rule), () => Changed(rule, structure: true), () =>
                {
                    rule.Actions.Remove(a);
                    Changed(rule, structure: true);
                });
            AddButton("＋ 添加动作", () => rule.Actions.Add(new RuleAction()), rule);

            NativeUi.Heading(editorContent, "限制");
            var limitRow = NativeUi.Row(editorContent, 44f, 12f);
            NativeUi.Label(limitRow, "冷却", 22, 60f);
            Widgets.Number(limitRow, rule.Cooldown, 110f, v => rule.Cooldown = v, () => Changed(rule));
            NativeUi.Label(limitRow, "秒", 22, 50f);
            NativeUi.Label(limitRow, "每局最多", 22, 100f);
            Widgets.Number(limitRow, rule.MaxPerBattle, 110f, v => rule.MaxPerBattle = (int)Math.Round(v), () => Changed(rule), whole: true);
            NativeUi.Label(limitRow, "次（0 = 不限）", 22, -1f);
        }

        private void ConditionRow(Rule rule, Condition c)
        {
            var row = NativeUi.Row(editorContent, 44f, 12f);
            var (_, unit) = RuleText.Label(c.Type);
            Widgets.Choice(row, RuleText.Conditions.Select(x => x.label), Array.FindIndex(RuleText.Conditions, x => x.value == c.Type), 300f, i =>
            {
                c.Type = RuleText.Conditions[i].value;
                Changed(rule, structure: true);
            });
            Widgets.Number(row, c.Value, 110f, v => c.Value = v, () => Changed(rule));
            NativeUi.Label(row, unit, 22, 40f);
            Widgets.Spacer(row);
            NativeUi.Button(row, "删除", false, 100f, () =>
            {
                rule.Conditions.Remove(c);
                Changed(rule, structure: true);
            });
        }

        private void AddButton(string text, Action add, Rule rule)
        {
            var row = NativeUi.Row(editorContent, 52f);
            NativeUi.Button(row, text, false, 220f, () =>
            {
                add();
                Changed(rule, structure: true);
            });
        }

        // ---- 修改 ----

        private void Changed(Rule rule, bool structure = false)
        {
            Packs.MarkDirty();
            UpdateListLabel(rule);
            if (rule == selected) UpdateSummary();
            if (structure) RequestRebuild(list: false);
        }

        private void UpdateSummary()
        {
            if (selected == null)
            {
                summary.text = "";
                return;
            }
            var owner = AugmentEngine.OwnerOf(selected);
            summary.text = RuleText.Sentence(selected) + "。" + (owner == null ? ""
                : $"\n<size=18><color=#a6a6a6>这条规则属于强化「{owner.Name}」，选到这个强化之后才生效。</color></size>");
        }

        private void Duplicate()
        {
            if (selected == null) return;
            var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<Rule>(Newtonsoft.Json.JsonConvert.SerializeObject(selected));
            copy.Name = selected.Name + " 副本";
            copy.Id = Rule.NewId();
            Rules.Insert(Rules.IndexOf(selected) + 1, copy);
            selected = copy;
            Packs.MarkDirty();
            RequestRebuild(list: true);
        }

        // 点一次变成"再点一次确认"，3 秒内再点才删除
        private void Delete()
        {
            if (selected == null) return;
            if (deleteArmedUntil <= 0f)
            {
                deleteArmedUntil = Time.unscaledTime + DeleteConfirmSeconds;
                deleteButton.GetComponentInChildren<Text>().text = "再点一次确认";
                return;
            }
            DisarmDelete();
            int index = Rules.IndexOf(selected);
            EventLog.Add($"删除规则「{selected.Name}」");
            RuleEngine.Forget(selected);
            Rules.Remove(selected);
            selected = Rules.Count == 0 ? null : Rules[Mathf.Clamp(index, 0, Rules.Count - 1)];
            Packs.MarkDirty();
            RequestRebuild(list: true);
        }

        private void DisarmDelete()
        {
            deleteArmedUntil = 0f;
            if (deleteButton != null) deleteButton.GetComponentInChildren<Text>().text = "删除规则";
        }
    }
}
