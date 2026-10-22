using System;
using System.Collections.Generic;
using DBModKit;

namespace DBStudio
{
    // 调试页的事件日志：进出战斗、有人进出房间、自动释放的决策变化、测试操作
    internal static class EventLog
    {
        private const int Max = 200;

        private static readonly List<string> lines = new List<string>();
        // 自动释放每帧都会询问，只在同一技能的结果变化时记一条
        private static readonly Dictionary<string, string> lastAutoCast = new Dictionary<string, string>();
        private static bool lastSolo = true;
        private static int lastPlayers = 1;

        public static IReadOnlyList<string> Lines => lines;

        // 每次变化加 1，页面据此判断要不要重绘
        public static int Version { get; private set; }

        public static void Add(string message)
        {
            lines.Add($"<color=#8a8a8a>{DateTime.Now:HH:mm:ss}</color>  {message}");
            if (lines.Count > Max) lines.RemoveRange(0, lines.Count - Max);
            Version++;
        }

        public static void Clear()
        {
            lines.Clear();
            Version++;
        }

        public static void Attach(ModContext mod)
        {
            lastSolo = Game.IsSolo;
            lastPlayers = Game.PlayerCount;
            mod.OnBattleStarted(() =>
            {
                lastAutoCast.Clear();
                Add("进入战斗");
            });
            mod.OnBattleEnded(() => Add("离开战斗"));
            mod.OnAutoCastResolved(OnAutoCast);
            mod.OnUpdate(WatchRoom);
        }

        private static void OnAutoCast(AutoCastResult r)
        {
            var info = r.Slot.Info;
            if (info == null) return;
            string result = r.Casted ? "已释放" : r.Verdict.Action == AutoCastAction.Skip ? "不放" : "没放出";
            string reason = string.IsNullOrEmpty(r.Verdict.Reason) ? "" : $"（{r.Verdict.Reason}）";
            string text = result + reason;
            // 释放成功每次都记；没放的只在原因变化时记
            if (!r.Casted && lastAutoCast.TryGetValue(info.Key, out var last) && last == text) return;
            lastAutoCast[info.Key] = text;
            Add($"自动释放 {info.Name}：{text}");
        }

        private static void WatchRoom()
        {
            bool solo = Game.IsSolo;
            int players = Game.PlayerCount;
            if (solo == lastSolo && players == lastPlayers) return;
            if (players > 0 && lastPlayers > 0 && players != lastPlayers)
                Add(players > lastPlayers ? $"有玩家加入，房间现在 {players} 人" : $"有玩家离开，房间现在 {players} 人");
            else if (solo != lastSolo)
                Add(solo ? "回到单人" : "进入联机房间");
            lastSolo = solo;
            lastPlayers = players;
        }
    }
}
