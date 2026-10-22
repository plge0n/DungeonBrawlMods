using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace Remap
{
    internal static class Program
    {
        // DBModKit 引用的、会被混淆的程序集（见 Directory.Build.props 的 ReferenceGame 部分）
        private static readonly string[] DefaultAssemblies = { "Assembly-CSharp", "Mirror", "com.rlabrecque.steamworks.net" };

        private static readonly string Usage = @"游戏更新重新混淆后，把 DBModKit 源码里的混淆名对应到新版。

用法：
  remap snapshot --version 6.45 [--out 目录]
      把当前游戏里会被混淆的程序集复制一份（默认 <游戏目录>/../_DungeonBrawl_backup/<版本>），作为下次更新的旧版。
      每次适配完新版本都要做一次。

  remap update --old 旧版快照目录 [--new 新版目录] [--src 源码目录 ...] [--notes NOTES.md] [--out 输出目录] [--write]
      对应旧版和新版程序集，生成报告（<输出目录>/report.md），并检查改写后的源码能否按新版编译。
      默认只检查不改文件；加 --write 才写回源码（和 --notes 指定的笔记）。
      --new 默认是游戏的 Managed 目录；--src 默认是仓库里的 DBModKit。

  remap selftest --old 旧版快照目录 [--seed 1] [--mutate 0.03] [--keep]
      自测：把旧版随机重新混淆一遍，用标准答案检查对应的准确率，并检查 DBModKit 改写后能否编译。
      --keep 保留模拟出的新版（打印目录），可以用它试 update。

公共选项：
  --game 游戏目录（默认环境变量 GameDir，或 D:\Steam\steamapps\common\地下城竞技场）
  --assemblies 逗号分隔的程序集名（默认 " + string.Join(",", DefaultAssemblies) + ")";

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                Console.WriteLine(Usage);
                return 0;
            }
            var opts = Parse(args.Skip(1).ToArray());
            try
            {
                switch (args[0])
                {
                    case "snapshot": return Snapshot(opts);
                    case "update": return Update(opts);
                    case "selftest": return SelfTest(opts);
                    default:
                        Console.Error.WriteLine("未知命令：" + args[0]);
                        Console.WriteLine(Usage);
                        return 2;
                }
            }
            catch (OptionException e)
            {
                Console.Error.WriteLine(e.Message);
                return 2;
            }
        }

        // ---------------- 选项 ----------------

        private sealed class OptionException : Exception
        {
            public OptionException(string m) : base(m) { }
        }

        private static Dictionary<string, List<string>> Parse(string[] args)
        {
            var d = new Dictionary<string, List<string>>();
            string key = null;
            foreach (var a in args)
            {
                if (a.StartsWith("--"))
                {
                    key = a.Substring(2);
                    if (!d.ContainsKey(key)) d[key] = new List<string>();
                }
                else if (key == null) throw new OptionException("多余的参数：" + a);
                else d[key].Add(a);
            }
            return d;
        }

        private static string Opt(Dictionary<string, List<string>> o, string key, string fallback = null)
            => o.TryGetValue(key, out var v) && v.Count > 0 ? v[0] : fallback;

        private static string Required(Dictionary<string, List<string>> o, string key)
            => Opt(o, key) ?? throw new OptionException($"缺少 --{key}");

        private static string GameDir(Dictionary<string, List<string>> o)
            => Opt(o, "game") ?? Environment.GetEnvironmentVariable("GameDir") ?? @"D:\Steam\steamapps\common\地下城竞技场";

        private static string Managed(string game) => Path.Combine(game, "DungeonBrawl_Data", "Managed");

        private static string[] Assemblies(Dictionary<string, List<string>> o)
            => Opt(o, "assemblies")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? DefaultAssemblies;

        // 仓库根目录：从工具所在位置往上找 DungeonBrawlMods.sln
        private static string RepoRoot()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                    if (File.Exists(Path.Combine(d.FullName, "DungeonBrawlMods.sln"))) return d.FullName;
            return null;
        }

        private static List<string> Sources(Dictionary<string, List<string>> o)
        {
            if (o.TryGetValue("src", out var v) && v.Count > 0) return v.Select(Path.GetFullPath).ToList();
            var root = RepoRoot() ?? throw new OptionException("找不到仓库根目录，请用 --src 指定源码目录");
            return new List<string> { Path.Combine(root, "DBModKit") };
        }

        // 编译检查用的其他引用：和真实构建一致，取仓库根目录 Directory.Build.props 里的 HintPath
        // （全引用 Managed 会多出不相干的程序集，里面的同名全局类型会造成假的冲突），再加上 Unity 的基础库
        private static List<string> ExtraRefs(string game, string[] processed)
        {
            var managed = Managed(game);
            var list = new List<string>();
            var props = RepoRoot() is string root ? Path.Combine(root, "Directory.Build.props") : null;
            if (props != null && File.Exists(props))
                foreach (Match m in Regex.Matches(File.ReadAllText(props), "HintPath=\"([^\"]+)\""))
                    list.Add(m.Groups[1].Value.Replace("$(Managed)", managed).Replace("$(GameDir)", game));
            else
                Console.WriteLine("警告：找不到 Directory.Build.props，编译检查只引用 Unity 基础库");
            foreach (var name in new[] { "netstandard.dll", "mscorlib.dll", "System.dll", "System.Core.dll" })
                list.Add(Path.Combine(managed, name));
            return list.Where(p => !processed.Contains(Path.GetFileNameWithoutExtension(p)) && File.Exists(p)).Distinct().ToList();
        }

        // ---------------- snapshot ----------------

        private static int Snapshot(Dictionary<string, List<string>> o)
        {
            var game = GameDir(o);
            var version = Required(o, "version");
            var outDir = Opt(o, "out") ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(game)), "_DungeonBrawl_backup", version);
            Directory.CreateDirectory(outDir);
            foreach (var name in Assemblies(o))
            {
                var src = Path.Combine(Managed(game), name + ".dll");
                if (!File.Exists(src))
                {
                    Console.Error.WriteLine("找不到 " + src);
                    return 1;
                }
                File.Copy(src, Path.Combine(outDir, name + ".dll"), overwrite: true);
            }
            Console.WriteLine($"已保存到 {outDir}");
            return 0;
        }

        // ---------------- update ----------------

        private static int Update(Dictionary<string, List<string>> o)
        {
            var game = GameDir(o);
            var asms = Assemblies(o);
            var oldDir = Required(o, "old");
            var newDir = Opt(o, "new") ?? Managed(game);
            var outDir = Opt(o, "out") ?? Path.Combine(RepoRoot() ?? ".", "tools", "Remap", "out");
            bool write = o.ContainsKey("write");
            if (string.Equals(Path.GetFullPath(oldDir), Path.GetFullPath(newDir), StringComparison.OrdinalIgnoreCase))
                throw new OptionException("--old 和 --new 是同一个目录");

            var fallback = new[] { Managed(game) };
            var sw = Stopwatch.StartNew();
            Console.WriteLine($"读取旧版 {oldDir}");
            var oldSide = new Side("旧版", oldDir, fallback, asms);
            Console.WriteLine($"读取新版 {newDir}");
            var newSide = new Side("新版", newDir, fallback, asms);
            Console.WriteLine($"  解密字符串：旧版 {oldSide.DecryptedStrings}，新版 {newSide.DecryptedStrings}（{sw.Elapsed.TotalSeconds:F0}s）");
            if (newSide.DecryptedStrings == 0 || newSide.DecryptedStrings < oldSide.DecryptedStrings / 2)
                Console.WriteLine("  警告：新版解密出的字符串明显变少，混淆器可能换了字符串加密方式（见 Side.DecryptStrings），对应会只靠结构，准确率下降");

            var matcher = new Matcher(oldSide, newSide) { Log = Console.WriteLine };
            matcher.Run();
            Console.WriteLine($"  对应完成（{sw.Elapsed.TotalSeconds:F0}s）");

            var extra = ExtraRefs(game, asms);
            var scan = new SourceScan(oldSide, Sources(o), extra);
            var rewrite = scan.Rewrite((d, p) => NewName(matcher, d, p), newSide, extra);

            Directory.CreateDirectory(outDir);
            var report = Report(oldSide, newSide, matcher, scan, rewrite, null);
            File.WriteAllText(Path.Combine(outDir, "report.md"), report, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDir, "mapping.tsv"), MappingTsv(matcher, oldSide), new UTF8Encoding(false));

            string notes = Opt(o, "notes");
            string notesText = null;
            int notesChanges = 0;
            if (notes != null) notesText = RewriteNotes(File.ReadAllText(notes), oldSide, matcher, out notesChanges);

            Console.WriteLine();
            Console.WriteLine($"源码：{rewrite.Changed} 处改名，{rewrite.Unresolved.Count} 处没有对应，按新版编译 {rewrite.NewErrors.Count} 个错误");
            if (notes != null) Console.WriteLine($"笔记：{notesChanges} 处改名");
            Console.WriteLine($"报告：{Path.Combine(outDir, "report.md")}");
            if (write)
            {
                scan.Write(rewrite);
                if (notesText != null) File.WriteAllText(notes, notesText, new UTF8Encoding(false));
                Console.WriteLine("已写回源码" + (notes != null ? "和笔记" : "") + "。检查 git diff 后编译、实机测试，再更新 Compat.TestedVersions。");
            }
            else Console.WriteLine("没有修改文件（加 --write 写回）");
            return rewrite.Unresolved.Count == 0 && rewrite.NewErrors.Count == 0 ? 0 : 1;
        }

        private static string NewName(Matcher m, IMemberDefinition oldDef, int param)
        {
            var nd = m.NewOf(oldDef);
            if (nd == null) return null;
            if (param >= 0) return nd is MethodDefinition md && param < md.Parameters.Count ? md.Parameters[param].Name : null;
            return nd.Name;
        }

        // ---------------- selftest ----------------

        private static int SelfTest(Dictionary<string, List<string>> o)
        {
            var game = GameDir(o);
            var asms = Assemblies(o);
            var oldDir = Required(o, "old");
            int seed = int.Parse(Opt(o, "seed", "1"));
            double mutate = double.Parse(Opt(o, "mutate", "0.03"), System.Globalization.CultureInfo.InvariantCulture);
            var fallback = new[] { Managed(game) };
            var tmp = Path.Combine(Path.GetTempPath(), "remap-selftest-" + seed);
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);


            var sw = Stopwatch.StartNew();
            var scrambler = new Scrambler(seed, mutate);
            scrambler.Run(oldDir, fallback, asms, tmp);
            Console.WriteLine($"重新混淆：改名 {scrambler.Renamed} 个，改动方法 {scrambler.Mutated} 个 → {tmp}（{sw.Elapsed.TotalSeconds:F0}s）");

            var oldSide = new Side("旧版", oldDir, fallback, asms);
            var newSide = new Side("模拟新版", tmp, fallback, asms);
            var matcher = new Matcher(oldSide, newSide) { Log = Console.WriteLine };
            matcher.Run();
            Console.WriteLine($"对应完成（{sw.Elapsed.TotalSeconds:F0}s）");
            foreach (var name in o.GetValueOrDefault("explain") ?? new List<string>()) matcher.Explain(name, Console.WriteLine);

            var extra = ExtraRefs(game, asms);
            var scan = new SourceScan(oldSide, Sources(o), extra);
            var rewrite = scan.Rewrite((d, p) => NewName(matcher, d, p), newSide, extra);

            // 准确率：全部 / DBModKit 用到的
            var stats = new SortedDictionary<string, int[]>(); // 类别 → [总数, 正确, 错误, 未对应]
            var wrongUsed = new List<string>();
            var wrongOther = new List<string>();
            void Count(string cat, bool? ok)
            {
                if (!stats.TryGetValue(cat, out var a)) stats[cat] = a = new int[4];
                a[0]++;
                a[ok == true ? 1 : ok == false ? 2 : 3]++;
            }
            foreach (var t in oldSide.Types.Where(t => !Names.IsStringHolder(t)))
            {
                foreach (var def in Members(t))
                {
                    var nd = matcher.NewOf(def);
                    bool? ok = nd == null ? (bool?)null : scrambler.Truth.TryGetValue(Side.Key(def), out var exp) && exp == Side.Key(nd);
                    // 等价副本（代码完全相同）互换不算错，单独统计
                    if (ok == false && matcher.ByOld[def].How == "等价副本")
                    {
                        Count("等价副本互换（代码相同，不算错）", true);
                        ok = true;
                    }
                    string cat = def switch { TypeDefinition _ => "类型", MethodDefinition _ => "方法", FieldDefinition _ => "字段", PropertyDefinition _ => "属性", _ => "事件" };
                    bool renamed = scrambler.Truth.TryGetValue(Side.Key(def), out var e2) && e2 != Side.Key(def);
                    Count(cat + (renamed ? "（改名）" : "（未改名）"), ok);
                    if (ok == false && wrongOther.Count < 15)
                        wrongOther.Add($"{Side.Key(def)} → {Side.Key(nd)}（{matcher.ByOld[def].How}，应为 {scrambler.Truth.GetValueOrDefault(Side.Key(def))}）");
                    if (scan.Used.Contains(def))
                    {
                        Count("★ DBModKit 用到的", ok);
                        if (ok != true) wrongUsed.Add($"{Side.Key(def)} → {(nd == null ? "未对应" : Side.Key(nd))}（应为 {scrambler.Truth.GetValueOrDefault(Side.Key(def))}）");
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine("| 类别 | 总数 | 正确 | 错误 | 未对应 |");
            Console.WriteLine("|---|---|---|---|---|");
            foreach (var kv in stats)
                Console.WriteLine($"| {kv.Key} | {kv.Value[0]} | {kv.Value[1]} | {kv.Value[2]} | {kv.Value[3]} |");
            Console.WriteLine();
            foreach (var w in wrongUsed) Console.WriteLine("  ✗ " + w);
            foreach (var w in wrongOther) Console.WriteLine("  ✗（未用到）" + w);
            Console.WriteLine($"源码：{rewrite.Changed} 处改名，{rewrite.Unresolved.Count} 处没有对应，按模拟新版编译 {rewrite.NewErrors.Count} 个错误");
            foreach (var u in rewrite.Unresolved.Take(30)) Console.WriteLine("  ? " + u);
            foreach (var e in rewrite.NewErrors.Take(30)) Console.WriteLine("  ! " + e);
            foreach (var w in scan.Warnings.Concat(rewrite.Warnings).Distinct().Take(30)) Console.WriteLine("  ~ " + w);
            if (scan.OldErrors.Count > 0)
            {
                Console.WriteLine($"注意：源码按旧版编译就有 {scan.OldErrors.Count} 个错误（源码和旧版快照不一致？）");
                foreach (var e in scan.OldErrors.Take(10)) Console.WriteLine("  ! " + e);
            }
            var dump = Opt(o, "dump");
            if (dump != null)
            {
                Directory.CreateDirectory(dump);
                foreach (var kv in rewrite.NewTexts) File.WriteAllText(Path.Combine(dump, Path.GetFileName(kv.Key)), kv.Value);
            }
            if (o.ContainsKey("keep"))
            {
                Console.WriteLine("模拟新版保留在 " + tmp + "（可以拿来试 update）");
                return 0;
            }
            try
            {
                Directory.Delete(tmp, true);
            }
            catch (IOException)
            {
                // Cecil 还开着文件；留在临时目录里，下次同一个 seed 运行时再删
            }
            int wrong = stats.Values.Sum(a => a[2]);
            return wrongUsed.Count == 0 && rewrite.NewErrors.Count == 0 && wrong == 0 ? 0 : 1;
        }

        private static IEnumerable<IMemberDefinition> Members(TypeDefinition t)
        {
            yield return t;
            foreach (var x in t.Fields) yield return x;
            foreach (var x in t.Methods) yield return x;
            foreach (var x in t.Properties) yield return x;
            foreach (var x in t.Events) yield return x;
        }

        // ---------------- 报告 ----------------

        private static string Report(Side oldSide, Side newSide, Matcher m, SourceScan scan, SourceScan.Result rewrite, string extra)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 混淆名映射重建报告");
            sb.AppendLine();
            sb.AppendLine($"- 旧版：`{oldSide.Dir}`");
            sb.AppendLine($"- 新版：`{newSide.Dir}`");
            sb.AppendLine($"- 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();

            sb.AppendLine("## 结论");
            sb.AppendLine();
            sb.AppendLine($"- 源码改名 {rewrite.Changed} 处；**没有对应 {rewrite.Unresolved.Count} 处**；按新版编译 **{rewrite.NewErrors.Count} 个错误**");
            int total = 0, matched = 0;
            foreach (var t in oldSide.Types.Where(t => !Names.IsStringHolder(t)))
                foreach (var d in Members(t))
                {
                    total++;
                    if (m.NewOf(d) != null) matched++;
                }
            sb.AppendLine($"- 整体：旧版 {total} 个类型/成员，对应上 {matched} 个（{100.0 * matched / Math.Max(1, total):F1}%）");
            sb.AppendLine();

            if (rewrite.Unresolved.Count > 0 || rewrite.NewErrors.Count > 0)
            {
                sb.AppendLine("## 需要人工处理");
                sb.AppendLine();
                foreach (var u in rewrite.Unresolved) sb.AppendLine("- " + u);
                foreach (var e in rewrite.NewErrors) sb.AppendLine("- 编译错误：`" + e + "`");
                sb.AppendLine();
            }
            var warnings = scan.Warnings.Concat(rewrite.Warnings).Distinct().ToList();
            if (warnings.Count > 0)
            {
                sb.AppendLine("## 提示");
                sb.AppendLine();
                foreach (var w in warnings) sb.AppendLine("- " + w);
                sb.AppendLine();
            }
            if (scan.OldErrors.Count > 0)
            {
                sb.AppendLine("## 源码按旧版编译的错误（源码和旧版快照不一致？）");
                sb.AppendLine();
                foreach (var e in scan.OldErrors) sb.AppendLine("- `" + e + "`");
                sb.AppendLine();
            }

            // DBModKit 用到的成员，可疑的排前面
            sb.AppendLine("## DBModKit 用到的成员");
            sb.AppendLine();
            sb.AppendLine("可信度：同名/访问器 = 确定；打分项看分数和次优差距，差距小于 0.15 的标 ⚠，建议对照反编译代码确认。");
            sb.AppendLine();
            sb.AppendLine("| 旧 | 新 | 依据 | 分数 | 次优 | |");
            sb.AppendLine("|---|---|---|---|---|---|");
            var rows = scan.Used.Select(d => (Def: d, Pair: m.ByOld.GetValueOrDefault(d)))
                .OrderBy(x => x.Pair == null ? 0 : x.Pair.How == "等价副本" || x.Pair.How == "打分" && x.Pair.Score - x.Pair.Runner < 0.15 ? 1 : 2)
                .ThenBy(x => Side.Key(x.Def));
            foreach (var (def, pair) in rows)
            {
                string old = Short(def);
                if (pair == null)
                {
                    sb.AppendLine($"| `{old}` | — | 未对应 | | | ❌ |");
                    continue;
                }
                string flag = pair.How == "等价副本" ? "⚠ 有完全相同的副本，若是补丁目标要确认打在被调用的那一个上"
                    : pair.How == "打分" && pair.Score - pair.Runner < 0.15 ? "⚠" : Short(pair.New) == old ? "" : "改名";
                sb.AppendLine($"| `{old}` | `{Short(pair.New)}` | {pair.How} | {(pair.How == "打分" ? pair.Score.ToString("F2") : "")} | {(pair.How == "打分" ? pair.Runner.ToString("F2") : "")} | {flag} |");
            }
            if (extra != null) sb.AppendLine(extra);
            return sb.ToString();
        }

        private static string Short(IMemberDefinition d)
            => d is TypeDefinition t ? t.FullName : d is MethodDefinition md
                ? $"{md.DeclaringType.FullName}.{md.Name}({string.Join(", ", md.Parameters.Select(p => p.ParameterType.Name))})"
                : d.DeclaringType.FullName + "." + d.Name;

        private static string MappingTsv(Matcher m, Side oldSide)
        {
            var sb = new StringBuilder("旧\t新\t依据\t分数\t次优\n");
            foreach (var p in m.ByOld.Values.Where(p => Side.Key(p.Old) != Side.Key(p.New)).OrderBy(p => Side.Key(p.Old)))
                sb.AppendLine($"{Short(p.Old)}\t{Short(p.New)}\t{p.How}\t{p.Score:F2}\t{p.Runner:F2}");
            return sb.ToString();
        }

        // ---------------- NOTES.md：只改反引号里的 `类型.成员` 和单独的混淆类型名 ----------------

        private static readonly Regex Code = new Regex("`[^`\n]+`");
        private static readonly Regex Ident = new Regex(@"(?<![\w.])([A-Za-z_]\w*)(?:\.([A-Za-z_]\w*))?(?![\w])");

        private static string RewriteNotes(string text, Side oldSide, Matcher m, out int changes)
        {
            int count = 0;
            var result = Code.Replace(text, code => Ident.Replace(code.Value, id =>
            {
                var types = oldSide.FindTypesBySimpleName(id.Groups[1].Value);
                if (types.Count != 1 || !Names.LooksObfuscated(types[0].Name) && !id.Groups[2].Success) return id.Value;
                var type = types[0];
                var nt = m.NewOf(type);
                string typeName = nt?.Name ?? type.Name;
                if (!id.Groups[2].Success)
                {
                    if (type.Name.Length < 2 || nt == null || nt.Name == type.Name) return id.Value;
                    count++;
                    return typeName;
                }
                var members = oldSide.FindMembers(type, id.Groups[2].Value).ToList();
                var names = members.Select(x => m.NewOf(x)?.Name).Distinct().ToList();
                string member = names.Count == 1 && names[0] != null ? names[0] : id.Groups[2].Value;
                string replaced = (Names.LooksObfuscated(type.Name) ? typeName : type.Name) + "." + member;
                if (replaced != id.Value) count++;
                return replaced;
            }));
            changes = count;
            return result;
        }
    }
}
