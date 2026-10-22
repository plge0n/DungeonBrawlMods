using System.Collections.Generic;
using DBModKit;

namespace DBStudio
{
    // 英雄皮肤的选择，存在 BepInEx/config/DBStudio/skins.json
    internal sealed class SkinSettings
    {
        // 选了同一个英雄的其他玩家保持他们原来的样子，只换自己
        public bool OnlyLocalPlayer = true;
        // 英雄编号 → 皮肤编号（"workshop:物品号" / "local:文件夹名" / "original"）；没有的按游戏设置
        public Dictionary<int, string> Choices = new Dictionary<int, string>();
    }

    // 皮肤只改本机的显示，不影响玩法，所以联机时也照常生效（不像玩法包）
    internal static class Skins
    {
        private static JsonConfig<SkinSettings> config;

        public static SkinSettings Current => config.Data;

        // 上次扫描到的皮肤；扫描要读盘、查 Steam，只在打开皮肤页和点刷新时做
        public static List<HeroSkin> All { get; private set; } = new List<HeroSkin>();

        public static void Init(ModContext mod)
        {
            config = mod.LoadJson<SkinSettings>("DBStudio/skins.json");
            var owner = new object();
            HeroSkins.Owner = owner;
            HeroSkins.OnlyLocalPlayer = Current.OnlyLocalPlayer;
            HeroSkins.Load(Current.Choices);
            HeroSkins.Changed += Save;
            // 卸载时恢复游戏原来的行为；热重载时新实例已经接管（Owner 变了），这时不能清空
            mod.AddCleanup(() =>
            {
                HeroSkins.Changed -= Save;
                if (HeroSkins.Owner != owner) return;
                HeroSkins.Owner = null;
                HeroSkins.OnlyLocalPlayer = false;
                HeroSkins.Load(null);
            });
        }

        public static void Rescan() => All = HeroSkins.Scan();

        public static void SetOnlyLocalPlayer(bool value)
        {
            if (Current.OnlyLocalPlayer == value) return;
            Current.OnlyLocalPlayer = HeroSkins.OnlyLocalPlayer = value;
            config.MarkDirty();
        }

        private static void Save()
        {
            Current.Choices = HeroSkins.Snapshot();
            config.MarkDirty();
        }
    }
}
