using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DBModKit
{
    /// <summary>dbmod.json 里的一条依赖。</summary>
    public sealed class ManifestDependency
    {
        /// <summary>依赖的插件 GUID，或内容包的 Id（可以写成 "content:Id"，也可以只写 Id）。</summary>
        public string Guid = "";
        /// <summary>显示用的名字；不写时显示 Guid。</summary>
        public string Name = "";
        /// <summary>最低版本；不写时不检查版本。</summary>
        public string MinVersion = "";
        /// <summary>创意工坊物品号：缺这个依赖时给玩家一个打开工坊页面的按钮。</summary>
        public ulong Workshop;

        public string DisplayName => string.IsNullOrEmpty(Name) ? Guid : Name;
    }

    public enum ModIssueKind
    {
        /// <summary>缺依赖，或依赖的版本太旧：这个 Mod / 内容包没有加载。</summary>
        MissingDependency,
        /// <summary>两个 Mod 争同一样东西，只有一个生效。</summary>
        Conflict,
        /// <summary>两个 Mod 的效果叠在一起（比如同一个技能被两个 Mod 调了倍率，会相乘），不一定是问题。</summary>
        Stacking,
    }

    /// <summary>Mod 之间的一个问题。</summary>
    public sealed class ModIssue
    {
        public ModIssueKind Kind { get; }
        public string Title { get; }
        /// <summary>现在怎么处理的。</summary>
        public string Detail { get; }
        /// <summary>缺的依赖在创意工坊上的物品号；没有时为 0。</summary>
        public ulong WorkshopId { get; }

        internal ModIssue(ModIssueKind kind, string title, string detail, ulong workshopId = 0)
        {
            Kind = kind;
            Title = title;
            Detail = detail;
            WorkshopId = workshopId;
        }
    }

    /// <summary>
    /// Mod 之间的依赖和冲突。依赖写在 dbmod.json 的 Dependencies 里，工坊插件和内容包都按它排序加载；
    /// 冲突包括：抢同一个处理器（自动释放、走位，先注册的优先、玩家可以指定）、改同一项数值（后改的生效）、内容包 Id 重复（只用前一个），
    /// 以及同一个技能被几个 Mod 调整（倍率相乘）。都显示在 Mod 设置 → DBModKit → 依赖和冲突。
    /// </summary>
    public static class ModConflicts
    {
        /// <summary>有变化时加 1，界面据此刷新。</summary>
        public static int Version { get; private set; }

        internal static void Touch() => Version++;

        /// <summary>现在的所有问题（每次调用重新算）。</summary>
        public static List<ModIssue> All()
        {
            var list = new List<ModIssue>();
            foreach (var item in Workshop.Items)
                foreach (var dep in item.MissingDependencies)
                    list.Add(Missing(item.DisplayName, dep));
            foreach (var pack in ContentPacks.All)
                foreach (var dep in pack.MissingDependencies)
                    list.Add(Missing("内容包 " + pack.DisplayName, dep));

            AddSlot(list, AutoCast.Slot);
            AddSlot(list, MoveControl.Slot);
            foreach (var (what, first, second, cleared) in GameData.Overlaps)
                list.Add(new ModIssue(ModIssueKind.Conflict, cleared ? $"数值：{second} 载入改动时清掉了 {first} 改的 {what}" : $"数值：{first} 和 {second} 都改了 {what}",
                    cleared ? "现在只有后载入的改动" : $"后改的生效（{second}）"));
            foreach (var (pack, other) in ContentPacks.Duplicates)
                list.Add(new ModIssue(ModIssueKind.Conflict, $"内容包 Id 重复：{pack}", $"只用前一个（{other}）"));
            foreach (var (skill, owners) in SkillTuning.Shared())
                list.Add(new ModIssue(ModIssueKind.Stacking, $"技能「{SkillInfo.Get(skill)?.Name ?? skill.ToString()}」被 {string.Join("、", owners)} 一起调整", "倍率相乘"));
            return list;
        }

        private static ModIssue Missing(string who, ManifestDependency dep)
            => new ModIssue(ModIssueKind.MissingDependency, $"{who} 缺少依赖 {dep.DisplayName}" + (string.IsNullOrEmpty(dep.MinVersion) ? "" : $" {dep.MinVersion}+"),
                dep.Workshop != 0 ? "订阅后重启游戏" : "装上它后重启游戏", dep.Workshop);

        private static void AddSlot<T>(List<ModIssue> list, HandlerSlot<T> slot) where T : class
        {
            var owners = slot.Owners;
            if (owners.Count < 2) return;
            string how = slot.Preferred != null && slot.Preferred == slot.Owner ? "你指定的" : "先注册的优先";
            list.Add(new ModIssue(ModIssueKind.Conflict, $"{slot.Label}：{string.Join("、", owners.Select(Multiplayer.ModName))} 都想接管",
                $"现在由 {Multiplayer.ModName(slot.Owner)} 处理（{how}），其他的不生效"));
        }

        // ---- 依赖检查 ----

        // null = 满足；否则是给玩家看的原因（缺的以"缺少依赖"开头）
        internal static string Check(ManifestDependency dep)
        {
            if (dep == null || string.IsNullOrEmpty(dep.Guid)) return null;
            string version;
            if (!dep.Guid.StartsWith("content:") && PluginVersion(dep.Guid) is string pv) version = pv;
            else
            {
                string id = ContentGuid(dep.Guid);
                var pack = ContentPacks.All.FirstOrDefault(p => p.Guid == id);
                if (pack == null || !pack.Loaded)
                    return $"缺少依赖 {dep.DisplayName}" + (pack != null ? "（内容包没有加载）" : dep.Workshop != 0 ? $"（工坊 {dep.Workshop}）" : "");
                version = pack.Manifest.Version;
            }
            if (!string.IsNullOrEmpty(dep.MinVersion) && System.Version.TryParse(dep.MinVersion, out var need)
                && System.Version.TryParse(version ?? "", out var have) && have < need)
                return $"需要 {dep.DisplayName} {need}，当前是 {have}";
            return null;
        }

        internal static string ContentGuid(string guid) => guid.StartsWith("content:") ? guid : "content:" + guid;

        private static string PluginVersion(string guid)
        {
            if (Multiplayer.ModVersion(guid) is string v) return v;
            return BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(guid, out var info) ? info.Metadata.Version.ToString() : null;
        }
    }

    // DBStudio Mod 页 → DBModKit 里的"依赖和冲突"一栏
    internal static class ModConflictSettings
    {
        private static RectTransform list;
        private static int shownVersion = -1;
        private static int shownWorkshop = -1, shownContent = -1;

        public static void Register() => ModSettings.Register(ModKitPlugin.Guid, "依赖和冲突", Build, Refresh);

        private static void Build(RectTransform container)
        {
            NativeUi.Label(container, "缺依赖的 Mod 不会加载；自动释放、走位同一时间只能有一个 Mod 接管，可以在这里指定。", 16, -1f, NativeUi.DimText);
            list = NativeUi.Column(container, -1f, 6f);
            list.GetComponent<LayoutElement>().flexibleHeight = 0f;
            shownVersion = -1;
            Refresh();
        }

        private static void Refresh()
        {
            if (list == null) return;
            if (shownVersion == ModConflicts.Version && shownWorkshop == Workshop.Version && shownContent == ContentPacks.Version) return;
            shownVersion = ModConflicts.Version;
            shownWorkshop = Workshop.Version;
            shownContent = ContentPacks.Version;
            for (int i = list.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.GetChild(i).gameObject);

            AddChooser(AutoCast.Slot);
            AddChooser(MoveControl.Slot);
            var issues = ModConflicts.All();
            if (issues.Count == 0)
            {
                NativeUi.Label(list, "没有发现问题", 18, -1f, NativeUi.DimText);
                return;
            }
            foreach (var issue in issues)
            {
                var row = NativeUi.Row(list, 56f, 12f);
                var texts = NativeUi.Column(row, -1f, 0f);
                string color = issue.Kind == ModIssueKind.MissingDependency ? "#e07a5f" : issue.Kind == ModIssueKind.Conflict ? "#e0b35f" : "#a6a6a6";
                NativeUi.Label(texts, $"<color={color}>{WorkshopPrompt.Plain(issue.Title)}</color>", 18);
                NativeUi.Label(texts, WorkshopPrompt.Plain(issue.Detail), 15, -1f, NativeUi.DimText);
                ulong id = issue.WorkshopId;
                if (id != 0) NativeUi.Button(row, "打开工坊页面", false, 200f, () => HeroSkins.OpenWorkshopPage(id));
            }
        }

        // 有两个以上 Mod 抢的时候给一个下拉框：第一项是"先注册的优先"
        private static void AddChooser<T>(HandlerSlot<T> slot) where T : class
        {
            var owners = slot.Owners;
            if (owners.Count < 2) return;
            var row = NativeUi.Row(list, 48f, 12f);
            NativeUi.Label(row, $"{slot.Label}由谁处理", 20, 220f);
            var options = new List<string> { "先注册的优先" };
            options.AddRange(owners.Select(Multiplayer.ModName));
            var dropdown = NativeUi.Dropdown(row, options, 360f, i => slot.Preferred = i <= 0 ? null : owners[i - 1]);
            int selected = slot.Preferred == null ? 0 : owners.ToList().IndexOf(slot.Preferred) + 1;
            dropdown.SetValueWithoutNotify(Mathf.Max(0, selected));
        }
    }
}
