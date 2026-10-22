using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBModKit
{
    /// <summary>游戏里的一个遗物。</summary>
    public sealed class RelicInfo
    {
        /// <summary>游戏的 ItemType 编号。</summary>
        public int Id { get; }
        public string Name { get; }
        /// <summary>品质：0~4 对应 D、C、B、A、S。</summary>
        public int Quality { get; }
        /// <summary>图标的 Resources 路径。</summary>
        public string IconPath { get; }
        private Sprite icon;
        private bool iconLoaded;

        internal RelicInfo(int id, string name, int quality, string iconPath)
        {
            Id = id;
            Name = name;
            Quality = quality;
            IconPath = iconPath;
        }

        public Sprite Icon
        {
            get
            {
                if (!iconLoaded)
                {
                    iconLoaded = true;
                    if (!string.IsNullOrEmpty(IconPath)) icon = Resources.Load<Sprite>(IconPath);
                }
                return icon;
            }
        }

        private static List<RelicInfo> all;

        /// <summary>所有遗物（游戏表加载后才有）。</summary>
        public static IReadOnlyList<RelicInfo> All
        {
            get
            {
                if (all == null && GameData.Ready) all = GameRaw.ReadRelics();
                return (IReadOnlyList<RelicInfo>)all ?? new RelicInfo[0];
            }
        }

        public static RelicInfo Get(int id)
        {
            foreach (var r in All)
                if (r.Id == id) return r;
            return null;
        }

        /// <summary>品质名（本地化），quality 不在 0~4 时返回空字符串。</summary>
        public static string QualityName(int quality)
            => quality >= 0 && quality <= 4 ? Game.Localize("quality_" + quality, "DCBAS"[quality].ToString()) : "";
    }

    /// <summary>三选一界面里的一个选项。</summary>
    public sealed class ChoiceOption
    {
        public string Name = "";
        public string Description = "";
        /// <summary>图标的 Resources 路径（例如 RelicInfo.IconPath）。</summary>
        public string IconPath = "";
        /// <summary>0~4 按品质上色（图标也会被染成品质色）；-1 表示不上色。</summary>
        public int Quality = -1;
    }

    /// <summary>
    /// 用游戏自己的肉鸽三选一界面（ui_roguelike，最多 4 个选项）让玩家选。
    /// 游戏的遗物选择也用这个界面：界面忙的时候不要打开，否则游戏那边待选的会被随机选掉。
    /// </summary>
    public static class ChoicePanel
    {
        public const int MaxOptions = 4;

        /// <summary>界面正开着（游戏或别的 Mod 正在让玩家选）。</summary>
        public static bool IsBusy => ModKit.Compatible && GameRaw.ChoiceBusy();

        /// <summary>
        /// 打开界面；界面忙、不在战斗中或选项不合法时返回 false。
        /// onPick 收到选中的下标。之后如果游戏在上面打开了自己的选择，游戏会替这次随机选一个，onPick 照样会被调用。
        /// </summary>
        public static bool TryShow(string title, IList<ChoiceOption> options, Action<int> onPick)
        {
            if (!ModKit.Compatible || !Game.InBattle || IsBusy) return false;
            if (options == null || options.Count == 0 || options.Count > MaxOptions) return false;
            GameRaw.ShowChoice(title, options, i => Safe.Run("ChoicePanel 回调", () => onPick(i)));
            return true;
        }
    }
}
