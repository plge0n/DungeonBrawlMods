using System.Collections.Generic;
using System.Linq;
using DBModKit;

namespace DBStudio
{
    // 英雄皮肤的选择，存在 BepInEx/config/DBStudio/skins.json
    internal sealed class SkinSettings
    {
        // 选了同一个英雄的其他玩家保持他们原来的样子，只换自己
        public bool OnlyLocalPlayer = true;
        // Steam 联机时，房间里也装了 Mod 的玩家用的工坊皮肤，你也订阅了就在你这边显示
        public bool ShowOtherPlayers = true;
        // 英雄编号 → 皮肤编号（"workshop:物品号" / "local:文件夹名" / "original"）；没有的按游戏设置
        public Dictionary<int, string> Choices = new Dictionary<int, string>();
    }

    // 皮肤只改显示，不影响玩法，所以联机时也照常生效（不像玩法包）。
    // Steam 联机时通过大厅成员数据和也装了 Mod 的玩家互相告知用的皮肤，没装 Mod 的人不受影响
    internal static class Skins
    {
        private static JsonConfig<SkinSettings> config;

        public static SkinSettings Current => config.Data;

        // 上次扫描到的皮肤；扫描要读盘、查 Steam，只在皮肤页打开期间定时做
        public static List<HeroSkin> All { get; private set; } = new List<HeroSkin>();

        public static void Init(ModContext mod)
        {
            config = mod.LoadJson<SkinSettings>("DBStudio/skins.json");
            var owner = new object();
            HeroSkins.Owner = owner;
            HeroSkins.OnlyLocalPlayer = Current.OnlyLocalPlayer;
            HeroSkins.ShowOtherPlayers = Current.ShowOtherPlayers;
            HeroSkins.SyncInLobby = true;
            HeroSkins.Load(Current.Choices);
            HeroSkins.Changed += Save;
            // 卸载时恢复游戏原来的行为；热重载时新实例已经接管（Owner 变了），这时不能清空
            mod.AddCleanup(() =>
            {
                HeroSkins.Changed -= Save;
                if (HeroSkins.Owner != owner) return;
                HeroSkins.Owner = null;
                HeroSkins.OnlyLocalPlayer = false;
                HeroSkins.ShowOtherPlayers = false;
                HeroSkins.SyncInLobby = false;
                HeroSkins.Load(null);
            });
        }

        // 返回列表有没有变化；没变时保留原来的对象（预览图已经加载过）
        public static bool Rescan()
        {
            var found = HeroSkins.Scan();
            if (found.Select(s => s.Id).SequenceEqual(All.Select(s => s.Id))) return false;
            All = found;
            return true;
        }

        public static void SetOnlyLocalPlayer(bool value)
        {
            if (Current.OnlyLocalPlayer == value) return;
            Current.OnlyLocalPlayer = HeroSkins.OnlyLocalPlayer = value;
            config.MarkDirty();
        }

        public static void SetShowOtherPlayers(bool value)
        {
            if (Current.ShowOtherPlayers == value) return;
            Current.ShowOtherPlayers = HeroSkins.ShowOtherPlayers = value;
            config.MarkDirty();
        }

        private static void Save()
        {
            Current.Choices = HeroSkins.Snapshot();
            config.MarkDirty();
        }
    }
}
