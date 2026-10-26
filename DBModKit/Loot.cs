using System.Collections.Generic;
using UnityEngine;

namespace DBModKit
{
    /// <summary>掉落物的大类（按游戏的 ItemType 编号分段）。</summary>
    public enum LootKind
    {
        /// <summary>金币、宝石、金币袋、阳光。</summary>
        Gold,
        /// <summary>力量 / 敏捷 / 耐力 / 全属性之书。</summary>
        AttributeBook,
        /// <summary>主动、被动技能书。</summary>
        SkillBook,
        Relic,
        /// <summary>神符（咆哮、护甲、速度……经验之书也算在这里）。</summary>
        Talisman,
        Card,
        /// <summary>药剂。</summary>
        Potion,
        /// <summary>装备强化材料。</summary>
        Material,
        /// <summary>其他（剧情道具、Boss 掉落的武器、钥匙等）。</summary>
        Other,
    }

    /// <summary>地上的一个掉落物（读取时的快照）。</summary>
    public sealed class LootItem
    {
        /// <summary>这个掉落物的编号（每次掉落都不同）。</summary>
        public uint Id { get; }
        /// <summary>游戏的 ItemType 编号。</summary>
        public int ItemType { get; }
        public LootKind Kind { get; }
        public Vector3 Position { get; }
        /// <summary>自己能捡（公共物品、自己的物品，或物主允许共享）。</summary>
        public bool Pickable { get; }
        /// <summary>捡起后占背包格子（背包 6 格）。</summary>
        public bool UsesBagSlot { get; }
        public string Name => ModKit.Compatible ? GameRaw.ItemName(ItemType) : ItemType.ToString();

        internal LootItem(uint id, int itemType, Vector3 position, bool pickable, bool usesBagSlot, bool talisman)
        {
            Id = id;
            ItemType = itemType;
            Position = position;
            Pickable = pickable;
            UsesBagSlot = usesBagSlot;
            Kind = talisman ? LootKind.Talisman : KindOf(itemType);
        }

        // 编号分段见游戏的 ItemType 枚举
        private static LootKind KindOf(int t)
        {
            if (t >= 1 && t < 1000) return LootKind.Relic;
            if (t >= 1000 && t < 1100) return LootKind.AttributeBook;
            if (t >= 1100 && t < 1300) return LootKind.SkillBook;
            if (t >= 1300 && t < 1400) return LootKind.Gold;
            if (t == 1500 || t == 1506 || t == 1507) return LootKind.Gold; // Pick_Sun（捡到变成金币）、Gold、Gem
            if (t >= 1600 && t < 1700) return LootKind.Material;
            if (t >= 1700 && t < 1800) return LootKind.Potion;
            if (t >= 2000 && t < 10000) return LootKind.Card;
            return LootKind.Other;
        }
    }

    /// <summary>拾取失败的原因。</summary>
    public enum PickResult
    {
        /// <summary>已发出拾取请求（由服务器确认，物品稍后从地上消失）。</summary>
        Requested,
        NotInBattle,
        /// <summary>死亡中。</summary>
        Dead,
        /// <summary>别人的物品。</summary>
        NotYours,
        TooFar,
        BagFull,
    }

    /// <summary>
    /// 地上的掉落物和拾取。只能捡本地玩家身边 <see cref="PickRange"/> 米内的东西，和按 C 键一样
    /// （服务器不检查距离，所以由 ModKit 限制，Mod 不能隔空拾取）。
    /// </summary>
    public static class Loot
    {
        /// <summary>按 C 拾取的范围（bkg.noi）。</summary>
        public const float PickRange = 5f;
        public const int BagSize = 6;

        private static readonly List<LootItem> buffer = new List<LootItem>();

        /// <summary>地上所有的掉落物（含别人的）。不在战斗中时为空。</summary>
        public static List<LootItem> All()
        {
            var result = new List<LootItem>();
            if (!Game.InBattle) return result;
            GameRaw.ReadLoot(buffer);
            result.AddRange(buffer);
            return result;
        }

        /// <summary>本地玩家背包里的物品数。</summary>
        public static int BagCount
        {
            get
            {
                var p = Game.LocalPlayer;
                return p == null ? 0 : GameRaw.BagCount(p.Native);
            }
        }

        /// <summary>游戏设置"护符需按键拾取"开着。关着时护符走到身上就自动捡，按 C 也不捡。</summary>
        public static bool KeyPickTalisman => ModKit.Compatible && GameRaw.KeyPickTalisman();

        /// <summary>
        /// 让本地玩家捡起一个掉落物（和点击地上的物品一样）。只发请求，不播放音效；
        /// 同一个物品在服务器确认之前重复请求是无害的，但最好自己限制频率。
        /// </summary>
        public static PickResult TryPick(LootItem item)
        {
            var p = Game.LocalPlayer;
            if (p == null) return PickResult.NotInBattle;
            if (!GameRaw.CanPickNow(p.Native)) return PickResult.Dead;
            if (!item.Pickable) return PickResult.NotYours;
            if (GameRaw.Distance(p.Native, item.Position) >= PickRange) return PickResult.TooFar;
            if (item.UsesBagSlot && GameRaw.BagCount(p.Native) >= BagSize) return PickResult.BagFull;
            GameRaw.PickItem(p.Native, item.Id);
            return PickResult.Requested;
        }

        /// <summary>游戏的拾取音效。</summary>
        public static void PlayPickSound()
        {
            if (ModKit.Compatible) GameRaw.PlayPickSound();
        }
    }
}
