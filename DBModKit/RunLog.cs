using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace DBModKit
{
    public enum RunLogLevel
    {
        /// <summary>高频细节：每个战斗事件、自动释放的每次决策变化、输入屏蔽的获取和释放。</summary>
        Debug,
        /// <summary>值得留意的事：Mod 加载、进出战斗、规则触发、改了玩家的数值。</summary>
        Info,
        Warning,
        Error,
    }

    /// <summary>一条运行记录。</summary>
    public sealed class RunLogEntry
    {
        private static readonly KeyValuePair<string, object>[] NoData = new KeyValuePair<string, object>[0];

        public DateTime Time { get; }
        /// <summary>Unity 帧号，同一帧发生的记录帧号相同。</summary>
        public int Frame { get; }
        /// <summary>记录时的波次；不在战斗中为 -1。</summary>
        public int Wave { get; }
        /// <summary>写这条记录的 Mod（插件名）。</summary>
        public string Source { get; }
        /// <summary>分类，例如"规则""玩家""战斗"。</summary>
        public string Category { get; }
        public RunLogLevel Level { get; }
        public string Message { get; }
        /// <summary>附带的数据（按写入顺序）。</summary>
        public IReadOnlyList<KeyValuePair<string, object>> Data { get; }

        internal RunLogEntry(string source, string category, RunLogLevel level, string message, (string Key, object Value)[] data, int wave)
        {
            Time = DateTime.Now;
            Frame = UnityEngine.Time.frameCount;
            Wave = wave;
            Source = source ?? "?";
            Category = category ?? "";
            Level = level;
            Message = message ?? "";
            Data = data == null || data.Length == 0 ? NoData : data.Select(d => new KeyValuePair<string, object>(d.Key, d.Value)).ToArray();
        }

        /// <summary>写进 trace.log 的一行：时间 级别 [来源/分类] 内容 键=值 …（波次 帧号）。</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Time.ToString("HH:mm:ss.fff")).Append(' ').Append(LevelTag(Level)).Append(" [").Append(Source);
            if (Category.Length > 0) sb.Append('/').Append(Category);
            sb.Append("] ").Append(Message);
            if (Data.Count > 0) sb.Append("  ").Append(DataText());
            sb.Append("  (");
            if (Wave >= 0) sb.Append('w').Append(Wave).Append(' ');
            sb.Append('#').Append(Frame).Append(')');
            return sb.ToString();
        }

        /// <summary>附带数据写成"键=值 键=值"。</summary>
        public string DataText() => string.Join(" ", Data.Select(d => $"{d.Key}={RunLog.FormatValue(d.Value)}"));

        internal static string LevelTag(RunLogLevel l)
        {
            switch (l)
            {
                case RunLogLevel.Debug: return "DEBUG";
                case RunLogLevel.Info: return "INFO ";
                case RunLogLevel.Warning: return "WARN ";
                default: return "ERROR";
            }
        }
    }

    /// <summary>
    /// 运行记录：所有 Mod 共用的一份带时间、波次、帧号的结构化日志，用来确认功能有没有生效、定位问题。
    /// 内存里保留最近 <see cref="Capacity"/> 条（所有级别），同时写进 <c>BepInEx/DBModKit/trace.log</c>（只写 ≥ 文件级别的，默认 Info）。
    /// 每次启动游戏时上一次的记录改名为 trace.prev.log。Warning 和 Error 还会同时写进 BepInEx 的 LogOutput.log。
    /// Mod 里一般用 <see cref="ModContext.RunLog"/> 写，来源自动填成 Mod 的名字。
    /// ModKit 自己会记录：Mod 加载和卸载、进出战斗、房间人数变化、通过 <see cref="Player"/> 改数值（前后对比）、
    /// 改配置表、三选一、输入屏蔽和暂停、战斗事件（Debug）、所有被拦下的异常。
    /// </summary>
    public static class RunLog
    {
        public const int Capacity = 3000;

        private static readonly List<RunLogEntry> recent = new List<RunLogEntry>();
        private static readonly Dictionary<string, ManualLogSource> mirrors = new Dictionary<string, ManualLogSource>();
        private static readonly object gate = new object();
        private static StreamWriter file;
        private static bool fileFailed;
        private static int markers;
        private static ConfigEntry<RunLogLevel> fileLevel;
        private static ConfigEntry<KeyboardShortcut> markHotkey;

        /// <summary>每写一条加 1，界面据此判断要不要刷新。</summary>
        public static int Version { get; private set; }

        /// <summary>写入一条记录时触发（在写入的线程上，一般是主线程）。</summary>
        public static event Action<RunLogEntry> Written;

        /// <summary>写进文件的最低级别（ModKit 配置 RunLog.FileLevel，默认 Info）。测试时改成 Debug 能记下所有战斗事件。</summary>
        public static RunLogLevel FileLevel => fileLevel?.Value ?? RunLogLevel.Info;

        public static string Folder => Path.Combine(Paths.BepInExRootPath, "DBModKit");
        public static string FilePath => Path.Combine(Folder, "trace.log");

        /// <summary>最近的记录（旧的在前），返回副本。</summary>
        public static List<RunLogEntry> Recent()
        {
            lock (gate) return new List<RunLogEntry>(recent);
        }

        /// <summary>清空内存里的记录（文件不受影响）。</summary>
        public static void Clear()
        {
            lock (gate) recent.Clear();
            Version++;
        }

        public static void Write(string source, string category, RunLogLevel level, string message, params (string Key, object Value)[] data)
            => WriteCore(source, category, level, message, data, mirror: level >= RunLogLevel.Warning);

        public static void Debug(string source, string category, string message, params (string Key, object Value)[] data)
            => Write(source, category, RunLogLevel.Debug, message, data);

        public static void Info(string source, string category, string message, params (string Key, object Value)[] data)
            => Write(source, category, RunLogLevel.Info, message, data);

        public static void Warning(string source, string category, string message, params (string Key, object Value)[] data)
            => Write(source, category, RunLogLevel.Warning, message, data);

        public static void Error(string source, string category, string message, params (string Key, object Value)[] data)
            => Write(source, category, RunLogLevel.Error, message, data);

        /// <summary>
        /// 插一条带编号的标记，返回编号。测试时看到问题就按一下（默认 Ctrl+F10，或调试页的"加标记"），
        /// 之后说"标记 3 附近"就能在 trace.log 里找到当时的记录。
        /// </summary>
        public static int Mark(string note = null)
        {
            int n = ++markers;
            Write("标记", "", RunLogLevel.Info, string.IsNullOrEmpty(note) ? $"==== 标记 {n} ====" : $"==== 标记 {n}：{note} ====");
            ScreenNotice.Show($"已加标记 {n}", 2f);
            return n;
        }

        /// <summary>
        /// 导出诊断报告到 BepInEx/DBModKit/report-时间.txt，返回文件路径。
        /// 里面有游戏和插件版本、兼容检查、房间和玩家状态，以及内存里的全部记录（含 Debug）。反馈问题时附上它。
        /// </summary>
        public static string ExportReport()
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, $"report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"DBModKit 诊断报告  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"游戏版本 {ModKit.GameVersion}  ModKit {ModKit.Version}  BepInEx {typeof(Paths).Assembly.GetName().Version}  Unity {Application.unityVersion}");
            sb.AppendLine($"兼容：{(ModKit.Compatible ? "是" : "否")}" + (ModKit.MissingMembers.Count > 0 ? $"，缺失 {string.Join("，", ModKit.MissingMembers)}" : ""));
            sb.AppendLine();
            sb.AppendLine("插件：");
            foreach (var info in Chainloader.PluginInfos.Values.OrderBy(i => i.Metadata.Name))
                sb.AppendLine($"  {info.Metadata.Name} {info.Metadata.Version}  ({info.Metadata.GUID})  {Path.GetFileName(info.Location)}");
            sb.AppendLine();
            Section(sb, "状态", StateText);
            sb.AppendLine();
            var all = Recent();
            sb.AppendLine($"最近 {all.Count} 条记录（含 Debug）：");
            foreach (var e in all) sb.AppendLine(e.ToString());
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Info("DBModKit", "诊断", "已导出诊断报告", ("文件", path));
            return path;
        }

        private static void Section(StringBuilder sb, string title, Func<string> text)
        {
            sb.AppendLine(title + "：");
            try
            {
                sb.AppendLine(text());
            }
            catch (Exception e)
            {
                sb.AppendLine("  读取失败：" + e.Message);
            }
        }

        private static string StateText()
        {
            if (!ModKit.Compatible) return "  ModKit 未启用";
            var sb = new StringBuilder();
            sb.AppendLine($"  场景 {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}  时间速度 {Time.timeScale}  暂停 {GamePause.IsPaused}");
            sb.AppendLine($"  单人 {Game.IsSolo}  房主 {Game.IsHost}  人数 {Game.PlayerCount}  波次 {Game.Wave}  自动战斗 {Game.AutoBattle}");
            var p = Game.LocalPlayer;
            if (p == null) sb.Append("  不在战斗中");
            else sb.Append($"  玩家 生命 {p.Hp}/{p.MaxHp} 蓝 {p.Mana}/{p.MaxMana} 金币 {p.Gold} 攻击 {p.Attack} 力/敏/体 {p.Strength}/{p.Agility}/{p.Stamina} 敌人 {p.Enemies().Count}");
            return sb.ToString();
        }

        // ---- 内部 ----

        internal static void Configure(ConfigFile config)
        {
            fileLevel = config.Bind("RunLog", "FileLevel", RunLogLevel.Info,
                "写进 BepInEx/DBModKit/trace.log 的最低级别。测试、排查问题时改成 Debug，能记下每个战斗事件和自动释放决策");
            markHotkey = config.Bind("RunLog", "MarkHotkey", new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl),
                "在运行记录里插一条带编号的标记，方便事后找到出问题的时刻");
        }

        internal static void Tick()
        {
            if (markHotkey != null && markHotkey.Value.IsDown()) Mark();
        }

        internal static void WriteCore(string source, string category, RunLogLevel level, string message, (string Key, object Value)[] data, bool mirror)
        {
            RunLogEntry entry;
            try
            {
                entry = new RunLogEntry(source, category, level, message, data, CurrentWave());
            }
            catch (Exception)
            {
                // 不在主线程时取不到帧号
                return;
            }
            lock (gate)
            {
                recent.Add(entry);
                if (recent.Count > Capacity) recent.RemoveRange(0, recent.Count - Capacity);
                if (level >= FileLevel) WriteFile(entry);
            }
            Version++;
            if (mirror) Mirror(entry);
            var evt = Written;
            if (evt == null) return;
            foreach (Action<RunLogEntry> h in evt.GetInvocationList())
            {
                // 订阅者出错不能再走 Safe（会递归写记录）
                try { h(entry); } catch (Exception) { }
            }
        }

        private static int CurrentWave()
        {
            try
            {
                return ModKit.Compatible && GameRaw.LocalPlayer() != null ? GameRaw.Wave() + 1 : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static void WriteFile(RunLogEntry e)
        {
            if (fileFailed) return;
            try
            {
                if (file == null)
                {
                    Directory.CreateDirectory(Folder);
                    string prev = Path.Combine(Folder, "trace.prev.log");
                    if (File.Exists(FilePath))
                    {
                        if (File.Exists(prev)) File.Delete(prev);
                        File.Move(FilePath, prev);
                    }
                    file = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
                    file.WriteLine($"# DBModKit {ModKit.Version} 运行记录  游戏版本 {Application.version}  {DateTime.Now:yyyy-MM-dd HH:mm:ss}  文件级别 {FileLevel}");
                }
                file.WriteLine(e.ToString());
            }
            catch (Exception ex)
            {
                fileFailed = true;
                Log.Source?.LogWarning($"写 trace.log 失败，之后只保留在内存里：{ex.Message}");
            }
        }

        private static void Mirror(RunLogEntry e)
        {
            if (!mirrors.TryGetValue(e.Source, out var log))
            {
                log = e.Source == ModKitPlugin.Name && Log.Source != null ? Log.Source : BepInEx.Logging.Logger.CreateLogSource(e.Source);
                mirrors[e.Source] = log;
            }
            string text = (e.Category.Length > 0 ? $"[{e.Category}] " : "") + e.Message + (e.Data.Count > 0 ? "  " + e.DataText() : "");
            log.Log(e.Level == RunLogLevel.Error ? LogLevel.Error : e.Level == RunLogLevel.Warning ? LogLevel.Warning : LogLevel.Info, text);
        }

        internal static string FormatValue(object v)
        {
            switch (v)
            {
                case null: return "null";
                case string s: return s.IndexOf(' ') >= 0 ? $"\"{s}\"" : s;
                case float f: return f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                case double d: return d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                case Vector3 p: return $"({p.x:0.#},{p.z:0.#})";
                case bool b: return b ? "是" : "否";
                default: return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        // 调用 ModKit 的那个 Mod：沿调用栈找第一个不属于 ModKit（也不属于 Unity、BepInEx）的程序集
        private static readonly Dictionary<Assembly, string> assemblyNames = new Dictionary<Assembly, string>();

        // 内容包的效果在 DBModKit 里执行，调用栈上找不到 Mod：执行时由 ContentEffects 设成内容包的名字
        [ThreadStatic] internal static string CallerOverride;

        internal static string CallerMod()
        {
            if (CallerOverride != null) return CallerOverride;
            var self = typeof(RunLog).Assembly;
            var frames = new StackTrace(2, false).GetFrames();
            if (frames == null) return "?";
            foreach (var f in frames)
            {
                var asm = f.GetMethod()?.DeclaringType?.Assembly;
                if (asm == null || asm == self) continue;
                string n = asm.GetName().Name;
                if (n.StartsWith("UnityEngine") || n == "mscorlib" || n.StartsWith("System") || n.StartsWith("BepInEx") || n == "0Harmony") continue;
                return PluginName(asm);
            }
            return "?";
        }

        private static string PluginName(Assembly asm)
        {
            if (assemblyNames.TryGetValue(asm, out var name)) return name;
            name = Chainloader.PluginInfos.Values
                .Where(i => !ReferenceEquals(i.Instance, null) && i.Instance.GetType().Assembly == asm)
                .Select(i => i.Metadata.Name).FirstOrDefault();
            // ScriptEngine 热重载出来的程序集名字带后缀（DBStudio-637…），去掉
            name ??= asm.GetName().Name.Split('-')[0];
            assemblyNames[asm] = name;
            return name;
        }
    }

    /// <summary>某个 Mod 的运行记录写入器（来源固定为 Mod 名），从 <see cref="ModContext.RunLog"/> 拿。</summary>
    public sealed class ModRunLog
    {
        public string Source { get; }

        internal ModRunLog(string source) => Source = source;

        public void Debug(string category, string message, params (string Key, object Value)[] data) => RunLog.Write(Source, category, RunLogLevel.Debug, message, data);
        public void Info(string category, string message, params (string Key, object Value)[] data) => RunLog.Write(Source, category, RunLogLevel.Info, message, data);
        public void Warning(string category, string message, params (string Key, object Value)[] data) => RunLog.Write(Source, category, RunLogLevel.Warning, message, data);
        public void Error(string category, string message, params (string Key, object Value)[] data) => RunLog.Write(Source, category, RunLogLevel.Error, message, data);
    }
}
