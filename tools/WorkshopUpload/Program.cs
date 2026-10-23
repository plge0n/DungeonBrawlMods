using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;

// 创意工坊上传：读仓库里的 workshop/items.json，按条目名上传（先跑 pack.ps1 生成 dist/workshop/）。
//   dotnet run --project tools/WorkshopUpload -- Runtime            上传/更新
//   dotnet run --project tools/WorkshopUpload -- Runtime --dry-run  只检查，不上传
//   dotnet run --project tools/WorkshopUpload -- Hextech --since 0.1.1   更新说明带上 0.1.1 之后的每一版（中间的版本没上传过时用）
// 第一次上传会新建条目并把编号写回 items.json（要提交）。标题和说明按语言分别提交（schinese 为主，其余是翻译）。
// 需要 Steam 客户端开着并登录了拥有游戏的账号。
internal static class Program
{
    private const uint AppId = 4246640;

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string name = args.Where((a, i) => !a.StartsWith("--") && (i == 0 || args[i - 1] != "--since")).FirstOrDefault();
        bool dryRun = args.Contains("--dry-run");
        int sinceAt = Array.IndexOf(args, "--since");
        string since = sinceAt >= 0 && sinceAt + 1 < args.Length ? args[sinceAt + 1] : null;
        string root = FindRepoRoot();
        string itemsPath = Path.Combine(root, "workshop", "items.json");
        var items = JObject.Parse(File.ReadAllText(itemsPath));
        if (name == null || items[name] == null)
        {
            Console.WriteLine($"用法：WorkshopUpload <条目> [--dry-run] [--since 旧版本]，条目：{string.Join("、", items.Properties().Select(p => p.Name))}");
            return 2;
        }
        var item = (JObject)items[name];
        ulong id = item.Value<ulong>("Id");
        string folder = Path.GetFullPath(Path.Combine(root, item.Value<string>("Folder")));
        string preview = Path.GetFullPath(Path.Combine(root, item.Value<string>("Preview")));
        var titles = item["Title"].ToObject<Dictionary<string, string>>();
        var descriptions = item["Description"].ToObject<Dictionary<string, string>>();
        var tags = item["Tags"]?.ToObject<List<string>>() ?? new List<string>();
        var visibility = ParseVisibility(item.Value<string>("Visibility"));
        // 第一次上传不贴开发用的更新日志
        string note = id == 0 ? FirstNote(folder) : ChangeNote(root, folder, since);

        // 上传前检查：内容文件夹、预览图、长度限制
        var problems = new List<string>();
        if (!Directory.Exists(folder) || !Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any()) problems.Add($"内容文件夹不存在或是空的：{folder}（先跑 pack.ps1）");
        if (!File.Exists(preview)) problems.Add($"预览图不存在：{preview}");
        else if (new FileInfo(preview).Length >= 1024 * 1024) problems.Add("预览图要小于 1 MB");
        if (!titles.ContainsKey("schinese")) problems.Add("Title 里缺少 schinese（主语言）");
        foreach (var t in titles) if (t.Value.Length > 128) problems.Add($"标题（{t.Key}）超过 128 字");
        foreach (var d in descriptions) if (d.Value.Length > 8000) problems.Add($"说明（{d.Key}）超过 8000 字");
        // Requires：必需物品（Steam 页面上的"必需物品"，订阅时提示一起订阅），写 items.json 里别的条目名，那个条目要先上传过
        var requires = new List<(string Name, ulong Id)>();
        foreach (var r in item["Requires"]?.ToObject<List<string>>() ?? new List<string>())
        {
            ulong rid = (items[r] as JObject)?.Value<ulong>("Id") ?? 0;
            if (rid == 0) problems.Add($"必需物品 {r} 不在 items.json 里或还没上传过");
            else requires.Add((r, rid));
        }

        long size = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) : 0;
        Console.WriteLine($"条目 {name}  {(id == 0 ? "（新建）" : $"编号 {id}")}  可见性 {visibility}");
        Console.WriteLine($"内容 {folder}（{size / 1024} KB）");
        Console.WriteLine($"预览 {preview}");
        foreach (var t in titles) Console.WriteLine($"标题[{t.Key}] {t.Value}");
        foreach (var r in requires) Console.WriteLine($"必需物品 {r.Name}（{r.Id}）");
        Console.WriteLine($"更新说明：{note}");
        if (problems.Count > 0)
        {
            foreach (var p in problems) Console.WriteLine("× " + p);
            return 1;
        }
        if (dryRun)
        {
            Console.WriteLine("检查通过（--dry-run，没有上传）");
            return 0;
        }

        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "steam_appid.txt"), AppId.ToString());
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        if (!SteamAPI.Init())
        {
            Console.WriteLine("× Steam 初始化失败：确认 Steam 客户端开着、已登录拥有《地下城竞技场》的账号");
            return 1;
        }
        try
        {
            if (id == 0)
            {
                var created = Wait<CreateItemResult_t>(SteamUGC.CreateItem(new AppId_t(AppId), EWorkshopFileType.k_EWorkshopFileTypeCommunity));
                if (created.m_eResult != EResult.k_EResultOK)
                {
                    Console.WriteLine($"× 新建条目失败：{created.m_eResult}");
                    return 1;
                }
                id = created.m_nPublishedFileId.m_PublishedFileId;
                // 编号马上写回，后面失败了重跑也是更新同一个条目
                item["Id"] = id;
                File.WriteAllText(itemsPath, items.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
                Console.WriteLine($"已新建条目 {id}（已写回 items.json）");
                if (created.m_bUserNeedsToAcceptWorkshopLegalAgreement) WarnAgreement(id);
            }

            var file = new PublishedFileId_t(id);
            // 主语言：内容、预览、可见性、标签、中文标题和说明
            var h = SteamUGC.StartItemUpdate(new AppId_t(AppId), file);
            SteamUGC.SetItemUpdateLanguage(h, "schinese");
            SteamUGC.SetItemTitle(h, titles["schinese"]);
            if (descriptions.TryGetValue("schinese", out var zh)) SteamUGC.SetItemDescription(h, zh);
            SteamUGC.SetItemContent(h, folder);
            SteamUGC.SetItemPreview(h, preview);
            SteamUGC.SetItemVisibility(h, visibility);
            SteamUGC.SetItemTags(h, tags);
            if (!Submit(h, note, "内容")) return 1;

            // 其他语言：只提交标题和说明（Steam 按玩家的语言显示，没有的语言显示主语言）
            foreach (var lang in titles.Keys.Union(descriptions.Keys).Where(l => l != "schinese"))
            {
                h = SteamUGC.StartItemUpdate(new AppId_t(AppId), file);
                SteamUGC.SetItemUpdateLanguage(h, lang);
                if (titles.TryGetValue(lang, out var t)) SteamUGC.SetItemTitle(h, t);
                if (descriptions.TryGetValue(lang, out var d)) SteamUGC.SetItemDescription(h, d);
                if (!Submit(h, null, lang)) return 1;
            }
            // 必需物品：已经加过的再加一次 Steam 返回 DuplicateRequest，当成成功
            foreach (var r in requires)
            {
                var dep = Wait<AddUGCDependencyResult_t>(SteamUGC.AddDependency(file, new PublishedFileId_t(r.Id)));
                if (dep.m_eResult != EResult.k_EResultOK && dep.m_eResult != EResult.k_EResultDuplicateRequest)
                {
                    Console.WriteLine($"× 设置必需物品 {r.Name} 失败：{dep.m_eResult}");
                    return 1;
                }
                Console.WriteLine($"已设置必需物品 {r.Name}");
            }
            Console.WriteLine($"完成：https://steamcommunity.com/sharedfiles/filedetails/?id={id}");
            return 0;
        }
        finally
        {
            SteamAPI.Shutdown();
        }
    }

    private static bool Submit(UGCUpdateHandle_t h, string note, string what)
    {
        var call = SteamUGC.SubmitItemUpdate(h, note);
        var result = Wait<SubmitItemUpdateResult_t>(call, () =>
        {
            var status = SteamUGC.GetItemUpdateProgress(h, out ulong done, out ulong total);
            return total > 0 ? $"{status} {done * 100 / total}%" : status.ToString();
        });
        if (result.m_eResult != EResult.k_EResultOK)
        {
            Console.WriteLine($"× 提交{what}失败：{result.m_eResult}");
            return false;
        }
        Console.WriteLine($"已提交{what}");
        if (result.m_bUserNeedsToAcceptWorkshopLegalAgreement) WarnAgreement(result.m_nPublishedFileId.m_PublishedFileId);
        return true;
    }

    private static void WarnAgreement(ulong id) =>
        Console.WriteLine($"! 你的账号还没接受创意工坊协议，接受前条目对别人不可见：https://steamcommunity.com/sharedfiles/workshoplegalagreement （条目 {id}）");

    // 等 Steam 回调；progress 用来打印进度
    private static T Wait<T>(SteamAPICall_t call, Func<string> progress = null) where T : struct
    {
        T result = default;
        bool done = false, failed = false;
        var cr = CallResult<T>.Create((r, ioFailure) => { result = r; failed = ioFailure; done = true; });
        cr.Set(call);
        string last = null;
        var start = DateTime.Now;
        while (!done)
        {
            SteamAPI.RunCallbacks();
            if (progress != null)
            {
                string p = progress();
                if (p != last) Console.WriteLine("  " + p);
                last = p;
            }
            if (DateTime.Now - start > TimeSpan.FromMinutes(10)) throw new TimeoutException("Steam 10 分钟没有响应");
            Thread.Sleep(100);
        }
        if (failed) throw new IOException("和 Steam 通信失败");
        return result;
    }

    private static ERemoteStoragePublishedFileVisibility ParseVisibility(string v) => (v ?? "private").ToLowerInvariant() switch
    {
        "public" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic,
        "friends" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly,
        "unlisted" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted,
        _ => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate,
    };

    private static string FirstNote(string folder)
    {
        string manifest = Path.Combine(folder, "dbmod.json");
        string version = File.Exists(manifest) ? JObject.Parse(File.ReadAllText(manifest)).Value<string>("Version") : "";
        return $"首次发布 / First release {version}".Trim();
    }

    // 更新说明：条目 dbmod.json 的版本 + 对应插件 CHANGELOG.md（中文）和 CHANGELOG.en.md（英文）里这一版的条目；
    // 给了 since 时带上 since 之后（不含）的每一版，每版前面写版本号
    private static string ChangeNote(string root, string folder, string since)
    {
        string manifest = Path.Combine(folder, "dbmod.json");
        if (!File.Exists(manifest)) return "";
        var m = JObject.Parse(File.ReadAllText(manifest));
        string version = m.Value<string>("Version");
        string plugin = m.Value<string>("Type") == "runtime" ? "DBModKit" : m.Value<string>("Name");
        var entries = new[] { "CHANGELOG.md", "CHANGELOG.en.md" }
            .Select(f => since == null ? ChangelogEntry(Path.Combine(root, plugin, f), version) : ChangelogSince(Path.Combine(root, plugin, f), since))
            .Where(e => e.Length > 0);
        string head = since == null ? version : $"{since} → {version}";
        return $"{head}\n{string.Join("\n\n", entries)}".Trim();
    }

    private static string ChangelogSince(string log, string since)
    {
        if (!File.Exists(log)) return "";
        var lines = File.ReadAllLines(log).SkipWhile(l => !l.StartsWith("## ")).TakeWhile(l => l.Trim() != "## " + since)
            .Select(l => l.StartsWith("## ") ? $"[b]{l.Substring(3).Trim()}[/b]" : l);
        return ToBBCode(string.Join("\n", lines));
    }

    private static string ChangelogEntry(string log, string version)
    {
        if (!File.Exists(log)) return "";
        var lines = File.ReadAllLines(log).SkipWhile(l => l.Trim() != "## " + version).Skip(1).TakeWhile(l => !l.StartsWith("## "));
        return ToBBCode(string.Join("\n", lines));
    }

    // 更新日志是 Markdown，Steam 的更新说明用 BBCode：粗体换成 [b]，去掉代码的反引号
    private static string ToBBCode(string text)
    {
        text = System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\*\*(.+?)\*\*", "[b]$1[/b]");
        return text.Replace("`", "");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DungeonBrawlMods.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("找不到仓库根目录（DungeonBrawlMods.sln）");
    }
}
