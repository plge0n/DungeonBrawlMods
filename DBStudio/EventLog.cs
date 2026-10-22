using System;
using System.Collections.Generic;
using System.Linq;
using DBModKit;

namespace DBStudio
{
    // 调试页的事件日志：显示 ModKit 运行记录（所有 Mod 共用，同时写进 BepInEx/DBModKit/trace.log）。
    // 进出战斗、房间人数、改玩家数值、自动释放决策这些由 ModKit 自己记录；工作台的操作用 Add 记
    internal static class EventLog
    {
        private const int MaxShown = 300;

        private static ModRunLog log;
        // "清空"只清这一页的显示，运行记录和 trace.log 保留（导出诊断时还用得到）
        private static DateTime clearedAt = DateTime.MinValue;
        private static int clearCount;

        // 显示 Debug 级别（每个战斗事件、自动释放的每次决策变化）
        public static bool Verbose;

        // 运行记录变化、清空或切换详细时都会变，页面据此判断要不要重绘
        public static int Version => RunLog.Version * 4 + clearCount * 2 + (Verbose ? 1 : 0);

        public static void Attach(ModContext mod) => log = mod.RunLog;

        public static void Add(string message) => log?.Info("工作台", message);

        public static void Clear()
        {
            clearedAt = DateTime.Now;
            clearCount++;
        }

        public static List<string> Lines()
        {
            var min = Verbose ? RunLogLevel.Debug : RunLogLevel.Info;
            return RunLog.Recent()
                .Where(e => e.Level >= min && e.Time > clearedAt)
                .Reverse().Take(MaxShown).Reverse()
                .Select(Format).ToList();
        }

        private static string Format(RunLogEntry e)
        {
            string color = e.Level == RunLogLevel.Error ? "#e07a5f" : e.Level == RunLogLevel.Warning ? "#d6b67d" : e.Level == RunLogLevel.Debug ? "#7f8c99" : null;
            string where = e.Source == Plugin.Name ? e.Category : string.IsNullOrEmpty(e.Category) ? e.Source : $"{e.Source}/{e.Category}";
            string text = e.Message + (e.Data.Count > 0 ? $"  <color=#a6a6a6>{e.DataText()}</color>" : "");
            if (color != null) text = $"<color={color}>{text}</color>";
            return $"<color=#8a8a8a>{e.Time:HH:mm:ss}</color>  " + (string.IsNullOrEmpty(where) ? "" : $"<color=#9aa7b5>[{where}]</color> ") + text;
        }
    }
}
