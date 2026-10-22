using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.UI;

namespace DamageMeter
{
    // 屏幕右侧的统计面板。除了"本波/本局"按钮，都不接收点击，不挡游戏操作
    internal static class Panel
    {
        private const float Width = 420f;
        private const float RefreshEvery = 0.5f;

        private static Canvas canvas;
        private static RectTransform root;
        private static Text title, footer;
        private static Text scopeLabel;
        private static readonly List<(GameObject row, Text name, Text damage, Text share)> rows = new List<(GameObject, Text, Text, Text)>();
        private static float nextRefresh;

        public static void Tick()
        {
            bool show = Plugin.Enabled.Value && Game.InBattle;
            if (!show)
            {
                if (root != null) root.gameObject.SetActive(false);
                return;
            }
            if (root == null && !Build()) return;
            root.gameObject.SetActive(true);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshEvery;
            Refresh();
        }

        public static void Destroy()
        {
            if (canvas != null) Object.Destroy(canvas.gameObject);
            canvas = null;
            root = null;
            rows.Clear();
        }

        // 位置、大小、行数改了就重建
        public static void Rebuild()
        {
            Destroy();
            nextRefresh = 0f;
        }

        private static bool Build()
        {
            if (!NativeUi.Load()) return false;
            canvas = NativeUi.Overlay("DamageMeter", 4000);
            root = NativeUi.Panel(canvas.transform, "DamageMeterPanel");
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 0.5f);
            root.anchoredPosition = new Vector2(-Plugin.OffsetX.Value, Plugin.OffsetY.Value);
            root.sizeDelta = new Vector2(Width, 0f);
            root.localScale = Vector3.one * Plugin.Scale.Value;
            var bg = root.GetComponent<Image>();
            bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, Plugin.Opacity.Value);
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 14);
            layout.spacing = 2f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = NativeUi.Row(root, 36f);
            title = NativeUi.Label(header, "", 20, -1, NativeUi.Gold);
            var scope = NativeUi.Button(header, "", false, 96f, () =>
            {
                Plugin.ShowRun.Value = !Plugin.ShowRun.Value;
                nextRefresh = 0f;
            });
            scopeLabel = scope.GetComponentInChildren<Text>();
            scopeLabel.fontSize = 16;

            for (int i = 0; i < Plugin.MaxRows.Value; i++)
            {
                var row = NativeUi.Row(root, 26f, 8f);
                var name = NativeUi.Label(row, "", 18);
                var damage = NativeUi.Label(row, "", 18, 90f);
                damage.alignment = TextAnchor.MiddleRight;
                var share = NativeUi.Label(row, "", 18, 56f, NativeUi.DimText);
                share.alignment = TextAnchor.MiddleRight;
                rows.Add((row.gameObject, name, damage, share));
            }
            footer = NativeUi.Label(root, "", 16, -1, NativeUi.DimText);

            // 只有按钮接收点击
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = g.GetComponentInParent<Button>() != null;
            return true;
        }

        private static void Refresh()
        {
            bool run = Plugin.ShowRun.Value;
            var period = run ? Meter.Run : Meter.Wave;
            title.text = run ? "伤害统计 · 本局" : Meter.WaveNumber > 0 ? $"伤害统计 · 第 {Meter.WaveNumber} 波" : "伤害统计 · 本波";
            scopeLabel.text = run ? "看本波" : "看本局";

            long total = period.Total;
            var sources = period.Sources.ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                bool has = i < sources.Count;
                r.row.SetActive(has || i == 0);
                if (!has)
                {
                    r.name.text = i == 0 ? "还没有造成伤害" : "";
                    r.damage.text = r.share.text = "";
                    continue;
                }
                var s = sources[i];
                string crit = s.Hits > 0 && s.Crits > 0 ? $" <size=14><color=#a6a6a6>暴击 {100f * s.Crits / s.Hits:0}%</color></size>" : "";
                r.name.text = s.Name + crit;
                r.damage.text = Meter.Format(s.Damage);
                r.share.text = total > 0 ? $"{100.0 * s.Damage / total:0}%" : "";
            }

            long maxHit = sources.Count > 0 ? sources.Max(s => s.MaxHit) : 0;
            footer.text = $"总计 {Meter.Format(total)}   DPS {Meter.Format(period.Dps)}   用时 {Meter.FormatTime(period.Seconds)}\n"
                + $"最高一击 {Meter.Format(maxHit)}   承受 {Meter.Format(period.Taken)}   击杀 {period.Kills}";
        }
    }
}
