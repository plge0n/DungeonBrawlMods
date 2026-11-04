using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DBStudio.Pages
{
    internal sealed class BuildPage : IStudioPage
    {
        private static readonly LoadoutKind[] Kinds = { LoadoutKind.Active, LoadoutKind.Passive, LoadoutKind.Equipment, LoadoutKind.Relic };
        private static readonly string[] Names = { "主动技能", "被动技能", "装备", "遗物" };
        private readonly Dictionary<LoadoutKind, RectTransform> lists = new Dictionary<LoadoutKind, RectTransform>();
        private readonly Dictionary<LoadoutKind, IReadOnlyList<LoadoutItem>> shown = new Dictionary<LoadoutKind, IReadOnlyList<LoadoutItem>>();
        private readonly List<(Button button, Func<bool> allowed)> controls = new List<(Button, Func<bool>)>();
        private Text hint, cardStatus;
        private Toggle unlock;
        private Button permanent;
        private string lastBackup;
        private int skillCapacity, equipCapacity;
        private bool dirty = true;

        public string Title => "构筑";

        public void Build(RectTransform body)
        {
            lists.Clear();
            shown.Clear();
            controls.Clear();
            dirty = true;
            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            hint = NativeUi.Label(body, "", 18, -1f, NativeUi.DimText);
            var area = new GameObject("loadout", typeof(RectTransform), typeof(LayoutElement));
            area.transform.SetParent(body, false);
            area.GetComponent<LayoutElement>().flexibleHeight = 1f;
            var columns = Studio.Columns(area.transform, 12f);
            for (int i = 0; i < Kinds.Length; i++)
            {
                var column = NativeUi.Column(columns);
                var kind = Kinds[i];
                NativeUi.Heading(column, Names[i]);
                lists[kind] = NativeUi.ScrollView(column, out _);
                var button = NativeUi.Button(column, "添加", false, 140f, () => Select(kind, null));
                controls.Add((button, () => true));
            }
            NativeUi.Separator(body);
            NativeUi.Heading(body, "卡牌（局外）");
            var row = NativeUi.Row(body, 44f, 16f);
            unlock = NativeUi.Toggle(row, "卡牌全解锁（不改存档）", 510f, on =>
            {
                bool before = CardCollection.Enabled;
                string collectionBefore = CardCollection.Summary;
                bool ok = CardCollection.SetEnabled(on);
                EventLog.Add($"构筑：卡牌开关 {before} → {CardCollection.Enabled}，{collectionBefore} → {CardCollection.Summary}" + (ok ? "" : $"，失败 {CardCollection.LastError}"));
                if (!ok) ScreenNotice.Show(CardCollection.LastError);
                Studio.RefreshSoon();
            });
            permanent = NativeUi.Button(row, "写进存档（永久）", false, 260f, ConfirmPermanent);
            NativeUi.Label(body, "库存至少 7 张，记忆容量 50。开关默认关闭，设置会保留。开着时编的卡组和预设不写盘，关闭后恢复原卡组。\n开着时不能合成、分解或升级容量。战斗中切换只影响下一局。永久写入仅在局外可用。", 18, -1f, NativeUi.DimText);
            cardStatus = NativeUi.Label(body, "", 16, -1f, NativeUi.DimText);
        }

        public void Refresh()
        {
            bool canEdit = Loadout.CanEdit;
            hint.text = !Game.InBattle ? "进入单人战斗后可编辑构筑。卡牌功能可在局外使用。"
                : !Game.IsSolo ? "联机时停用构筑编辑，会影响其他玩家。"
                : "只影响本地玩家，排行榜照常拦截。主动和被动共用技能栏。被动暂支持增删，等级显示为游戏内部等级。";
            var current = Kinds.ToDictionary(k => k, Loadout.Current);
            int skills = Loadout.Capacity(LoadoutKind.Active), equipment = Loadout.Capacity(LoadoutKind.Equipment);
            if (dirty || skills != skillCapacity || equipment != equipCapacity || Kinds.Any(k => !shown.TryGetValue(k, out var old) || !old.SequenceEqual(current[k])))
            {
                dirty = false;
                skillCapacity = skills;
                equipCapacity = equipment;
                // 添加按钮保留，行内按钮重建。
                controls.RemoveAll(c => c.button == null || Kinds.Any(k => c.button.transform.IsChildOf(lists[k])));
                foreach (var kind in Kinds)
                {
                    Widgets.Clear(lists[kind]);
                    shown[kind] = current[kind];
                    foreach (var item in current[kind]) BuildItem(lists[kind], item);
                    if (kind == LoadoutKind.Equipment)
                        for (int i = current[kind].Count; i < Math.Max(6, equipment); i++) NativeUi.Label(lists[kind], $"第 {i + 1} 格　空", 18, -1f, NativeUi.DimText);
                    else if (current[kind].Count == 0) NativeUi.Label(lists[kind], "暂无", 18, -1f, NativeUi.DimText);
                }
            }
            foreach (var control in controls)
                if (control.button != null) NativeUi.SetEnabled(control.button, canEdit && control.allowed());
            unlock.SetIsOnWithoutNotify(CardCollection.Enabled);
            NativeUi.SetEnabled(unlock, CardCollection.Available);
            NativeUi.SetEnabled(permanent, CardCollection.Ready && !Game.InBattle);
            cardStatus.text = !CardCollection.Available ? CardCollection.UnavailableReason
                : !CardCollection.Ready ? "等待读取存档，开关启用后会自动套用。"
                : lastBackup != null ? "已备份到 " + lastBackup
                : "永久写入前会复制存档目录中的 save.json*，备份放在同目录的 DBModKit-backup-年月日-时分秒 文件夹。";
        }

        private void BuildItem(Transform parent, LoadoutItem item)
        {
            var box = NativeUi.Column(parent, -1f, 4f);
            box.GetComponent<LayoutElement>().flexibleHeight = 0f;
            var row = NativeUi.Row(box, 48f, 8f);
            Widgets.Icon(row, item.Entry.Icon, 44f);
            string level = item.Entry.Kind == LoadoutKind.Active ? "" : item.Entry.Kind == LoadoutKind.Equipment
                ? $"  {item.Level}/{item.MaxLevel}" : $"  等级 {item.Level}";
            NativeUi.Label(row, item.Entry.Name + level, 18);
            var actions = NativeUi.Row(box, 38f, 6f);
            if (item.Entry.Kind == LoadoutKind.Active) Button(actions, "替换", 80f, () => Select(item.Entry.Kind, item));
            if (item.Entry.Kind == LoadoutKind.Equipment || item.Entry.Kind == LoadoutKind.Relic)
            {
                Button(actions, "−", 48f, () => Act(item.Entry.Kind, "降级 " + item.Entry.Name, () => Loadout.SetLevel(item, item.Level - 1)), () => item.Level > 0);
                Button(actions, "+", 48f, () => Act(item.Entry.Kind, "升级 " + item.Entry.Name, () => Loadout.SetLevel(item, item.Level + 1)), () => item.CanUpgrade);
            }
            Button(actions, "移除", 80f, () => Act(item.Entry.Kind, "移除 " + item.Entry.Name, () => Loadout.Remove(item)));
        }

        private void Button(Transform parent, string text, float width, Action action, Func<bool> allowed = null)
        {
            var button = NativeUi.Button(parent, text, false, width, () => action());
            button.GetComponent<LayoutElement>().preferredHeight = 38f;
            controls.Add((button, allowed ?? (() => true)));
        }

        private void Select(LoadoutKind kind, LoadoutItem replace)
        {
            if (!Loadout.CanEdit) return;
            if (replace == null)
            {
                int count = kind == LoadoutKind.Equipment ? Loadout.Current(kind).Count
                    : Loadout.Current(LoadoutKind.Active).Count + Loadout.Current(LoadoutKind.Passive).Count;
                if (kind != LoadoutKind.Relic && count >= Loadout.Capacity(kind))
                {
                    string reason = kind == LoadoutKind.Equipment ? "装备已满 6 件，请先移除。" : "技能栏已满，请先移除或使用替换。";
                    ScreenNotice.Show(reason);
                    EventLog.Add($"构筑：添加失败，数量 {count} → {count}，{reason}");
                    return;
                }
            }
            Picker.Show("选择" + Names[(int)kind], Loadout.All(kind).Select(e => new Picker.Item
            {
                Key = e.Id.ToString(), Name = e.Name, Icon = e.Icon, IconPath = e.IconPath,
            }), picked => Act(kind, (replace == null ? "添加 " : "替换 ") + picked.Name,
                () => replace == null ? Loadout.Add(kind, int.Parse(picked.Key)) : Loadout.Replace(replace, int.Parse(picked.Key))));
        }

        private void Act(LoadoutKind kind, string operation, Func<bool> change)
        {
            string before = Describe(kind);
            bool ok = Loadout.CanEdit && change();
            string after = Describe(kind);
            EventLog.Add($"构筑：{operation}，[{before}] → [{after}]" + (ok ? "" : "，失败"));
            if (!ok) ScreenNotice.Show(Loadout.LastError ?? "仅限单人战斗中的本地玩家");
            dirty = true;
            Studio.RefreshSoon();
        }

        private static string Describe(LoadoutKind kind)
            => string.Join("、", Loadout.Current(kind).Select(i => $"{i.Entry.Name}({i.Entry.Id})/{i.Level}"));

        private void ConfirmPermanent()
        {
            if (!CardCollection.Ready || Game.InBattle) return;
            Picker.Confirm("确认永久写入卡牌", "将所有可用卡牌的库存补到至少 7 张，记忆容量设为 50。\n\n永久生效，卸载 Mod 也会保留，Steam 会把存档同步到云端。恢复只能使用备份。\n\n确认后先备份 save.json*。运行时开关会关闭，并恢复真实收藏和原卡组后再写盘。", () =>
            {
                if (Game.InBattle || !CardCollection.Ready) return;
                bool before = CardCollection.Enabled;
                string collectionBefore = CardCollection.Summary;
                bool ok = CardCollection.WritePermanently(out var backup);
                EventLog.Add($"构筑：永久卡牌写入，开关 {before} → {CardCollection.Enabled}，{collectionBefore} → {CardCollection.Summary}，" + (ok ? $"完成，备份 {backup}" : $"失败 {CardCollection.LastError}"));
                if (ok) lastBackup = backup;
                ScreenNotice.Show(ok ? "永久写入完成，备份位于 " + backup : CardCollection.LastError, 12f);
                Studio.RefreshSoon();
            });
        }
    }
}
