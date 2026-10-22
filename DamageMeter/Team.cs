using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;

namespace DamageMeter
{
    // 队伍里一个玩家的伤害
    internal sealed class TeamMember
    {
        public string Name;
        public bool IsMe;
        // 没收到这个人的数据（没装 DamageMeter、关了共享，或者局域网模式）
        public bool HasData;
        public long Damage;
        public float Seconds;
        public string Top;

        public float Dps => Damage / Mathf.Max(1f, Seconds);
    }

    // Steam 联机时，通过大厅成员数据和装了 DamageMeter 的队友交换伤害统计。每人写自己的一条：
    //   "1|角色 netId|波次|本波伤害|本波秒数|本波最高来源|本局伤害|本局秒数|本局最高来源"
    // 伤害只能在出手方的客户端算出来，所以队友的数据只能由队友自己发
    internal static class Team
    {
        private const string Key = "dbmods.damagemeter";
        private const string Format = "1";
        private const float Interval = 2f;
        private const int MaxNameLength = 12;

        private struct Remote
        {
            public int Wave;
            public long WaveDamage, RunDamage;
            public int WaveSeconds, RunSeconds;
            public string WaveTop, RunTop;
        }

        private static float next;
        // 场上其他玩家的 netId → 名字
        private static readonly Dictionary<uint, string> others = new Dictionary<uint, string>();
        private static Dictionary<uint, Remote> remotes = new Dictionary<uint, Remote>();

        /// <summary>场上有别的玩家。</summary>
        public static bool HasTeammates => others.Count > 0;

        public static void Tick()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Interval;

            var me = Game.LocalPlayer;
            others.Clear();
            if (me == null)
            {
                remotes.Clear();
                LobbySync.Clear(Key);
                return;
            }
            foreach (var p in Game.Players())
                if (p != me) others[p.NetId] = Clean(string.IsNullOrEmpty(p.Name) ? p.HeroName : p.Name);

            if (Plugin.ShareInLobby.Value) LobbySync.Publish(Key, Encode(me.NetId));
            else LobbySync.Clear(Key);
            Read();
        }

        public static void Clear()
        {
            others.Clear();
            remotes.Clear();
            LobbySync.Clear(Key);
        }

        /// <summary>按伤害从高到低排列的队伍成员（含自己）。</summary>
        public static List<TeamMember> Members(bool run)
        {
            var list = new List<TeamMember>();
            var period = run ? Meter.Run : Meter.Wave;
            list.Add(new TeamMember
            {
                Name = "我",
                IsMe = true,
                HasData = true,
                Damage = period.Total,
                Seconds = period.Seconds,
                Top = period.Sources.FirstOrDefault()?.Name,
            });
            foreach (var kv in others)
            {
                var m = new TeamMember { Name = kv.Value };
                if (remotes.TryGetValue(kv.Key, out var r))
                {
                    m.HasData = true;
                    // 波次刚切换时对方的数据可能还是上一波的，先不显示
                    bool sameWave = r.Wave == Meter.WaveNumber;
                    m.Damage = run ? r.RunDamage : sameWave ? r.WaveDamage : 0;
                    m.Seconds = run ? r.RunSeconds : sameWave ? r.WaveSeconds : 0;
                    m.Top = run ? r.RunTop : sameWave ? r.WaveTop : null;
                }
                list.Add(m);
            }
            return list.OrderByDescending(m => m.HasData).ThenByDescending(m => m.Damage).ToList();
        }

        private static string Encode(uint netId)
        {
            var w = Meter.Wave;
            var r = Meter.Run;
            return string.Join("|", Format, netId, Meter.WaveNumber,
                w.Total, Mathf.FloorToInt(w.Seconds), Clean(w.Sources.FirstOrDefault()?.Name),
                r.Total, Mathf.FloorToInt(r.Seconds), Clean(r.Sources.FirstOrDefault()?.Name));
        }

        private static void Read()
        {
            var found = new Dictionary<uint, Remote>();
            // 别人写的数据不可信：只认场上确实存在的角色，同一个角色被多个成员声明时都不认
            var conflicts = new HashSet<uint>();
            foreach (var entry in LobbySync.Read(Key))
            {
                if (!TryParse(entry.Value, out uint netId, out var remote) || !others.ContainsKey(netId)) continue;
                if (found.ContainsKey(netId)) conflicts.Add(netId);
                found[netId] = remote;
            }
            foreach (uint id in conflicts) found.Remove(id);
            remotes = found;
        }

        private static bool TryParse(string value, out uint netId, out Remote r)
        {
            netId = 0;
            r = default;
            var p = (value ?? "").Split('|');
            if (p.Length != 9 || p[0] != Format || !uint.TryParse(p[1], out netId)) return false;
            if (!int.TryParse(p[2], out r.Wave)
                || !long.TryParse(p[3], out r.WaveDamage) || !int.TryParse(p[4], out r.WaveSeconds)
                || !long.TryParse(p[6], out r.RunDamage) || !int.TryParse(p[7], out r.RunSeconds))
                return false;
            if (r.WaveDamage < 0 || r.RunDamage < 0 || r.WaveSeconds < 0 || r.RunSeconds < 0) return false;
            r.WaveTop = Clean(p[5]);
            r.RunTop = Clean(p[8]);
            return true;
        }

        // 去掉分隔符和富文本标记，限制长度
        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = new string(s.Where(c => c != '|' && c != '<' && c != '>' && !char.IsControl(c)).ToArray());
            return s.Length > MaxNameLength ? s.Substring(0, MaxNameLength) : s;
        }
    }
}
