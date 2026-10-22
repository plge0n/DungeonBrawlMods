using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DBModKit
{
    /// <summary>联机时改玩法的 Mod 内容能不能用，见 <see cref="Multiplayer.Policy"/>。</summary>
    public enum RoomPolicy
    {
        /// <summary>房间里只有自己。</summary>
        Solo,
        /// <summary>Steam 大厅里每个人都装了同样版本的 ModKit 和同样的改玩法内容（Mod 版本、玩法包都一样）。</summary>
        AllHaveSame,
        /// <summary>有人装的不一样，但房主允许了；装了 ModKit 的人都会收到提示。</summary>
        HostAllowed,
        /// <summary>有人没装或装的不一样，房主也没允许。局域网联机没有大厅、确认不了，也是这个。</summary>
        Blocked,
    }

    /// <summary>
    /// 联机握手：每个装了 ModKit 的玩家通过 Steam 大厅成员数据（<see cref="LobbySync"/> 同一个通道）公布自己的 ModKit 版本和改玩法的内容，
    /// 据此决定本机改玩法的内容能不能用（<see cref="GameplayAllowed"/>）。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>改玩法的内容：自定义被动和主动技能、改了属性或专属技能的变体英雄，以及用 <see cref="ModContext.DeclareGameplay"/> 声明过的 Mod（比如 DBStudio 的玩法包）。
    /// 只换外观的（皮肤、变体的模型和名字）不受限制。</item>
    /// <item>进入战斗时锁定，本局只会降级（有人中途进房、房主取消）不会升级。</item>
    /// <item>握手结果只决定本机启不启用，不会往别人那边发任何东西；没装 ModKit 的人收不到提示。</item>
    /// <item>别人公布的内容可以造假，造假的后果由对方自己承担；排行榜不受影响（装了 ModKit 一律不上传）。</item>
    /// </list>
    /// </remarks>
    public static class Multiplayer
    {
        private const string Key = "dbmods.mods";
        private const string Format = "1";
        private const float Interval = 1f;
        private const int MaxEntries = 64;

        // 启动成功的 Mod：GUID → 名字、版本
        private static readonly Dictionary<string, (string name, string version)> mods = new Dictionary<string, (string, string)>();
        // DeclareGameplay 声明的：GUID → 指纹（可以为空）
        private static readonly Dictionary<string, string> declared = new Dictionary<string, string>();

        private static float next;
        private static bool wasSolo = true;
        private static RoomPolicy room = RoomPolicy.Blocked;
        private static string roomStatus = "";
        private static RoomPolicy? locked;
        // 房主勾选"允许"时所在的大厅；换了大厅自动失效
        private static ulong allowLobby;

        /// <summary>当前策略（战斗中是进入战斗时锁定的结果，只会降级）。</summary>
        public static RoomPolicy Policy { get; private set; } = RoomPolicy.Blocked;

        /// <summary>改玩法的内容可以用（单人、所有人装的一样、或房主允许）。</summary>
        public static bool GameplayAllowed => Policy != RoomPolicy.Blocked;

        /// <summary>在战斗中，策略已锁定。</summary>
        public static bool Locked => locked.HasValue;

        /// <summary>给玩家看的说明，比如"联机：1 人没装 ModKit"。</summary>
        public static string Status { get; private set; } = "";

        /// <summary>本机改玩法的内容，"GUID@版本" 或 "GUID@版本#指纹"，已排序。</summary>
        public static IReadOnlyList<string> Signature { get; private set; } = new string[0];

        /// <summary>自己是 Steam 大厅的房主（只有房主能 <see cref="HostAllow"/>）。</summary>
        public static bool IsLobbyOwner
        {
            get
            {
                ulong lobby = ModKit.Compatible ? GameRaw.LobbyId() : 0UL;
                return lobby != 0 && GameRaw.LobbyOwner(lobby) == GameRaw.MySteamId();
            }
        }

        /// <summary>
        /// 房主允许这个房间使用改玩法的内容（即使有人装的不一样）。只有房主、而且不在战斗中时能改；换了房间自动取消。
        /// </summary>
        public static bool HostAllow
        {
            get => allowLobby != 0 && ModKit.Compatible && allowLobby == GameRaw.LobbyId();
            set
            {
                if (Locked || !IsLobbyOwner || value == HostAllow) return;
                allowLobby = value ? GameRaw.LobbyId() : 0UL;
                RunLog.WriteCore(ModKitPlugin.Name, "联机", RunLogLevel.Info, value ? "房主允许改玩法的内容" : "房主取消允许", null, mirror: false);
                next = 0f;
            }
        }

        /// <summary>策略变了。</summary>
        public static event Action Changed;

        internal static void AddMod(string guid, string name, string version) => mods[guid] = (name, version);

        internal static void RemoveMod(string guid)
        {
            mods.Remove(guid);
            declared.Remove(guid);
        }

        internal static void Declare(string guid, string fingerprint)
        {
            fingerprint = fingerprint ?? "";
            if (fingerprint.Any(c => !IsSafe(c))) throw new ArgumentException($"指纹只能用字母、数字和 . _ - +：\"{fingerprint}\"");
            if (declared.TryGetValue(guid, out var old) && old == fingerprint) return;
            declared[guid] = fingerprint;
            next = 0f;
        }

        internal static void Retract(string guid)
        {
            if (declared.Remove(guid)) next = 0f;
        }

        // Runner 每帧调用，在 ModEvents.Update 之前，Mod 读到的总是这一帧的结果
        internal static void Tick()
        {
            bool solo = Game.IsSolo;
            // 有人进出房间时立即重新检查，不等下一秒
            if (solo != wasSolo)
            {
                wasSolo = solo;
                next = 0f;
            }
            if (Time.unscaledTime >= next)
            {
                next = Time.unscaledTime + Interval;
                Refresh(solo);
            }
            var want = solo ? RoomPolicy.Solo : room;
            if (locked.HasValue)
            {
                if (want == RoomPolicy.Blocked) locked = RoomPolicy.Blocked;
                if (locked == RoomPolicy.Blocked) want = RoomPolicy.Blocked;
            }
            Status = (solo ? "单人" : roomStatus) + (locked.HasValue ? "（本局已锁定）" : "");
            if (want == Policy) return;
            var old = Policy;
            Policy = want;
            RunLog.WriteCore(ModKitPlugin.Name, "联机", RunLogLevel.Info, "策略", new (string, object)[] { ("策略", want), ("原来", old), ("说明", Status) }, mirror: false);
            if (Signature.Count > 0 || want == RoomPolicy.HostAllowed)
            {
                if (want == RoomPolicy.HostAllowed) ScreenNotice.Show("房主允许了这个房间使用改玩法的 Mod 内容（不是每个人都装了同样的 Mod）。", 8f);
                else if (want == RoomPolicy.Blocked && locked.HasValue) ScreenNotice.Show("有人进了房间或房主取消了允许，改玩法的 Mod 内容本局停用。", 8f);
            }
            var evt = Changed;
            if (evt != null) Safe.InvokeEach(evt, nameof(Changed));
        }

        // 进入战斗时锁定（Runner 在 ModEvents.BattleStarted 之前调用）
        internal static void Lock()
        {
            locked = Policy;
            RunLog.WriteCore(ModKitPlugin.Name, "联机", RunLogLevel.Info, "进入战斗，锁定策略", new (string, object)[] { ("策略", Policy), ("说明", Status), ("内容", string.Join("，", Signature)) }, mirror: false);
            if (Signature.Count == 0 && Policy != RoomPolicy.HostAllowed) return;
            switch (Policy)
            {
                case RoomPolicy.AllHaveSame:
                    ScreenNotice.Show("联机：房间里每个人都装了同样的 Mod，改玩法的内容本局可用。", 6f);
                    break;
                case RoomPolicy.HostAllowed:
                    ScreenNotice.Show("房主允许本局使用改玩法的 Mod 内容（不是每个人都装了同样的 Mod）。", 8f);
                    break;
                case RoomPolicy.Blocked:
                    ScreenNotice.Show($"{roomStatus}。改玩法的 Mod 内容（自定义技能、变体属性、玩法包等）本局停用，外观照常。", 10f);
                    break;
            }
        }

        internal static void Unlock() => locked = null;

        private static void Refresh(bool solo)
        {
            Signature = BuildSignature();
            ulong lobby = GameRaw.LobbyId();
            if (lobby == 0)
            {
                LobbySync.Publish(Key, "");
                room = RoomPolicy.Blocked;
                roomStatus = "局域网联机：没有 Steam 大厅，确认不了别人装了什么";
                return;
            }
            ulong me = GameRaw.MySteamId(), owner = GameRaw.LobbyOwner(lobby);
            bool allow = HostAllow;
            LobbySync.Publish(Key, $"{Format}|{ModKit.Version}|{(owner == me && allow ? 1 : 0)}|{string.Join(";", Signature)}");
            if (solo) return;

            var mine = new HashSet<string>(Signature);
            int same = 0, noKit = 0, different = 0;
            bool hostAllow = owner == me && allow;
            var diffs = new SortedSet<string>(StringComparer.Ordinal);
            foreach (ulong member in GameRaw.LobbyMembers(lobby))
            {
                if (member == me) continue;
                if (!TryParse(GameRaw.LobbyMemberData(lobby, member, Key), out string kit, out bool memberAllow, out var theirs))
                {
                    noKit++;
                    continue;
                }
                if (member == owner && memberAllow) hostAllow = true;
                if (kit == ModKit.Version && theirs.SetEquals(mine))
                {
                    same++;
                    continue;
                }
                different++;
                if (kit != ModKit.Version) diffs.Add($"ModKit {kit}");
                Describe(mine, theirs, diffs);
            }
            var parts = new List<string>();
            if (noKit > 0) parts.Add($"{noKit} 人没装 ModKit");
            if (different > 0) parts.Add($"{different} 人装的不一样（{string.Join("、", diffs)}）");
            if (parts.Count == 0)
            {
                room = RoomPolicy.AllHaveSame;
                roomStatus = $"联机：{same + 1} 人都装了同样的内容";
            }
            else
            {
                room = hostAllow ? RoomPolicy.HostAllowed : RoomPolicy.Blocked;
                roomStatus = "联机：" + string.Join("，", parts) + (hostAllow ? "，房主已允许" : "");
            }
        }

        private static List<string> BuildSignature()
        {
            var owners = new HashSet<string>(declared.Keys);
            foreach (var p in PassiveSkills.All) owners.Add(p.Owner);
            foreach (var s in ActiveSkills.All) owners.Add(s.Owner);
            foreach (var v in HeroVariants.All)
                if (v.ChangesGameplay) owners.Add(v.Owner);
            var list = new List<string>();
            foreach (string guid in owners)
            {
                string version = mods.TryGetValue(guid, out var m) ? m.version : "?";
                string entry = guid + "@" + version;
                if (declared.TryGetValue(guid, out var fp) && fp != "") entry += "#" + fp;
                if (entry.All(IsSafeEntry)) list.Add(entry);
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        // 别人写的数据不可信：格式、长度、字符都要检查
        private static bool TryParse(string value, out string kit, out bool allow, out HashSet<string> entries)
        {
            kit = null;
            allow = false;
            entries = null;
            var parts = (value ?? "").Split('|');
            if (parts.Length != 4 || parts[0] != Format || parts[1].Length == 0 || parts[1].Length > 20 || !parts[1].All(IsSafe)) return false;
            if (parts[2] != "0" && parts[2] != "1") return false;
            var list = parts[3].Length == 0 ? new string[0] : parts[3].Split(';');
            if (list.Length > MaxEntries || list.Any(e => e.Length == 0 || e.Length > 200 || !e.All(IsSafeEntry) || e.IndexOf('@') <= 0)) return false;
            kit = parts[1];
            allow = parts[2] == "1";
            entries = new HashSet<string>(list);
            return true;
        }

        private static bool IsSafe(char c) => c < 128 && (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' || c == '+');

        private static bool IsSafeEntry(char c) => IsSafe(c) || c == '@' || c == '#';

        // 按 GUID 比较：缺了哪些、多了哪些、哪些版本或玩法包不同
        private static void Describe(HashSet<string> mine, HashSet<string> theirs, SortedSet<string> output)
        {
            string Guid(string e) => e.Substring(0, e.IndexOf('@'));
            var a = mine.ToDictionary(Guid, e => e);
            var b = new Dictionary<string, string>();
            foreach (var e in theirs) b[Guid(e)] = e;
            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var other)) output.Add("缺 " + NameOf(kv.Key));
                else if (other != kv.Value) output.Add(NameOf(kv.Key) + " 版本或玩法包不同");
            }
            foreach (var guid in b.Keys.Where(g => !a.ContainsKey(g))) output.Add("多了 " + NameOf(guid));
        }

        private static string NameOf(string guid) => mods.TryGetValue(guid, out var m) ? m.name : guid;
    }

    // DBStudio Mod 页 → DBModKit 里的"联机"一栏
    internal static class MultiplayerSettings
    {
        private static Text status;
        private static Toggle allow;

        public static void Register() => ModSettings.Register(ModKitPlugin.Guid, "联机", Build, Refresh);

        private static void Build(RectTransform container)
        {
            NativeUi.Label(container,
                "改玩法的 Mod 内容（自定义技能、变体英雄的属性、DBStudio 玩法包等）联机时只在房间里每个人都装了同样的 Mod 时生效；" +
                "房主也可以手动允许，装了 ModKit 的人都会收到提示。进入地牢后本局不再放宽。只换外观的内容不受影响。", 16, -1f, NativeUi.DimText);
            status = NativeUi.Label(container, "", 18);
            allow = NativeUi.Toggle(container, "我是房主：在这个房间里允许改玩法的内容（即使有人装的不一样）", 900f, v => Multiplayer.HostAllow = v);
            Refresh();
        }

        private static void Refresh()
        {
            if (status == null) return;
            string mine = Multiplayer.Signature.Count == 0 ? "没有" : string.Join("、", Multiplayer.Signature);
            string state = Multiplayer.GameplayAllowed ? "<color=#9fd08a>可用</color>" : "<color=#e07a5f>停用</color>";
            status.text = $"{Multiplayer.Status}  ·  改玩法的内容{state}\n<size=16><color=#a6a6a6>本机改玩法的内容：{mine}</color></size>";
            allow.SetIsOnWithoutNotify(Multiplayer.HostAllow);
            allow.interactable = Multiplayer.IsLobbyOwner && !Multiplayer.Locked;
        }
    }
}
