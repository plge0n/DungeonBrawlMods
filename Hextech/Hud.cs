using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hextech
{
    // 已选强化的图标：放在背包右边、传送头像（"F1"那格）下面，4 张以内 2×2、更多时 3 列，悬停用游戏原生的提示框显示效果。
    // 联机时那一栏里还有队友的头像：排在最下面那个头像的下面，有人进出时重新排（没实测）
    internal static class Hud
    {
        private const string StripName = "HextechStrip";
        private const float Gap = 6f;
        private static RectTransform strip;
        private static int shown = -1;
        // 建图标栏时传送栏里有几个头像（不含图标栏自己）
        private static int heads = -1;

        public static void Refresh() => shown = -1;

        public static void Tick()
        {
            var picks = Engine.Active ? Engine.Picks : (IReadOnlyList<Card>)new Card[0];
            if (picks.Count == 0)
            {
                RemoveAll();
                return;
            }
            // 有人进出房间，头像数变了：重新排
            if (strip != null && strip.parent != null && strip.parent.childCount - 1 != heads) RemoveAll();
            if (strip == null)
            {
                strip = Create();
                if (strip == null) return;
                heads = strip.parent.childCount - 1;
                shown = -1;
            }
            if (shown == picks.Count) return;
            shown = picks.Count;
            foreach (Transform c in strip) Object.Destroy(c.gameObject);
            // 4 张以内 2×2，多了（棱彩蛋、嬗变的额外选择）排成 3 列，免得往下太长
            int columns = picks.Count > 4 ? 3 : 2;
            for (int i = 0; i < picks.Count; i++) AddIcon(i, picks[i], columns);
        }

        // 找传送头像栏（ui_playerState 下的 trans_teleportTip），按它第一格的大小、位置排
        private static RectTransform Create()
        {
            var first = SkillBar.Buttons().FirstOrDefault();
            var home = first?.Icon?.parent?.parent;
            var teleport = home?.Find("trans_teleportTip") as RectTransform;
            if (teleport == null || teleport.childCount == 0) return null;
            var head = (RectTransform)teleport.GetChild(0);

            var go = new GameObject(StripName, typeof(RectTransform), typeof(LayoutElement));
            // 放在传送栏里面但不参加它的格子布局
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)go.transform;
            rt.SetParent(teleport, false);
            rt.anchorMin = rt.anchorMax = head.anchorMin;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = Vector2.zero;
            // 以头像格的左边、最下面那个头像的底边为起点往下排（联机时栏里有好几个头像）
            float bottom = head.anchoredPosition.y - head.rect.height * head.pivot.y;
            for (int i = 1; i < teleport.childCount; i++)
                if (teleport.GetChild(i) is RectTransform other && other != rt && other.gameObject.activeSelf)
                    bottom = Mathf.Min(bottom, other.anchoredPosition.y - other.rect.height * other.pivot.y);
            rt.anchoredPosition = new Vector2(head.anchoredPosition.x - head.rect.width * head.pivot.x, bottom - Gap);
            rt.SetAsLastSibling();
            return rt;
        }

        private static float CellSize => strip.parent.GetChild(0) is RectTransform head ? head.rect.width : 40f;

        private static void AddIcon(int index, Card card, int columns)
        {
            float size = CellSize;
            var frame = new GameObject("augment" + index, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)frame.transform;
            rt.SetParent(strip, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(index % columns * (size + Gap), -(index / columns) * (size + Gap));
            frame.GetComponent<Image>().color = TierColor(card.Tier);

            var icon = new GameObject("icon", typeof(RectTransform), typeof(Image));
            var irt = (RectTransform)icon.transform;
            irt.SetParent(rt, false);
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(3f, 3f);
            irt.offsetMax = new Vector2(-3f, -3f);
            var img = icon.GetComponent<Image>();
            img.raycastTarget = false;
            string path = Engine.IconFor(card);
            var sprite = string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
            if (sprite != null) img.sprite = sprite;
            else img.color = new Color(0.15f, 0.15f, 0.2f);

            var trigger = frame.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => NativeTip.Show(
                $"{card.Name}（{Engine.TierName(card.Tier)}）", string.Join("\n", card.Lines), path, rt.position, Engine.QualityColor(card.Tier)));
            AddTrigger(trigger, EventTriggerType.PointerExit, NativeTip.Hide);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private static Color TierColor(Tier t)
            => t == Tier.Silver ? new Color(0.78f, 0.82f, 0.88f) : t == Tier.Gold ? new Color(1f, 0.78f, 0.28f) : new Color(0.78f, 0.5f, 1f);

        // 专属主动技能悬停提示末尾追加的文字
        public static string TooltipText(IReadOnlyList<Card> picks)
        {
            if (picks.Count == 0) return null;
            var lines = picks.Select(c => $"· {c.Name}（{Engine.TierName(c.Tier)}）：{string.Join("；", c.Lines)}");
            return "<color=#C38BFF>海克斯强化</color>\n" + string.Join("\n", lines);
        }

        public static void RemoveAll()
        {
            if (strip != null)
            {
                NativeTip.Hide();
                // 先移出传送栏：Destroy 到帧末才生效，同一帧重建时不能把它算进头像数
                strip.SetParent(null, false);
                Object.Destroy(strip.gameObject);
            }
            strip = null;
            shown = -1;
        }
    }
}
