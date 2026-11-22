# 地下城竞技场 Mods 协作规程

## 先读

- `DungeonBrawlMods.sln` 包含 DBModKit、AutoForge、AutoPickup、DamageMeter、DBStudio、Hextech、SmartMove、SmartSkill。只有 DBModKit 直接引用游戏代码，游戏绑定使用的混淆名只放在 `DBModKit/GameRaw.cs`、`Hooks.cs`、`Compat.cs`。其他 Mod 通过 DBModKit 公开接口访问游戏。
- 读 `README.md` 的「架构」「DBModKit 接口速览」「开发」（版本号和发布段落）。写 Mod 时查 `docs/api/` 和 `templates/DBMod/`，插件直接继承 `BaseUnityPlugin`，用 `ModContext` 注册与清理，保持 ScriptEngine 热重载可用。
- 做游戏适配时另读 `README.md` 的「游戏更新后」和 `tools/Remap/README.md`。仓库外的逆向资料在 `E:\test\地下城竞技场\tools\NOTES.md` 和 `E:\test\地下城竞技场\decompiled-<版本>\`，程序集快照在 `E:\test\地下城竞技场\backup\<版本>\`。
- NOTES 中的未确认项要对照当前版源码或 IL 核实。改 NOTES 前先在工作区的本地 git 提交一次（`git -C E:\test\地下城竞技场 commit -am "改 NOTES 前：<任务名>"`），不再另存 `.bak`；保留文件编码和换行。

## 编译、部署和测试

- 一律在 PowerShell 7（`pwsh`）中编译。Git Bash 会吞掉 `-p:DeployDir=E:\dbtest702` 的反斜杠。
- 正式游戏目录 `D:\Steam\steamapps\common\地下城竞技场` **只读**。可以从 `DungeonBrawl_Data\Managed` 读取程序集。绝不部署或改动该目录，保留 `BepInEx\DBMods.nodeploy`。
- 只向测试副本 `E:\dbtest702` 部署。它的 `DungeonBrawl_Data` 是指向正式游戏的目录联接，同样只读。测试副本和正式游戏共用 `$env:USERPROFILE\AppData\LocalLow\UndergroundCat\DungeonBrawl`，禁止手改、覆盖或删除存档，禁止调用写存档功能。
- 任务要求编译并部署时，在 worktree 根目录运行下列命令。先确认测试游戏已关，避免 DBModKit DLL 被占用。只编译时用 `-p:DeployToGame=false`。

```powershell
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$ilspy = "$env:USERPROFILE\.dotnet\tools\ilspycmd.exe"
$managed = 'D:\Steam\steamapps\common\地下城竞技场\DungeonBrawl_Data\Managed'
dotnet build DungeonBrawlMods.sln -c Release -p:DeployDir=E:\dbtest702 -p:ForceDeploy=true
```

- 核对 DLL 时间戳和构建输出。DBModKit 在 `E:\dbtest702\BepInEx\plugins\DBModKit\DBModKit.dll`，其余启用热重载的 Mod 在 `E:\dbtest702\BepInEx\scripts\`（测试副本已装 ScriptEngine）。`GameDir` 是编译引用来源，`DeployDir` 才是部署目标。
- 任务要求主菜单检查时，保持 Steam 开着，启动 `E:\dbtest702\DungeonBrawl.exe`。用本次启动的 `E:\dbtest702\BepInEx\LogOutput.log` 核对 DBModKit 版本、游戏版本、各 Mod 加载及兼容检查。可用存档目录下 `Player.log` 的 `[Startup] Main menu initialized.` 确认到达主菜单。
- 查 Warning、Error、Exception、找不到、不兼容、Harmony 补丁失败。开发副本因 ScriptEngine、UnityExplorer 等而显示「打榜资格：不能上传」是预期信息。只看主菜单，不点击、不进局，核对路径后结束本次启动的 PID。
- 打榜资格离线测试见 `tools/tests/Ranked/README.md`。从仓库根运行 `dotnet run --project tools/tests/Ranked/Ranked.Tests.csproj -c Release`，该测试不启动游戏或部署。

## 固定检查

- 改过 `Compat.cs` 时，用以下 PowerShell 命令查字典初始化器重复键，输出必须为空。手动运行任务书里的验收命令。

```powershell
Select-String -Path DBModKit/Compat.cs -Pattern '^\s*\["(?<key>[A-Za-z_0-9]+)"\]\s*=' |
    ForEach-Object { $_.Matches[0].Groups['key'].Value } |
    Group-Object -CaseSensitive | Where-Object Count -gt 1 | Select-Object -ExpandProperty Name
```

- 新用到的混淆成员要加进 Compat 检查。改反射或 Harmony 目标时，核对所属类型、完整参数与返回类型、字段及嵌套类型。补丁要落在游戏真实调用链上，沿调用者继续溯源，留意只有诱饵调用的候选。字段和方法同名造成「方法组」错误时，核实后参照 `GameRaw` 的 `AccessTools.FieldRefAccess`。
- Remap 先预览再写回。按准确的声明类型和签名查看 `mapping.tsv`，逐项核对 Compat 的元组成员数组、字符串类型键、嵌套类型和 `CheckSignature`。检查字符串 diff，撤销 JSON 字段、GameObject 名、语言代码等普通字符串的误改。
- 提交前运行 `git diff --check` 和 `git status`，核对改动范围。游戏 DLL、游戏资源和反编译代码留在仓库外，也不得进入发布包。

## 版本、发版和权限

- 在指派的 worktree 和分支工作。不要改 `main` 或用户的主工作目录 `E:\test\地下城竞技场\mods`。用仓库现有 Git 提交身份，不改配置。不 push、不合并、不开 PR、不发 Release、不上传工坊。
- 只在任务要求时改版本。整套发版用两段式 `VERSION`，改过并随这次发版发布的插件把源码 `Version` 对齐为 `<VERSION>.0`，未改的版本保持原样。版本号只能递增。
- 对每个发版的插件同步 `CHANGELOG.md` 和 `CHANGELOG.en.md`。用玩家能理解的文字写改动、升级操作和验证范围，主菜单加载只算启动检查。已知问题和新增未实测功能同步到中英 FAQ。
- 整套发版时在根 `CHANGELOG.md` 中英两段顶部都追加本版改动，重写 `RELEASE_NOTES.md`（本版升级步骤、改动、兼容表，英文折叠块同步）。局部开发任务按任务书处理插件日志，整套发版文件留给发版任务。
- 发版任务用 `pwsh ./pack.ps1` 检查日志、对照表和发版历史。核对 `dist/` 下 full、plugins ZIP 和工坊目录。普通开发任务若因缺整套发版材料而打包失败，照实报告。

## 交回

- 写到任务指定路径。默认放在仓库外 `E:\test\地下城竞技场\tools\codex-tasks\<任务名>.result.md`，游戏适配任务使用 `E:\test\地下城竞技场\tools\game<去点版本>-handoff.md`。
- 写清完成的改动和依据、分支与提交号。贴 `git status`、`git log --oneline origin/main..HEAD`、`git show --stat HEAD` 和验收命令的原文输出。无输出的检查注明为空，运行过游戏时贴关键加载行和全部相关警告、错误。
- 列出没做的项目、未确认的结论和验证范围。报告位置和结果摘要写入最后回复。
