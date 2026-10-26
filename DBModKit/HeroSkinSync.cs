using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DBModKit
{
    // 通过 Steam 大厅成员数据同步皮肤。每个装了 Mod 的玩家写自己的一条：
    //   "1|角色 netId|英雄编号|皮肤编号"（皮肤编号是 "workshop:物品号" 或 "original"；不在战斗中时写空字符串）
    // 用 netId 而不是 Steam 昵称找角色：昵称可能重名，netId 在同一局里每个客户端看到的都一样。
    // 不能用自定义的 Mirror 消息：没装 Mod 的一方收到不认识的消息会断开连接。
    internal static class HeroSkinSync
    {
        private const string Key = "dbmods.heroskin";
        private const string Format = "1";
        private const float Interval = 1f;
        // 读 manifest 判断皮肤装没装、自己公布什么，结果缓存这么久（也用来发现对局中途订阅完成的皮肤）
        private const float CacheInterval = 5f;

        private struct Entry
        {
            public int HeroId;
            public string SkinId;
            // 你本机也装了这套皮肤（原版总是 true）
            public bool Installed;
        }

        private static float next, cacheUntil;
        private static ulong publishedLobby;
        private static string publishedValue = "";
        private static int sharedHero;
        private static string sharedSkin;
        private static readonly Dictionary<(string skin, int hero), bool> installed = new Dictionary<(string, int), bool>();
        // 角色 netId → 对方公布的皮肤
        private static Dictionary<uint, Entry> entries = new Dictionary<uint, Entry>();
        // 要重新换模型的角色。对方的角色还没生成时不用管，生成时（bby.lll）会按 entries 换
        private static readonly HashSet<uint> pending = new HashSet<uint>();

        internal static List<RemoteSkin> Others { get; private set; } = new List<RemoteSkin>();
        internal static int Version { get; private set; }

        internal static bool TryGetSkin(Component owner, int heroId, out string skinId)
        {
            skinId = null;
            if (!entries.TryGetValue(GameRaw.NetId(owner), out var e) || e.HeroId != heroId) return false;
            skinId = e.SkinId;
            return true;
        }

        // 显示其他玩家皮肤的开关变化时，公布过皮肤的角色都要重新换
        internal static void ReapplyAll() => pending.UnionWith(entries.Keys);

        // 自己的选择变了，下次立即重新算要公布的皮肤
        internal static void InvalidateShared() => sharedHero = 0;

        internal static void Tick()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Interval;
            if (Time.unscaledTime >= cacheUntil)
            {
                cacheUntil = Time.unscaledTime + CacheInterval;
                installed.Clear();
                sharedHero = 0;
            }

            ulong lobby = HeroSkins.SyncInLobby && HeroSkins.Available ? GameRaw.LobbyId() : 0UL;
            if (lobby != publishedLobby && publishedLobby != 0 && publishedValue != "")
            {
                // 关掉同步时清掉自己写过的数据；换了大厅时旧大厅已经离开，写不了也无所谓
                ulong old = publishedLobby;
                Safe.Run("清除皮肤同步数据", () => GameRaw.TryClearLobbyMemberData(old, Key));
            }
            if (lobby == 0)
            {
                publishedLobby = 0;
                publishedValue = "";
                SetEntries(new Dictionary<uint, Entry>());
            }
            else
            {
                Publish(lobby);
                Read(lobby);
            }
            // 离开大厅、关掉同步时也要执行：已经换上的队友皮肤按本机设置还原
            if (pending.Count == 0 && entries.Count == 0 && Others.Count == 0) return;
            var players = GameRaw.AllPlayers();
            ApplyPending(players);
            UpdateOthers(players);
        }

        private static void Publish(ulong lobby)
        {
            var me = GameRaw.LocalPlayer();
            string value = "";
            if (me != null)
            {
                int heroId = GameRaw.HeroIdOf(me);
                if (sharedHero != heroId)
                {
                    sharedHero = heroId;
                    sharedSkin = HeroSkins.ShareableSkin(heroId);
                }
                if (sharedSkin != null) value = $"{Format}|{GameRaw.NetId(me)}|{heroId}|{sharedSkin}";
            }
            if (lobby == publishedLobby && value == publishedValue) return;
            GameRaw.SetLobbyMemberData(lobby, Key, value);
            publishedLobby = lobby;
            publishedValue = value;
        }

        private static void Read(ulong lobby)
        {
            ulong me = GameRaw.MySteamId();
            var found = new Dictionary<uint, Entry>();
            // 别人写的数据不可信：同一个角色被多个成员声明时都不认
            var claimed = new HashSet<uint>();
            var conflicts = new HashSet<uint>();
            foreach (ulong member in GameRaw.LobbyMembers(lobby))
            {
                if (member == me) continue;
                if (!TryParse(GameRaw.LobbyMemberData(lobby, member, Key), out uint netId, out var entry)) continue;
                if (!claimed.Add(netId)) conflicts.Add(netId);
                entry.Installed = IsInstalled(entry.SkinId, entry.HeroId);
                found[netId] = entry;
            }
            foreach (uint id in conflicts) found.Remove(id);
            SetEntries(found);
        }

        // 只认工坊皮肤和原版：别人的数据不能指向本机任意文件夹
        private static bool TryParse(string value, out uint netId, out Entry entry)
        {
            netId = 0;
            entry = default;
            var parts = (value ?? "").Split('|');
            if (parts.Length != 4 || parts[0] != Format) return false;
            if (!uint.TryParse(parts[1], out netId) || !int.TryParse(parts[2], out int heroId) || heroId <= 0) return false;
            string skin = parts[3];
            if (skin != HeroSkins.Original && HeroSkins.WorkshopFileId(skin) == 0) return false;
            entry = new Entry { HeroId = heroId, SkinId = skin };
            return true;
        }

        private static bool IsInstalled(string skin, int heroId)
        {
            if (!installed.TryGetValue((skin, heroId), out bool ok))
                installed[(skin, heroId)] = ok = HeroSkins.IsInstalled(skin, heroId);
            return ok;
        }

        // 英雄、皮肤或本机是否装了这套皮肤（对局中途订阅完成）有变化的角色都要重新换
        private static void SetEntries(Dictionary<uint, Entry> found)
        {
            foreach (var id in entries.Keys.Union(found.Keys))
            {
                bool had = entries.TryGetValue(id, out var a), has = found.TryGetValue(id, out var b);
                if (had != has || a.HeroId != b.HeroId || a.SkinId != b.SkinId || a.Installed != b.Installed) pending.Add(id);
            }
            entries = found;
        }

        private static void ApplyPending(IEnumerable<Component> players)
        {
            if (pending.Count == 0) return;
            // 变身中换模型会换到变身后的模型上，等变回来再换
            var transformed = new HashSet<uint>();
            foreach (var p in players)
            {
                if (p == null || GameRaw.IsLocalPlayer(p)) continue;
                uint id = GameRaw.NetId(p);
                if (!pending.Contains(id)) continue;
                if (GameRaw.IsTransformed(p)) transformed.Add(id);
                else Safe.Run("给其他玩家换皮肤", () => GameRaw.ReapplyHeroModel(p));
            }
            // 没找到的是还没生成（或已经离开）的角色，生成时会按 entries 换
            pending.IntersectWith(transformed);
        }

        private static void UpdateOthers(IEnumerable<Component> players)
        {
            var names = new Dictionary<uint, string>();
            foreach (var p in players)
                if (p != null) names[GameRaw.NetId(p)] = GameRaw.PlayerName(p);
            var list = entries.Select(kv => new RemoteSkin(
                    names.TryGetValue(kv.Key, out var name) && !string.IsNullOrEmpty(name) ? name : "玩家",
                    kv.Value.HeroId, kv.Value.SkinId, HeroSkins.WorkshopFileId(kv.Value.SkinId), kv.Value.Installed))
                .OrderBy(r => r.PlayerName, StringComparer.Ordinal)
                .ToList();
            if (list.Select(r => r.Signature).SequenceEqual(Others.Select(r => r.Signature))) return;
            Others = list;
            Version++;
        }
    }
}
