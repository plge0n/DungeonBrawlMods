using System.Collections.Generic;

namespace DBModKit
{
    /// <summary>大厅里另一个玩家写的一条数据。</summary>
    public readonly struct LobbyEntry
    {
        public ulong SteamId { get; }
        public string Value { get; }

        internal LobbyEntry(ulong steamId, string value)
        {
            SteamId = steamId;
            Value = value;
        }
    }

    /// <summary>
    /// 通过 Steam 大厅成员数据和其他装了 Mod 的玩家交换少量文字数据。每个玩家只能写自己的一条，所有人都能读。
    /// 只有 Steam 联机有大厅（进地牢后也不离开）；局域网模式和单机没有，此时读写都什么也不做。
    /// <list type="bullet">
    /// <item>不用自定义 Mirror 消息：没装 Mod 的玩家收到不认识的消息会断开连接；成员数据游戏自己不用，没装 Mod 的人不受影响。</item>
    /// <item>别人写的内容不可信，解析时要检查格式和范围，不能拿来改游戏。</item>
    /// <item>值最长约 8KB；Steam 会把每次修改广播给大厅所有人，不要每帧写，隔一两秒写一次。</item>
    /// <item>键用 "dbmods.&lt;mod 名&gt;" 之类的前缀，避免和别的 Mod 撞。</item>
    /// </list>
    /// </summary>
    public static class LobbySync
    {
        private static readonly Dictionary<string, (ulong lobby, string value)> published = new Dictionary<string, (ulong, string)>();

        /// <summary>当前大厅（0 = 不在大厅里）。</summary>
        private static ulong Lobby => ModKit.Compatible ? GameRaw.LobbyId() : 0UL;

        /// <summary>在 Steam 大厅里，可以和别人交换数据。</summary>
        public static bool InLobby => Lobby != 0;

        /// <summary>写自己的数据（和上次写的一样时不重复发送）。value 为空字符串表示清除。</summary>
        public static void Publish(string key, string value)
        {
            value = value ?? "";
            ulong lobby = Lobby;
            if (published.TryGetValue(key, out var last) && last.lobby != lobby && last.lobby != 0 && last.value != "")
            {
                // 换了大厅时旧大厅已经离开，写不了也无所谓
                ulong old = last.lobby;
                Safe.Run("清除大厅数据", () => GameRaw.TryClearLobbyMemberData(old, key));
            }
            if (lobby == 0)
            {
                published.Remove(key);
                return;
            }
            if (published.TryGetValue(key, out last) && last.lobby == lobby && last.value == value) return;
            GameRaw.SetLobbyMemberData(lobby, key, value);
            published[key] = (lobby, value);
        }

        /// <summary>清除自己写过的数据（Mod 卸载、关掉功能时调用）。</summary>
        public static void Clear(string key) => Publish(key, "");

        /// <summary>房主写的数据。自己是房主、不在大厅里、或者房主没写时为 null。</summary>
        public static string ReadHost(string key)
        {
            ulong lobby = Lobby;
            if (lobby == 0) return null;
            ulong owner = GameRaw.LobbyOwner(lobby);
            if (owner == 0 || owner == GameRaw.MySteamId()) return null;
            string value = GameRaw.LobbyMemberData(lobby, owner, key);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>其他玩家写的数据（不含自己，不含空值）。不在大厅里时为空。</summary>
        public static List<LobbyEntry> Read(string key)
        {
            var result = new List<LobbyEntry>();
            ulong lobby = Lobby;
            if (lobby == 0) return result;
            ulong me = GameRaw.MySteamId();
            foreach (ulong member in GameRaw.LobbyMembers(lobby))
            {
                if (member == me) continue;
                string value = GameRaw.LobbyMemberData(lobby, member, key);
                if (!string.IsNullOrEmpty(value)) result.Add(new LobbyEntry(member, value));
            }
            return result;
        }
    }
}
