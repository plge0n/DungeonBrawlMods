using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio.Pages
{
    // 强化：做自己的"海克斯"，波次结束时用游戏的三选一界面让玩家选
    internal sealed class AugmentsPage : IStudioPage
    {
        private const float DeleteConfirmSeconds = 3f;

        private static Augment selected;

        private RectTransform listContent, editorContent;
        private Text state, previewName, previewDesc;
        private Image previewIcon;
        private Button deleteButton, testButton;
        private float deleteArmedUntil;
        // 在控件回调里销毁控件自己会出问题，下一帧再重建
        private bool rebuildList, rebuildEditor;
        private readonly Dictionary<Augment, Text> listLabels = new Dictionary<Augment, Text>();
        private readonly Dictionary<Augment, Image> listIcons = new Dictionary<Augment, Image>();

        public string Title => "强化";

        private static List<Augment> Augments => Packs.Current.Augments;
        private static AugmentOffer Offer => Packs.Current.Offer;

        public void Build(RectTransform body)
        {
            deleteArmedUntil = 0f;
            if (selected != null && !Augments.Contains(selected)) selected = null;
            if (selected == null) selected = Augments.FirstOrDefault();
            var columns = Studio.Columns(body);

            var left = NativeUi.Column(columns, 520f);
            var toggleRow = NativeUi.Row(left, 44f);
            NativeUi.Toggle(toggleRow, "启用玩法包", 200f, Packs.SetEnabled).SetIsOnWithoutNotify(Packs.Current.Enabled);
            state = NativeUi.Label(toggleRow, "", 18, -1f, NativeUi.DimText);

            NativeUi.Toggle(left, "波次结束时弹出强化选择", 500f, v =>
            {
                Offer.Enabled = v;
                Packs.MarkDirty();
            }).SetIsOnWithoutNotify(Offer.Enabled);
            var offerRow = NativeUi.Row(left, 44f, 10f);
            NativeUi.Label(offerRow, "每", 22, 30f);
            Widgets.Number(offerRow, Offer.EveryWaves, 80f, v => Offer.EveryWaves = Mathf.Max(1, (int)v), Packs.MarkDirty, whole: true);
            NativeUi.Label(offerRow, "波一次，给", 22, 120f);
            Widgets.Number(offerRow, Offer.Choices, 80f, v => Offer.Choices = Mathf.Clamp((int)v, 1, ChoicePanel.MaxOptions), Packs.MarkDirty, whole: true);
            NativeUi.Label(offerRow, $"个选项（最多 {ChoicePanel.MaxOptions}）", 22, -1f);
            var testRow = NativeUi.Row(left, 52f, 10f);
            NativeUi.Toggle(testRow, "同一局不重复", 200f, v =>
            {
                Offer.NoRepeat = v;
                Packs.MarkDirty();
            }).SetIsOnWithoutNotify(Offer.NoRepeat);
            testButton = NativeUi.Button(testRow, "现在弹一次", false, 190f, () =>
            {
                AugmentEngine.OfferSoon();
                Studio.Close(); // 三选一界面在游戏 UI 上，工作台会挡住它
            });
            NativeUi.Separator(left);

            var templates = new[] { "＋ 新建强化（选一个模板）" }.Concat(AugmentTemplates.All.Select(t => t.label)).ToList();
            Dropdown create = null;
            create = NativeUi.Dropdown(left, templates, -1f, i =>
            {
                create.SetValueWithoutNotify(0);
                if (i <= 0) return;
                var (augment, rules) = AugmentTemplates.All[i - 1].make();
                Packs.Current.Rules.AddRange(rules);
                Augments.Add(augment);
                selected = augment;
                Packs.MarkDirty();
                EventLog.Add($"新建强化「{augment.Name}」" + (rules.Length > 0 ? "（以及它专属的规则，可以在规则页修改）" : ""));
                RequestRebuild(list: true);
            });
            create.GetComponent<LayoutElement>().flexibleWidth = 1f;
            listContent = NativeUi.ScrollView(left, out _, 4f);

            var right = NativeUi.Column(columns);
            // 预览：和三选一卡片上一样的图标、名字、描述
            var preview = NativeUi.Row(right, 110f, 16f);
            previewIcon = Widgets.Icon(preview, null, 96f);
            var previewText = NativeUi.Column(preview, -1f, 4f);
            previewName = NativeUi.Label(previewText, "", 28);
            previewName.fontStyle = FontStyle.Bold;
            previewDesc = Studio.Block(previewText, 18);
            previewDesc.color = NativeUi.DimText;
            NativeUi.Separator(right);
            editorContent = NativeUi.ScrollView(right, out _, 8f, boxed: false);
            NativeUi.Separator(right);
            var bottom = NativeUi.Row(right, 56f, 16f);
            NativeUi.Label(bottom, "选到的强化本局有效；只在单人时生效", 16, -1f, NativeUi.DimText);
            NativeUi.Button(bottom, "复制", false, 150f, Duplicate);
            deleteButton = NativeUi.Button(bottom, "删除强化", false, 170f, Delete);

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
            NativeUi.SetEnabled(testButton, Packs.IsActive && Game.InBattle && Augments.Any(a => a.Enabled));
            if (deleteArmedUntil > 0f && Time.unscaledTime > deleteArmedUntil) DisarmDelete();
        }

        private void RequestRebuild(bool list)
        {
            rebuildEditor = true;
            rebuildList |= list;
            Studio.RefreshSoon();
        }

        // ---- 左边：强化列表 ----

        private void BuildList()
        {
            Widgets.Clear(listContent);
            listLabels.Clear();
            listIcons.Clear();
            if (Augments.Count == 0)
            {
                Studio.Block(listContent).text = "还没有强化。\n点上面的「新建强化」，选一个模板开始。";
                return;
            }
            foreach (var a in Augments) AddListItem(a);
        }

        private void AddListItem(Augment augment)
        {
            var go = new GameObject("augment", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(listContent, false);
            go.GetComponent<LayoutElement>().minHeight = 60f;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 6, 6);
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            var bg = go.GetComponent<Image>();
            bg.sprite = NativeUi.BoxSprite;
            bg.type = Image.Type.Sliced;
            bg.color = augment == selected ? NativeUi.BoxColor : new Color(0f, 0f, 0f, 0f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (selected == augment) return;
                selected = augment;
                DisarmDelete();
                RequestRebuild(list: true);
            });

            NativeUi.Toggle(go.transform, "", 40f, v =>
            {
                augment.Enabled = v;
                Changed(augment);
            }).SetIsOnWithoutNotify(augment.Enabled);
            listIcons[augment] = Widgets.Icon(go.transform, LoadIcon(augment.Icon), 44f);
            listLabels[augment] = NativeUi.Label(go.transform, "", 22);
            UpdateListItem(augment);
        }

        private void UpdateListItem(Augment a)
        {
            if (listLabels.TryGetValue(a, out var label))
            {
                int q = AugmentText.QualityFromIndex(AugmentText.QualityIndex(a.Quality)); // 存档里写了越界的值时当成不上色
                string quality = q >= 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(QualityColor(q))}>[{AugmentText.QualityLabels[q]}]</color> " : "";
                label.text = quality + (a.Enabled ? a.Name : $"<color=#a6a6a6>{a.Name}（已停用）</color>");
            }
            if (listIcons.TryGetValue(a, out var icon))
            {
                icon.sprite = LoadIcon(a.Icon);
                icon.enabled = icon.sprite != null;
            }
        }

        // ---- 右边：编辑选中的强化 ----

        private void BuildEditor()
        {
            Widgets.Clear(editorContent);
            NativeUi.SetEnabled(deleteButton, selected != null);
            if (selected == null)
            {
                previewIcon.enabled = false;
                previewName.text = previewDesc.text = "";
                Studio.Block(editorContent).text = "在左边选一个强化，或者新建一个。";
                return;
            }
            var a = selected;
            UpdatePreview();

            var nameRow = NativeUi.Row(editorContent, 44f, 12f);
            NativeUi.Label(nameRow, "名字", 22, 90f);
            Widgets.Text(nameRow, a.Name, "给强化起个名字", -1f, v =>
            {
                a.Name = v;
                Changed(a);
            }, "新强化");

            var looks = NativeUi.Row(editorContent, 52f, 12f);
            NativeUi.Label(looks, "品质", 22, 90f);
            Widgets.Choice(looks, AugmentText.QualityLabels, AugmentText.QualityIndex(a.Quality), 180f, i =>
            {
                a.Quality = AugmentText.QualityFromIndex(i);
                Changed(a);
            });
            NativeUi.Label(looks, "图标", 22, 60f);
            NativeUi.Button(looks, "换图标", false, 160f, () => Picker.Show("选择图标",
                Picker.Relics().Concat(Picker.SkillIcons()).Where(item => item.Icon != null), item =>
                {
                    a.Icon = item.IconPath;
                    Changed(a);
                }));
            Widgets.Spacer(looks);

            var descRow = NativeUi.Row(editorContent, 44f, 12f);
            NativeUi.Label(descRow, "描述", 22, 90f);
            Widgets.Text(descRow, a.Description, "留空就按下面的效果自动生成", -1f, v =>
            {
                a.Description = v;
                Changed(a);
            });

            NativeUi.Heading(editorContent, "获得时（立刻生效一次）");
            if (a.Effects.Count == 0)
                NativeUi.Label(editorContent, "没有。加属性的效果把持续时间填 0 就是本局一直有效", 18, -1f, NativeUi.DimText);
            foreach (var effect in a.Effects.ToList())
                Widgets.ActionRow(editorContent, effect, () => Changed(a), () => Changed(a, structure: true), () =>
                {
                    a.Effects.Remove(effect);
                    Changed(a, structure: true);
                });
            Widgets.AddButton(editorContent, "＋ 添加效果", () =>
            {
                a.Effects.Add(new RuleAction { Seconds = 0 });
                Changed(a, structure: true);
            });

            NativeUi.Heading(editorContent, "之后一直生效的规则");
            NativeUi.Label(editorContent, "这些规则平时不生效，选到这个强化之后本局才生效。规则在规则页编辑。", 18, -1f, NativeUi.DimText);
            foreach (var id in a.Rules.ToList())
            {
                var rule = Packs.Current.Rules.FirstOrDefault(r => r.Id == id);
                var row = NativeUi.Row(editorContent, 44f, 12f);
                NativeUi.Label(row, rule == null ? "<color=#e07a5f>（规则已被删除）</color>" : $"{rule.Name}  <size=16><color=#a6a6a6>{RuleText.Sentence(rule)}</color></size>", 20);
                NativeUi.Button(row, "移除", false, 100f, () =>
                {
                    a.Rules.Remove(id);
                    Changed(a, structure: true);
                });
            }
            // 一条规则只属于一个强化
            var candidates = Packs.Current.Rules.Where(r => AugmentEngine.OwnerOf(r) == null).ToList();
            if (candidates.Count == 0)
            {
                NativeUi.Label(editorContent, "没有可以加的规则：先去规则页新建一条（已经属于别的强化的规则不能再加）", 18, -1f, NativeUi.DimText);
            }
            else
            {
                Dropdown add = null;
                add = Widgets.Choice(editorContent, new[] { "＋ 加一条规则" }.Concat(candidates.Select(r => r.Name)), 0, -1f, i =>
                {
                    add.SetValueWithoutNotify(0);
                    if (i <= 0) return;
                    a.Rules.Add(candidates[i - 1].Id);
                    Changed(a, structure: true);
                });
            }
        }

        private void UpdatePreview()
        {
            if (selected == null) return;
            var sprite = LoadIcon(selected.Icon);
            previewIcon.sprite = sprite;
            previewIcon.enabled = sprite != null;
            // 游戏的三选一卡片会把图标和名字染成品质色
            var color = selected.Quality >= 0 ? QualityColor(selected.Quality) : new Color(1f, 0.746f, 0.146f);
            previewIcon.color = selected.Quality >= 0 ? color : Color.white;
            previewName.color = color;
            string quality = selected.Quality >= 0 ? $"  <size=18>{RelicInfo.QualityName(selected.Quality)}</size>" : "";
            previewName.text = selected.Name + quality;
            previewDesc.text = AugmentText.Description(selected);
        }

        // ---- 修改 ----

        private void Changed(Augment a, bool structure = false)
        {
            Packs.MarkDirty();
            UpdateListItem(a);
            if (a == selected) UpdatePreview();
            if (structure) RequestRebuild(list: false);
        }

        private void Duplicate()
        {
            if (selected == null) return;
            var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<Augment>(Newtonsoft.Json.JsonConvert.SerializeObject(selected));
            copy.Id = Rule.NewId();
            copy.Name = selected.Name + " 副本";
            // 专属规则也复制一份：一条规则只属于一个强化，否则选到一个两边都会解锁
            copy.Rules.Clear();
            foreach (var id in selected.Rules)
            {
                var rule = Packs.Current.Rules.FirstOrDefault(r => r.Id == id);
                if (rule == null) continue;
                var ruleCopy = Newtonsoft.Json.JsonConvert.DeserializeObject<Rule>(Newtonsoft.Json.JsonConvert.SerializeObject(rule));
                ruleCopy.Id = Rule.NewId();
                ruleCopy.Name = rule.Name + " 副本";
                Packs.Current.Rules.Add(ruleCopy);
                copy.Rules.Add(ruleCopy.Id);
            }
            Augments.Insert(Augments.IndexOf(selected) + 1, copy);
            selected = copy;
            Packs.MarkDirty();
            RequestRebuild(list: true);
        }

        // 点一次变成"再点一次确认"，3 秒内再点才删除；它专属的规则留着，变回普通规则
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
            int index = Augments.IndexOf(selected);
            EventLog.Add($"删除强化「{selected.Name}」" + (selected.Rules.Count > 0 ? "，它的规则变回普通规则" : ""));
            Augments.Remove(selected);
            selected = Augments.Count == 0 ? null : Augments[Mathf.Clamp(index, 0, Augments.Count - 1)];
            Packs.MarkDirty();
            RequestRebuild(list: true);
        }

        private void DisarmDelete()
        {
            deleteArmedUntil = 0f;
            if (deleteButton != null) deleteButton.GetComponentInChildren<Text>().text = "删除强化";
        }

        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        private static Sprite LoadIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!icons.TryGetValue(path, out var s)) icons[path] = s = Resources.Load<Sprite>(path);
            return s;
        }

        // 与游戏的品质色一致（nu.bclm）
        private static Color QualityColor(int q)
        {
            switch (q)
            {
                case 0: return new Color(0.9f, 0.9f, 0.9f);
                case 1: return new Color(0.404f, 0.898f, 1f);
                case 2: return new Color(0.608f, 0.188f, 1f);
                case 3: return new Color(0.973f, 0.506f, 0f);
                default: return new Color(0.992f, 0.118f, 0.180f);
            }
        }
    }
}
