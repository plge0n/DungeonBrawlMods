# 地下城竞技场 Mods

BepInEx 5 插件，目标游戏：地下城竞技场（Steam 4246640，Unity 2022.3 Mono）。

| 项目 | 说明 |
|---|---|
| [DBModKit](DBModKit) | 依赖库：游戏接口、自动释放钩子、事件、原生风格 UI、JSON 配置、兼容检查 |
| [SmartSkill](SmartSkill) | 给"右键技能图标自动释放"加上每个技能单独的释放条件 |
| [DBStudio](DBStudio) | 游戏内的 Mod 工作台（F9）：调试面板、规则、强化（海克斯式三选一）、数值编辑器、英雄皮肤 |
| [SmartMove](SmartMove) | 自动躲避地面预警（F9 开关） |
| [AutoPickup](AutoPickup) | 战斗中自动拾取身边能捡的掉落物（和按 C 一样），可按类别过滤 |
| [DamageMeter](DamageMeter) | 伤害统计：按普攻、技能、召唤物分开统计本波和本局的伤害与 DPS；Steam 联机时显示装了它的队友的伤害 |

玩家说明见 [安装说明.txt](安装说明.txt)。

## 架构
```
SmartSkill 等 Mod   只写玩法逻辑，不引用游戏程序集
      │ [BepInDependency]
DBModKit            唯一接触游戏混淆代码的一层
      │
BepInEx 5
```
游戏代码经过 GUPS 混淆，每次更新混淆名都可能全部改变。所有混淆名集中在 DBModKit 的 `GameRaw.cs`、`Hooks.cs`（补丁签名）和 `Compat.cs`（检查清单）。游戏更新后只需修 DBModKit，已编译的 Mod 不用改。

## DBModKit 接口速览
Mod 直接继承 `BaseUnityPlugin`，通过 `ModContext` 注册的事件、处理器、配置会在卸载时自动清理（热重载需要）。
**不要**继承 DBModKit 里的类作为插件基类：ScriptEngine 解析不到继承链，热重载会崩溃。
```csharp
[BepInPlugin(Guid, Name, Version)]
[BepInDependency(ModKitPlugin.Guid, ModKit.Version)]
public class Plugin : BaseUnityPlugin
{
    private ModContext mod;
    private void Awake() => mod = ModContext.Start(this, Logger, OnLoad, OnUnload);
    private void OnDestroy() => mod?.Dispose();

    private void OnLoad(ModContext mod)           // ModKit 不兼容时不会调用
    {
        mod.OnUpdate(() =>
        {
            var player = Game.LocalPlayer;            // 不在战斗中为 null
            if (player == null) return;
            var boss = player.PickEnemy(10f, e => e.IsBoss ? 0 : 1);
        });
        mod.UseAutoCast((slot, player) =>
            slot.Info.HasTag(SkillTags.Heal) && player.HpPercent > 0.5f
                ? AutoCastVerdict.Skip("血量充足")
                : AutoCastVerdict.Vanilla());
    }

    private void OnUnload() { }                   // 销毁自己挂在游戏对象上的东西
}
```
| 类型 | 作用 |
|---|---|
| `Game` | 本地玩家、是否在战斗、房间状态（`IsSolo` 等）、本地化文本 |
| `Role` / `Player` | 血量、属性、位置、Boss/精英、距离判定；玩家的蓝量、金币、范围内敌人、按评分选敌人；本地玩家回血/加蓝/加金币 |
| `SkillInfo` | 技能表数据：名字、标签、指示器、施法范围、冷却 |
| `SkillSlot` / `SkillBar` | 技能栏里的技能：自动释放开关、剩余冷却、消耗、图标 |
| `AutoCast` | 接管自动释放：决定放不放、打谁；`Resolved` 事件报告结果 |
| `MoveControl` | 接管没有方向键输入时的走位（等同按住方向键） |
| `Arena` | 地面预警（形状、剩余时间）、场地边界 |
| `Combat` | 命中事件：伤害、是否打死、来源技能、是不是普攻 |
| `ModContext` | Mod 运行上下文：`OnUpdate`、`OnBattleStarted`、`UseAutoCast`、`LoadJson` 等，卸载时自动清理 |
| `ModEvents` | 每帧、进入/离开战斗、退出游戏 |
| `NativeUi` | 用游戏素材拼的面板、开关、滑条、下拉框、按钮，以及大窗口、页签、滚动列表、输入框 |
| `GameInput` / `GamePause` | 打开自己的窗口时屏蔽游戏输入、暂停游戏（都要 Dispose 释放） |
| `GameEvents` | 战斗事件：击杀、放技能、暴击、受伤、升级、波次开始/结束等 |
| `Loot` | 地上的掉落物（类别、位置、自己能不能捡）；替本地玩家拾取，范围和按 C 一样不超过 5 米 |
| `ChoicePanel` / `RelicInfo` | 用游戏的三选一界面显示自定义选项；遗物列表、发遗物 |
| `ModSettings` | 往 DBStudio 的 Mod 页里挂自定义设置区块 |
| `GameData` | 改技能、英雄、怪物配置表里的数值，可随时整体恢复原版 |
| `PassiveSkills` | 自定义被动技能：注册后进技能书三选一、占技能格，行为由 Mod 写（`mod.RegisterPassive`） |
| `HeroSkins` | 给每个英雄选一套工坊或本地的模型（或原版），可以只换本地玩家；Steam 联机时通过大厅成员数据同步 |
| `LobbySync` | 通过 Steam 大厅成员数据和其他装了 Mod 的玩家交换少量文字数据（局域网不可用） |
| `JsonConfig<T>` | `BepInEx/config` 下的 JSON 配置，自动延迟保存 |
| `ScreenNotice` | 屏幕顶部临时提示 |
| `ModText` / `Localization` | 多语言文本：代码里写中文原文，翻译放在插件旁的 `lang/<语言>.json`，跟随游戏语言（见下） |
| `RunLog` | 运行记录：所有 Mod 共用、带波次和帧号的结构化日志，写进 `BepInEx/DBModKit/trace.log`；可以加标记、导出诊断报告（见下） |

### 运行记录（RunLog）
用来确认功能有没有生效、出了问题能定位到当时发生了什么。
```csharp
mod.RunLog.Info("规则", "触发了", ("生命", player.Hp), ("目标", "Boss"));   // 来源自动是 Mod 名
mod.RunLog.Debug("躲避", "换落脚点");                                      // 高频细节用 Debug
```
- 每条记录：时间、级别、`[来源/分类]`、内容、`键=值`、波次、帧号。内存保留最近 3000 条，调试页（F9）能看，同时写进 `BepInEx/DBModKit/trace.log`，上一次启动的记录在 `trace.prev.log`。Warning/Error 也会进 `LogOutput.log`。
- ModKit 自动记录：Mod 加载/卸载、进出战斗、房间人数、`Player` 的回血/加蓝/加金币/发遗物/改属性（前后数值，以及是哪个 Mod 调的）、配置表修改、三选一、被拦下的异常；Debug 级：每个战斗事件、自动释放决策变化、输入屏蔽和暂停。所以 Mod 自己只需要记"为什么这么做"。
- 测试时：在 Mod 页把 DBModKit 的 `FileLevel` 改成 `Debug`，文件里就有全部细节。看到问题按 **Ctrl+F10** 加一个编号标记（调试页也有按钮），之后说"标记 3 附近"就能在文件里找到。
- 反馈问题：调试页点"导出"，生成 `BepInEx/DBModKit/report-时间.txt`（版本、插件列表、兼容检查、当前状态、全部记录），把它发过来。

### 多语言（ModText）
代码里直接写中文原文当默认值；要支持其他语言时，在插件 dll 旁边放 `lang/en.json`、`lang/ja.json` 等（平铺的 键 → 文本）。
`csharp
NativeUi.Label(parent, mod.Text["debug.title", "调试"]);
mod.Text.Format("rule.fired", "规则「{0}」触发", rule.Name);
`
- 语言跟随游戏设置（`Localization.Language`：zh-CN、zh-TW、en、ja……），玩家换语言时触发 `Localization.Changed`。
- 查找顺序：当前语言 → 英文（游戏不是中文时）→ 代码里的中文。没有翻译文件时一切照旧。
- 现有 Mod 的界面文字还没有改成这种写法，以后翻译时逐步替换。

## 开发
需要 .NET SDK 8+，游戏目录已装 BepInEx 5。

```powershell
pwsh tools/devtools.ps1                         # 一次性：安装 UnityExplorer + ScriptEngine（-Remove 卸载）
pwsh tools/new-mod.ps1 MyMod -Description "…"   # 新建依赖 DBModKit 的 Mod 并加入解决方案
dotnet build DungeonBrawlMods.sln -c Release    # 编译并部署到游戏
pwsh ./pack.ps1                                 # 生成 dist/ 下的发布包（包含解决方案里的所有插件）
```

**热重载**：装了 ScriptEngine 后，普通 Mod 会部署到 `BepInEx/scripts`，游戏开着也能编译，约 1 秒后自动重载（也可按 F6）。DBModKit 始终在 `BepInEx/plugins`，改了它要重启游戏（编译前先关游戏，否则 dll 被占用）。没装 ScriptEngine 时一律部署到 `plugins`。

**DBStudio**：游戏内按 F9，游戏风格的调试面板：玩家属性、技能栏、敌人、事件日志、已加载插件；单人时有满血、满蓝、加金币等测试操作。规则页可以不写代码做"当……如果……就……"的玩法，强化页可以做波次结束时三选一的强化，数值页可以改技能、英雄、怪物数值，改动都存成玩法包（`BepInEx/config/DBStudio/packs/`），只在单人时生效。皮肤页给每个英雄挑一套已订阅的工坊模型或 `BepInEx/skins/` 下的本地模型，只改显示、不影响玩法；Steam 联机时也装了 Mod 的队友能互相看到对方的工坊皮肤。

**UnityExplorer**：游戏内按 F7。可以浏览场景里的 UI 和对象、查看/修改字段、在 C# 控制台里执行代码，是找游戏对象和调 UI 的首选工具。

- 游戏不在默认位置：设置环境变量 `GameDir`，或加 `-p:GameDir="X:\...\地下城竞技场"`。
- 公共构建配置在 `Directory.Build.props/.targets`：Mod 项目只需写 `AssemblyName` 和对 DBModKit 的 `ProjectReference`（`Private="false"`）。模板在 `templates/DBMod`。
- 发布新版本：改源码里的 `Version` 常量 → 在对应目录的 `CHANGELOG.md` 加条目 → `pack.ps1`（缺少更新日志条目时会报错）。

## 创意工坊
游戏的工坊不加载 dll。DBModKit 自带加载器：订阅的条目里有 `dbmod.json`（`"Type": "plugin"`）就当成 BepInEx 插件加载，
第一次加载前会弹窗让玩家确认。BepInEx 和 DBModKit 本身仍要装进游戏目录（Doorstop 的 `winhttp.dll` 只能放在那里）。

```json
{ "Format": 1, "Type": "plugin", "Name": "SmartSkill", "Author": "…", "Version": "0.7.0",
  "Description": "…", "MinModKit": "0.9.0", "Assemblies": ["SmartSkill.dll"] }
```
- `pack.ps1` 会在 `dist/workshop/<插件>/` 生成条目文件夹（dll + dbmod.json）。
- 上传前测试：把条目文件夹复制到 `BepInEx/workshop-dev/`，并把 `plugins`/`scripts` 里同名的插件挪走（本地已装的优先）。
- 条目里不要放 `manifest.json`：那是游戏自己的格式（英雄模型等），游戏会去读它。

### 上传
- 条目的标题、说明（按语言）、可见性、预览图在 `workshop/items.json`；第一次上传后条目编号会写回这个文件，要提交。
- `Runtime` 条目（BepInEx + DBModKit）：游戏工坊不加载 dll，玩家订阅后运行条目里的 `install.bat` 把文件复制进游戏目录，`uninstall.bat` 卸载。
  安装器模板在 `workshop/Runtime/`，`pack.ps1` 生成 `dist/workshop/Runtime/`。DBModKit 发现工坊里的运行环境比装的新时会在游戏里提示。
- 上传（Steam 客户端要开着并登录拥有游戏的账号，不需要密码）：
  ```powershell
  pwsh ./pack.ps1
  dotnet run -c Release --project tools/WorkshopUpload -- Runtime --dry-run   # 只检查
  dotnet run -c Release --project tools/WorkshopUpload -- Runtime             # 上传或更新
  ```

## 游戏更新后
游戏每次更新都会重新混淆，混淆名全部改变。`tools/Remap` 自动把 DBModKit 源码里的旧名字对应到新版（详见 [tools/Remap/README.md](tools/Remap/README.md)）：
```powershell
dotnet build tools/Remap -c Release
$remap = "tools/Remap/bin/Release/net8.0/Remap.dll"
dotnet $remap update --old ../_DungeonBrawl_backup/6.45 --notes ../_DungeonBrawl_tools/NOTES.md          # 只检查，看 tools/Remap/out/report.md
dotnet $remap update --old ../_DungeonBrawl_backup/6.45 --notes ../_DungeonBrawl_tools/NOTES.md --write  # 写回源码和笔记
```
1. 先跑一遍不带 `--write` 的，看报告里"需要人工处理"和标 ⚠ 的项，必要时对照反编译代码（`../_DungeonBrawl_tools/` 里的工具重新解密字符串、反编译）。
2. 加 `--write` 写回，检查 `git diff`，编译，进游戏看 `BepInEx/LogOutput.log`（DBModKit 会列出找不到的成员），实机测一遍。
3. 把新版本号加进 `Compat.TestedVersions`，然后 `dotnet $remap snapshot --version <新版本>` 保存这一版，作为下次的 `--old`。

**不要**把游戏的 dll、反编译出来的源码放进本仓库或发布包。
