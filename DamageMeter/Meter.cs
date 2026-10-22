using System.Collections.Generic;
using System.Linq;
using DBModKit;
using UnityEngine;

namespace DamageMeter
{
    // 一个伤害来源（普攻、某个技能、召唤物、其他）的累计
    internal sealed class SourceStats
    {
        public readonly string Name;
        public long Damage;
        public int Hits;
        public int Crits;
        public long MaxHit;

        public SourceStats(string name) => Name = name;

        public void Add(long damage, bool critical)
        {
            Damage += damage;
            Hits++;
            if (critical) Crits++;
            if (damage > MaxHit) MaxHit = damage;
        }
    }

    // 一段时间（一波或一整局）的统计
    internal sealed class Period
    {
        private readonly Dictionary<string, SourceStats> sources = new Dictionary<string, SourceStats>();
        // 战斗时间：只算波次进行中的时间（游戏时间，战斗加速、暂停都会反映进来）
        private float seconds;
        private float runningSince = -1f;

        public long Taken;
        public int Kills;

        public long Total => sources.Values.Sum(s => s.Damage);

        public float Seconds => seconds + (runningSince >= 0f ? Time.time - runningSince : 0f);

        public float Dps => Total / Mathf.Max(1f, Seconds);

        public bool Running => runningSince >= 0f;

        public IEnumerable<SourceStats> Sources => sources.Values.OrderByDescending(s => s.Damage);

        public void Start()
        {
            if (runningSince < 0f) runningSince = Time.time;
        }

        public void Stop()
        {
            if (runningSince < 0f) return;
            seconds += Time.time - runningSince;
            runningSince = -1f;
        }

        public void Add(string source, long damage, bool critical)
        {
            if (!sources.TryGetValue(source, out var s)) sources[source] = s = new SourceStats(source);
            s.Add(damage, critical);
        }

        public void Clear()
        {
            sources.Clear();
            seconds = 0f;
            runningSince = -1f;
            Taken = 0;
            Kills = 0;
        }
    }

    internal static class Meter
    {
        public const string NormalAttack = "普攻";
        public const string Summons = "召唤物";
        public const string Other = "其他";

        public static readonly Period Wave = new Period();
        public static readonly Period Run = new Period();
        /// <summary>正在统计的波次（0 = 还没开始第一波）。</summary>
        public static int WaveNumber { get; private set; }
        private static bool waveEventSeen;

        public static void Reset()
        {
            Wave.Clear();
            Run.Clear();
            WaveNumber = 0;
            waveEventSeen = false;
        }

        public static void OnHit(HitInfo hit)
        {
            var player = Game.LocalPlayer;
            if (player == null || hit.Damage <= 0 || hit.Source == null || hit.Target == player) return;
            string source;
            if (hit.Source == player)
                source = hit.IsNormalAttack ? NormalAttack : hit.SkillId is int id ? SkillName(id) : Other;
            else if (hit.Source.IsSummonOf(player))
                source = Summons;
            else
                return;
            // 波次之间（休息、商店）打出的伤害照算，但不计时，DPS 只算波次进行中的时间。
            // 第一波开始前还没收到过波次事件时，从第一下开始计时
            if (!waveEventSeen)
            {
                Wave.Start();
                Run.Start();
            }
            Wave.Add(source, hit.Damage, hit.Critical);
            Run.Add(source, hit.Damage, hit.Critical);
        }

        public static void OnEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.WaveStarted:
                    waveEventSeen = true;
                    Wave.Clear();
                    WaveNumber = e.Wave;
                    Wave.Start();
                    Run.Start();
                    break;
                case GameEventKind.WaveEnded:
                case GameEventKind.GameOver:
                    waveEventSeen = true;
                    Wave.Stop();
                    Run.Stop();
                    break;
                case GameEventKind.Damaged:
                    Wave.Taken += e.Amount;
                    Run.Taken += e.Amount;
                    break;
                case GameEventKind.EnemyKilled:
                    Wave.Kills++;
                    Run.Kills++;
                    break;
            }
        }

        private static readonly Dictionary<int, string> skillNames = new Dictionary<int, string>();

        private static string SkillName(int id)
        {
            if (!skillNames.TryGetValue(id, out var name))
                skillNames[id] = name = SkillInfo.Get(id)?.Name ?? ("技能 " + id);
            return name;
        }

        // 12345 → 1.2万，123456789 → 1.23亿
        public static string Format(double v)
        {
            if (v >= 1e8) return (v / 1e8).ToString(v >= 1e9 ? "0.0" : "0.00") + "亿";
            if (v >= 1e4) return (v / 1e4).ToString(v >= 1e5 ? "0" : "0.0") + "万";
            return v.ToString("0");
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
