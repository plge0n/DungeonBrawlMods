using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBModKit
{
    public enum LoadoutKind { Active, Passive, Equipment, Relic }

    /// <summary>游戏当前配置字典中的条目，包含 Mod 注册的内容。</summary>
    public sealed class LoadoutEntry
    {
        public LoadoutKind Kind { get; }
        public int Id { get; }
        public string Name { get; }
        public string IconPath { get; }
        public Sprite Icon => string.IsNullOrEmpty(IconPath) ? null : Resources.Load<Sprite>(IconPath);

        internal LoadoutEntry(LoadoutKind kind, int id, string name, string iconPath)
        {
            Kind = kind;
            Id = id;
            Name = name;
            IconPath = iconPath;
        }
    }

    /// <summary>本地玩家的一件物品。句柄只对应这一个实例，移除或换局后失效。</summary>
    public sealed class LoadoutItem
    {
        public LoadoutEntry Entry { get; }
        public int Slot { get; }
        public int Level { get; }
        /// <summary>-1 表示游戏未规定上限。被动暂不支持修改等级。</summary>
        public int MaxLevel { get; }
        public bool CanUpgrade { get; }
        internal object Native { get; }
        internal Component Owner { get; }

        internal LoadoutItem(LoadoutEntry entry, int slot, int level, int maxLevel, bool canUpgrade, object native, Component owner)
        {
            Entry = entry;
            Slot = slot;
            Level = level;
            MaxLevel = maxLevel;
            CanUpgrade = canUpgrade;
            Native = native;
            Owner = owner;
        }

        public override bool Equals(object obj) => obj is LoadoutItem other && ReferenceEquals(Native, other.Native)
            && Owner == other.Owner && Slot == other.Slot && Level == other.Level && MaxLevel == other.MaxLevel
            && CanUpgrade == other.CanUpgrade && Entry.Kind == other.Entry.Kind && Entry.Id == other.Entry.Id
            && Entry.Name == other.Entry.Name && Entry.IconPath == other.Entry.IconPath;
        public override int GetHashCode() => Native?.GetHashCode() ?? 0;
    }

    /// <summary>局内构筑编辑。所有修改都重新检查单人和本地玩家，不写存档。</summary>
    public static class Loadout
    {
        public static bool CanEdit => ModKit.Compatible && Game.InBattle && Game.IsSolo;
        public static string LastError { get; private set; }

        /// <summary>主动和被动共用格数，装备单独计数。局外返回 0。</summary>
        public static int Capacity(LoadoutKind kind)
        {
            try { return ModKit.Compatible && Game.InBattle ? GameRaw.LoadoutCapacity(kind) : 0; }
            catch (Exception e) { Fail(e.Message); return 0; }
        }

        public static IReadOnlyList<LoadoutEntry> All(LoadoutKind kind)
        {
            try { return ModKit.Compatible && GameRaw.TablesReady() ? GameRaw.LoadoutEntries(kind) : new List<LoadoutEntry>(); }
            catch (Exception e) { Fail(e.Message); return new LoadoutEntry[0]; }
        }

        public static IReadOnlyList<LoadoutItem> Current(LoadoutKind kind)
        {
            try { return ModKit.Compatible && Game.InBattle ? GameRaw.LoadoutCurrent(kind) : new List<LoadoutItem>(); }
            catch (Exception e) { Fail(e.Message); return new LoadoutItem[0]; }
        }

        public static bool Add(LoadoutKind kind, int id) => Change(() => GameRaw.LoadoutAdd(kind, id, null));
        public static bool Replace(LoadoutItem item, int id) => Change(() => GameRaw.LoadoutAdd(LoadoutKind.Active, id, item));
        public static bool Remove(LoadoutItem item) => Change(() => GameRaw.LoadoutRemove(item));
        public static bool SetLevel(LoadoutItem item, int level) => Change(() => GameRaw.LoadoutSetLevel(item, level));

        private static bool Change(Func<bool> action)
        {
            LastError = null;
            try
            {
                if (!CanEdit) return Fail("仅限单人战斗中的本地玩家");
                if (!action()) return Fail("操作未完成。技能栏或装备栏可能已满、条目已失效，或游戏限制了此次操作");
                return true;
            }
            catch (Exception e) { return Fail(e.ToString()); }
        }

        private static bool Fail(string message)
        {
            LastError = message;
            RunLog.Warning(ModKitPlugin.Name, "构筑", message);
            return false;
        }
    }
}
