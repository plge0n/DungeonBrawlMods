using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;

namespace DBModKit
{
    // 游戏装在带中文的目录（比如 Steam 默认的 common\地下城竞技场）时，Mono 的一个 bug（mono/mono#20968）让 Harmony 打补丁失败：
    // 从字节数组加载的程序集（BepInEx 补过的 UnityEngine.CoreModule、MonoMod 运行时生成的辅助程序集）会用"加载那一刻的当前目录"
    // 拼 CodeBase，中文按系统代码页（GBK）转换后不是合法的 UTF-8，之后对它调 Assembly.GetName() 就抛
    // ExecutionEngineException: String conversion error: Illegal byte sequence。Unity 启动时把当前目录设成游戏目录，所以一定会碰上。
    // 两处绕开：
    //   1. ModKit 初始化、打补丁期间把当前目录临时切到 System32（纯英文），MonoMod 这时生成的程序集就不受影响；
    //   2. CoreModule 是 BepInEx 预加载器在 ModKit 之前从内存加载的，只能让 BepInEx 改从文件加载（LoadDumpedAssemblies），下次启动生效
    internal static class AsciiPath
    {
        public static bool NeedsWorkaround(string path) => path != null && path.Any(c => c > 127);

        /// <summary>当前目录带非 ASCII 字符时，临时切到 System32，Dispose 时切回。</summary>
        public static IDisposable Enter()
        {
            string current;
            try
            {
                current = Directory.GetCurrentDirectory();
            }
            catch (Exception)
            {
                return null;
            }
            if (!NeedsWorkaround(current)) return null;
            try
            {
                Directory.SetCurrentDirectory(Environment.SystemDirectory);
            }
            catch (Exception e)
            {
                Log.Warning($"无法临时切换当前目录：{e.Message}");
                return null;
            }
            return new Restore(current);
        }

        private sealed class Restore : IDisposable
        {
            private readonly string dir;
            public Restore(string dir) => this.dir = dir;

            public void Dispose()
            {
                try
                {
                    Directory.SetCurrentDirectory(dir);
                }
                catch (Exception e)
                {
                    Log.Warning($"无法切回当前目录 {dir}：{e.Message}");
                }
            }
        }

        // 游戏目录带中文、BepInEx 还在从内存加载 CoreModule 时，打开 LoadDumpedAssemblies，返回 true 表示要重启游戏
        public static bool EnsureDumpedAssemblies()
        {
            if (!NeedsWorkaround(Paths.GameRootPath)) return false;
            // 不能用 GetName()：出问题的程序集正是对它调 GetName() 会抛异常
            var core = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("UnityEngine.CoreModule,"));
            if (core == null || !string.IsNullOrEmpty(core.Location)) return false;

            try
            {
                var path = Paths.BepInExConfigPath;
                var text = File.ReadAllText(path);
                var fixedText = Regex.Replace(text, @"(?m)^LoadDumpedAssemblies\s*=\s*false", "LoadDumpedAssemblies = true");
                if (fixedText == text) return false;
                File.WriteAllText(path, fixedText);
                Log.Warning("游戏目录带中文，已在 BepInEx.cfg 里打开 LoadDumpedAssemblies（绕开 Mono 的路径编码 bug），重启游戏后生效");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"无法修改 BepInEx.cfg：{e.Message}");
                return false;
            }
        }
    }
}
