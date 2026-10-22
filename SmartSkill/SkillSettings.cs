using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace SmartSkill
{
    // 挂到设置界面（DBStudio 的 Mod 页）里的"每个技能的规则"：不进战斗也能改模式、套推荐；细节条件仍在齿轮弹窗里改
    internal static class SkillSettings
    {
        private static readonly string[] ModeLabels = { "原版", "自定义条件", "永不自动" };

        private sealed class Row
        {
            public SkillInfo Info;
            public GameObject Root;
            public Text Summary, ModeText;
        }

        private static readonly List<Row> rows = new List<Row>();

        public static void Build(RectTransform container)
        {
            rows.Clear();
            NativeUi.Label(container, "右键技能图标开启自动释放后，按这里的规则决定放不放。条件细节在战斗中点技能图标上的齿轮修改。", 18, -1f, NativeUi.DimText);
            if (!GameData.Ready)
            {
                NativeUi.Label(container, "游戏还没加载完，进入主菜单后再看", 18, -1f, NativeUi.DimText);
                return;
            }
            var search = NativeUi.Input(container, "搜索技能名字", -1f, null);

            // 改过的排在前面
            var skills = GameData.Entries(DataTable.Skill)
                .Select(e => (entry: e, info: SkillInfo.Find(e.Key)))
                .Where(x => x.info != null && x.info.CanAutoCast)
                .OrderBy(x => RuleStore.Peek(x.info)?.Mode == RuleMode.Vanilla || RuleStore.Peek(x.info) == null)
                .ToList();
            foreach (var (entry, info) in skills) rows.Add(AddRow(container, entry, info));
            search.onValueChanged.AddListener(filter =>
            {
                foreach (var r in rows)
                    r.Root.SetActive(string.IsNullOrEmpty(filter) || r.Info.Name.Contains(filter) || r.Info.Key.Contains(filter));
            });
            Refresh();
        }

        public static void Refresh()
        {
            foreach (var r in rows)
            {
                if (r.Summary == null) continue;
                var rule = RuleStore.Peek(r.Info);
                var mode = rule?.Mode ?? RuleMode.Vanilla;
                r.ModeText.text = ModeLabels[(int)mode];
                r.Summary.text = rule == null || mode != RuleMode.Custom ? "" : RuleText.Summary(rule, r.Info);
            }
        }

        private static Row AddRow(Transform parent, DataEntry entry, SkillInfo info)
        {
            var row = NativeUi.Row(parent, 60f, 12f);
            var icon = new GameObject("icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(row, false);
            var le = icon.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 48f;
            var img = icon.GetComponent<Image>();
            img.preserveAspect = true;
            img.sprite = entry.Icon;
            img.enabled = entry.Icon != null;

            var texts = NativeUi.Column(row, -1f, 0f);
            NativeUi.Label(texts, info.Name, 22);
            var summary = NativeUi.Label(texts, "", 16, -1f, NativeUi.DimText);

            var r = new Row { Info = info, Root = row.gameObject, Summary = summary };
            var mode = NativeUi.Button(row, "", false, 170f, () =>
            {
                // 原版 → 自定义（第一次切到自定义时套推荐）→ 永不 → 原版
                var rule = RuleStore.Get(info);
                if (rule.Mode == RuleMode.Vanilla && !HasConditions(rule)) RuleStore.Set(info, Recommend.For(info));
                else
                {
                    rule.Mode = (RuleMode)(((int)rule.Mode + 1) % ModeLabels.Length);
                    RuleStore.MarkDirty();
                }
                Refresh();
            });
            r.ModeText = mode.GetComponentInChildren<Text>();
            NativeUi.Button(row, "按标签推荐", false, 170f, () =>
            {
                RuleStore.Set(info, Recommend.For(info));
                Refresh();
            });
            return r;
        }

        private static bool HasConditions(SkillRule r)
            => r.UseHpBelow || r.UseHpAbove || r.UseManaAfter || r.UseEnemyCount || r.UseBossOrElite || r.UseNoBoss
               || r.UseTargetHpBelow || r.UseKillable || r.UseEffectEnded || r.KeepCharges > 0 || r.CastWithoutEnemy;
    }

    // 自定义规则的一句话摘要
    internal static class RuleText
    {
        public static string Summary(SkillRule r, SkillInfo info)
        {
            var parts = new List<string>();
            if (r.UseHpBelow) parts.Add($"血量 ≤ {r.HpBelowPercent:0}%");
            if (r.UseHpAbove) parts.Add($"血量 ≥ {r.HpAbovePercent:0}%");
            if (r.UseManaAfter) parts.Add($"放完剩蓝 ≥ {r.ManaAfterPercent:0}%");
            if (r.UseEnemyCount) parts.Add($"敌人 ≥ {r.MinEnemies} 个");
            if (r.UseBossOrElite) parts.Add("有 Boss 或精英");
            if (r.UseNoBoss) parts.Add("没有 Boss");
            if (r.UseTargetHpBelow) parts.Add($"有敌人血量 ≤ {r.TargetHpBelowPercent:0}%");
            if (r.UseKillable) parts.Add($"能斩杀 ≥ {r.MinKillable} 个");
            if (r.UseEffectEnded) parts.Add("上次效果结束后");
            string when = parts.Count == 0 ? "冷却好就放" : string.Join(r.Combine == Combine.All ? "，且 " : "，或 ", parts);
            var extra = new List<string>();
            if (r.KeepCharges > 0) extra.Add($"留 {r.KeepCharges} 层充能");
            if (info.IsSelfCastable && r.CastWithoutEnemy) extra.Add("没敌人也放");
            return when + (extra.Count > 0 ? "（" + string.Join("，", extra) + "）" : "");
        }
    }
}
