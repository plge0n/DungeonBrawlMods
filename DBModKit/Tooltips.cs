using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DBModKit
{
    /// <summary>游戏原生的悬停提示框（鼠标停在技能、遗物上时出现的那个）。</summary>
    public static class NativeTip
    {
        /// <summary>在 position（世界坐标，一般是图标的 transform.position）旁边显示。quality 0~4 按品质上色，-1 不上色。</summary>
        public static void Show(string title, string text, string iconPath, Vector3 position, int quality = -1)
        {
            if (!ModKit.Compatible) return;
            Safe.Run("NativeTip.Show", () => GameRaw.ShowTip(title, text, iconPath, position, quality));
        }

        public static void Hide()
        {
            if (!ModKit.Compatible) return;
            Safe.Run("NativeTip.Hide", GameRaw.HideTip);
        }
    }

    /// <summary>
    /// 在本地玩家某个主动技能的悬停提示末尾追加文字（用 <see cref="ModContext.SetSkillTooltip"/> 设置，卸载时自动去掉）。
    /// 游戏自己写的附加说明保留在前面。
    /// </summary>
    public static class SkillTooltips
    {
        private static readonly Dictionary<(string owner, int id), string> entries = new Dictionary<(string, int), string>();
        // 每个技能：游戏的原文、我们最后写进去的完整文字。当前值和写进去的不一样，说明游戏改过（或重试波次还原成了旧值），
        // 去掉我们追加过的部分当作新的原文
        private static readonly Dictionary<int, (string original, string value)> written = new Dictionary<int, (string, string)>();
        private static readonly HashSet<string> suffixes = new HashSet<string>();
        private static Component owner;

        internal static void Set(string mod, int skillId, string text)
        {
            if (string.IsNullOrEmpty(text)) entries.Remove((mod, skillId));
            else entries[(mod, skillId)] = text;
        }

        internal static void ClearOwner(string mod)
        {
            foreach (var key in entries.Keys.Where(k => k.owner == mod).ToList()) entries.Remove(key);
        }

        private static string Suffix(int skillId)
        {
            var parts = entries.Where(kv => kv.Key.id == skillId).Select(kv => kv.Value).ToList();
            return string.Join("\n", parts);
        }

        internal static void Tick()
        {
            var player = GameRaw.LocalPlayer();
            if (player != owner)
            {
                written.Clear();
                suffixes.Clear();
                owner = player;
            }
            if (player == null) return;
            var ids = new HashSet<int>(entries.Keys.Select(k => k.id));
            ids.UnionWith(written.Keys);
            foreach (int id in ids)
            {
                var skill = GameRaw.FindActiveSkill(player, id);
                if (skill == null) continue;
                string current = GameRaw.SkillExtraInfo(skill) ?? "";
                string original;
                if (written.TryGetValue(id, out var w) && current == w.value) original = w.original;
                else
                {
                    original = current;
                    // 原文为空时追加的部分前面没有换行，两种都要认
                    foreach (var x in suffixes.OrderByDescending(x => x.Length))
                        if (original == x) original = "";
                        else if (original.EndsWith("\n" + x)) original = original.Substring(0, original.Length - x.Length - 1);
                }
                string suffix = Suffix(id);
                if (suffix.Length > 0) suffixes.Add(suffix);
                string value = suffix.Length == 0 ? original : original.Length == 0 ? suffix : original + "\n" + suffix;
                if (value != current) GameRaw.SetSkillExtraInfo(skill, value.Length == 0 ? null : value);
                if (suffix.Length == 0) written.Remove(id);
                else written[id] = (original, value);
            }
        }
    }
}
