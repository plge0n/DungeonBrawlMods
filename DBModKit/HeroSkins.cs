using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace DBModKit
{
    /// <summary>一套可以换上的英雄模型：创意工坊里订阅的英雄模型，或者本地皮肤文件夹里的模型。</summary>
    public sealed class HeroSkin
    {
        internal const string WorkshopPrefix = "workshop:";
        internal const string LocalPrefix = "local:";

        /// <summary>存档用的编号："workshop:物品号" 或 "local:文件夹名"。</summary>
        public string Id { get; }
        public int HeroId { get; }
        public string Title { get; }
        public string Author { get; }
        /// <summary>模型所在的文件夹（里面有 manifest.json 和模型包）。</summary>
        public string Folder { get; }
        public bool IsLocal => Id.StartsWith(LocalPrefix, StringComparison.Ordinal);

        private Sprite preview;
        private bool previewLoaded;

        internal HeroSkin(string id, int heroId, string title, string author, string folder)
        {
            Id = id;
            HeroId = heroId;
            Title = title;
            Author = author;
            Folder = folder;
        }

        /// <summary>预览图；没有时为 null。</summary>
        public Sprite Preview
        {
            get
            {
                if (!previewLoaded)
                {
                    previewLoaded = true;
                    Safe.Run($"皮肤预览图 {Id}", () => preview = GameRaw.SkinPreview(Folder));
                }
                return preview;
            }
        }
    }

    /// <summary>
    /// 英雄皮肤：用游戏自带的创意工坊模型替换功能，但每个英雄可以在多套模型之间切换，也可以强制用原版。
    /// 没有选择的英雄按游戏自己的设置（主菜单"工坊 Mod"里启用的模型）。只改本机的显示，不联机同步。
    /// </summary>
    public static class HeroSkins
    {
        /// <summary>选择"原版"时的编号。</summary>
        public const string Original = "original";

        // 英雄编号 → 皮肤编号（Original 表示原版）；没有的按游戏设置
        private static readonly Dictionary<int, string> choices = new Dictionary<int, string>();

        /// <summary>换模型的钩子已安装。为 false 时选择不生效。</summary>
        public static bool Available { get; internal set; }

        /// <summary>
        /// 只给本地玩家换模型。游戏原本按英雄换：在你这边，选了同一个英雄的其他玩家也会换成你的模型。
        /// 默认 false（保持游戏原样）。
        /// </summary>
        public static bool OnlyLocalPlayer { get; set; }

        /// <summary><see cref="OnlyLocalPlayer"/> 需要的钩子已安装。</summary>
        public static bool OnlyLocalPlayerAvailable { get; internal set; }

        /// <summary>本地皮肤文件夹：每个子文件夹放一套模型（格式同创意工坊的英雄模型：manifest.json + 模型包）。</summary>
        public static string LocalRoot => Path.Combine(Paths.BepInExRootPath, "skins");

        /// <summary>
        /// 当前负责这些选择的对象（一般是编辑器插件实例）。热重载时新实例会先于旧实例的清理启动，
        /// 旧实例清理前检查自己还是不是 Owner，不是就别清空。
        /// </summary>
        public static object Owner { get; set; }

        /// <summary>选择变化后触发。</summary>
        public static event Action Changed;

        /// <summary>
        /// 能用的所有皮肤：已安装的工坊英雄模型 + 本地皮肤文件夹。会读盘、查询 Steam，别每帧调用。
        /// </summary>
        public static List<HeroSkin> Scan()
        {
            var list = new List<HeroSkin>();
            if (!ModKit.Compatible) return list;
            Safe.Run("扫描工坊皮肤", () => list.AddRange(GameRaw.ReadWorkshopSkins()));
            Safe.Run("扫描本地皮肤", () =>
            {
                if (!Directory.Exists(LocalRoot)) return;
                foreach (var dir in Directory.GetDirectories(LocalRoot).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    var skin = GameRaw.ReadSkin(HeroSkin.LocalPrefix + Path.GetFileName(dir), dir);
                    if (skin != null) list.Add(skin);
                }
            });
            return list;
        }

        /// <summary>英雄现在的选择：皮肤编号、<see cref="Original"/>，或 null（按游戏设置）。</summary>
        public static string Get(int heroId) => choices.TryGetValue(heroId, out var id) ? id : null;

        /// <summary>选择英雄的皮肤：皮肤编号、<see cref="Original"/>，或 null（按游戏设置）。本地玩家正在用这个英雄时立即换上。</summary>
        public static void Set(int heroId, string skinId)
        {
            if (Get(heroId) == skinId) return;
            if (skinId == null) choices.Remove(heroId);
            else choices[heroId] = skinId;
            Reapply(heroId);
            RaiseChanged();
        }

        /// <summary>清空全部选择并换成 data（加载存档时用）；data 为 null 时只清空。</summary>
        public static void Load(Dictionary<int, string> data)
        {
            var touched = choices.Keys.ToList();
            choices.Clear();
            if (data != null)
                foreach (var kv in data)
                    if (!string.IsNullOrEmpty(kv.Value))
                        choices[kv.Key] = kv.Value;
            foreach (int heroId in touched.Concat(choices.Keys).Distinct().ToList())
                Reapply(heroId);
            RaiseChanged();
        }

        /// <summary>所有选择（英雄编号 → 皮肤编号），用于保存。</summary>
        public static Dictionary<int, string> Snapshot() => new Dictionary<int, string>(choices);

        /// <summary>本地玩家现在用的英雄编号；不在战斗中时为 0。</summary>
        public static int LocalHeroId
        {
            get
            {
                if (!ModKit.Compatible) return 0;
                // 主菜单阶段游戏的管理器可能还没创建，读取本地玩家会抛异常
                try
                {
                    return GameRaw.LocalHeroId();
                }
                catch
                {
                    return 0;
                }
            }
        }

        // 本地玩家正在用这个英雄时，按新的选择重新换模型
        private static void Reapply(int heroId)
        {
            if (!Available || LocalHeroId != heroId) return;
            Safe.Run($"重新换上英雄 {heroId} 的模型", () => GameRaw.ReapplyLocalHeroModel(heroId));
        }

        // 游戏查找英雄模型文件夹时调用。返回 false 表示按游戏原来的逻辑找；
        // 返回 true 时 folder 是要用的文件夹，为 null 表示用原版
        internal static bool TryResolve(int heroId, out string folder)
        {
            folder = null;
            var id = Get(heroId);
            if (id == null) return false;
            if (id == Original) return true;
            folder = FolderOf(id);
            if (folder != null && GameRaw.IsValidSkinFolder(folder, heroId)) return true;
            // 选的皮肤不在了（取消订阅、文件夹被删），按游戏设置
            folder = null;
            return false;
        }

        private static string FolderOf(string id)
        {
            if (id.StartsWith(HeroSkin.WorkshopPrefix, StringComparison.Ordinal))
                return ulong.TryParse(id.Substring(HeroSkin.WorkshopPrefix.Length), out ulong fileId) ? GameRaw.WorkshopFolder(fileId) : null;
            if (id.StartsWith(HeroSkin.LocalPrefix, StringComparison.Ordinal))
            {
                string name = id.Substring(HeroSkin.LocalPrefix.Length);
                // 只认 LocalRoot 下一层的文件夹
                if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..") return null;
                return Path.Combine(LocalRoot, name);
            }
            return null;
        }

        internal static string WorkshopId(ulong fileId) => HeroSkin.WorkshopPrefix + fileId;

        private static void RaiseChanged()
        {
            var evt = Changed;
            if (evt != null) Safe.InvokeEach(evt, nameof(Changed));
        }
    }
}
