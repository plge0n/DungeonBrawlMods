using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DBModKit
{
    /// <summary>
    /// Mod 的多语言文本。代码里写中文原文当默认值，翻译放在插件 dll 旁边的 <c>lang/&lt;语言&gt;.json</c>
    /// （平铺的 键 → 文本，例如 <c>lang/en.json</c>：<c>{ "debug.title": "Debug" }</c>）。
    /// 当前语言跟随游戏设置；找不到当前语言的翻译时用英文（游戏不是中文时），再找不到就用代码里的中文原文。
    /// 没有翻译文件时什么都不用做，所有文本照常显示中文。
    /// </summary>
    /// <example>
    /// <code>
    /// var t = mod.Text;                                   // ModContext 自带，按插件 dll 位置找 lang 文件夹
    /// NativeUi.Label(parent, t["debug.title", "调试"]);
    /// t.Format("rule.fired", "规则「{0}」触发", rule.Name);
    /// </code>
    /// </example>
    public sealed class ModText
    {
        private readonly string folder;
        private readonly Dictionary<string, Dictionary<string, string>> cache = new Dictionary<string, Dictionary<string, string>>();

        /// <param name="folder">放 &lt;语言&gt;.json 的文件夹；不存在也没关系。</param>
        public ModText(string folder) => this.folder = folder;

        /// <summary>键对应的当前语言文本；没有翻译时返回 chinese（代码里的原文）。</summary>
        public string this[string key, string chinese] => Get(key, chinese);

        public string Get(string key, string chinese)
        {
            string lang = Localization.Language;
            if (lang == Localization.Chinese) return chinese;
            if (Lookup(lang, key) is string s) return s;
            if (lang != Localization.English && Lookup(Localization.English, key) is string en) return en;
            return chinese;
        }

        /// <summary>同 <see cref="Get"/>，再用 string.Format 填参数。</summary>
        public string Format(string key, string chinese, params object[] args)
        {
            string pattern = Get(key, chinese);
            try
            {
                return string.Format(pattern, args);
            }
            catch (FormatException)
            {
                // 翻译里的 {0} 写错了，退回原文，免得界面整块出错
                return string.Format(chinese, args);
            }
        }

        private string Lookup(string lang, string key)
        {
            if (!cache.TryGetValue(lang, out var table))
            {
                table = Load(lang);
                cache[lang] = table;
            }
            return table != null && table.TryGetValue(key, out var s) && !string.IsNullOrEmpty(s) ? s : null;
        }

        private Dictionary<string, string> Load(string lang)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            string path = Path.Combine(folder, lang + ".json");
            if (!File.Exists(path)) return null;
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Log.Warning($"翻译文件 {path} 读取失败：{e.Message}");
                return null;
            }
        }

        /// <summary>丢掉已读的翻译（换语言、改了翻译文件后调用）。</summary>
        public void Reload() => cache.Clear();
    }

    /// <summary>游戏当前语言。</summary>
    public static class Localization
    {
        public const string Chinese = "zh-CN";
        public const string English = "en";

        // 游戏的 LanguageType 顺序 → 语言代码（文件名用）
        private static readonly string[] Codes =
        {
            "zh-CN", "zh-TW", "en", "ja", "ru", "fr", "de", "es", "it", "da", "hu", "tr", "no", "th", "ro", "nl", "pt",
            "vi", "ko", "uk", "bg", "id", "el", "cs", "pl", "sv", "fi", "ar",
        };

        private static string last;

        /// <summary>游戏设置里的语言，例如 zh-CN、zh-TW、en、ja。ModKit 不兼容时为 zh-CN。</summary>
        public static string Language
        {
            get
            {
                if (!ModKit.Compatible) return Chinese;
                try
                {
                    int i = GameRaw.LanguageIndex();
                    return i >= 0 && i < Codes.Length ? Codes[i] : English;
                }
                catch
                {
                    return Chinese;
                }
            }
        }

        /// <summary>玩家在游戏设置里换了语言（每帧检查）。</summary>
        public static event Action Changed;

        internal static void Tick()
        {
            string now = Language;
            if (last != null && now != last)
            {
                Log.Info($"游戏语言切换为 {now}");
                Safe.InvokeEach(Changed, nameof(Changed));
            }
            last = now;
        }
    }
}
