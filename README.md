# 地下城竞技场 Mods

BepInEx 5 插件，目标游戏：地下城竞技场（Steam 4246640，Unity 2022.3 Mono）。

| 项目 | 说明 |
|---|---|
| [DBModKit](DBModKit) | 依赖库：游戏接口、自动释放钩子、事件、原生风格 UI、JSON 配置、兼容检查 |
| [SmartSkill](SmartSkill) | 给"右键技能图标自动释放"加上每个技能单独的释放条件 |
| [DBStudio](DBStudio) | 游戏内的 Mod 工作台（F9）：调试面板、规则、强化（海克斯式三选一）、数值编辑器、英雄皮肤 |
| [SmartMove](SmartMove) | 自动躲避地面预警（F8 开关） |
| [AutoPickup](AutoPickup) | 战斗中自动拾取身边能捡的掉落物（和按 C 一样），可按类别过滤 |
| [DamageMeter](DamageMeter) | 伤害统计：按普攻、技能、召唤物分开统计本波和本局的伤害与 DPS；Steam 联机时显示装了它的队友的伤害 |
| [Hextech](Hextech) | 海克斯强化：每局 4 次三选一，专属技能强化 + 参照海克斯大乱斗、斗魂竞技场的通用卡和队友卡（联机时要每个人装的一样或房主允许） |

玩家说明见 [安装说明.txt](安装说明.txt)（英文 [INSTALL-EN.txt](INSTALL-EN.txt)），当前版本的发版说明见 [RELEASE_NOTES.md](RELEASE_NOTES.md)。

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
完整的接口文档按模块分页放在 **[docs/api/](docs/api/README.md)**（用途、示例、联机时的行为、限制），包括接口约定（1.0 之后只增不改）。

| 想做什么 | 看哪里 |
|---|---|
| 读游戏状态、改本地玩家数值 | [游戏状态](docs/api/游戏状态.md)、[战斗事件](docs/api/战斗事件.md) |
| 自动释放、走位、调技能 | [自动释放与走位](docs/api/自动释放与走位.md) |
| 新技能、新被动 | [自定义技能](docs/api/自定义技能.md)、示例 [samples/SkillSample](samples/SkillSample) |
| 新遗物 | [自定义遗物](docs/api/遗物.md)、示例 [samples/SkillSample](samples/SkillSample)（铁壁徽章） |
| 新装备、给已有装备加特效 | [自定义装备](docs/api/装备.md)、示例 [samples/SkillSample](samples/SkillSample)（风暴法杖、静电） |
| "新英雄" | [英雄](docs/api/英雄.md)、示例 [samples/Frieren](samples/Frieren)（芙莉莲：无限魔力 + 杀人魔法） |
| 自己的图标、特效、动作 | [AssetBundle](docs/AssetBundle.md)、示例 [samples/BundleSkill](samples/BundleSkill) |
| 不写代码做 Mod（技能、被动、遗物、装备、变体） | [内容包](docs/内容包.md)、示例 [templates/ContentPack](templates/ContentPack)、可视化编辑 [内容包工作台](tools/ContentWorkbench) |
| 联机时该怎么办 | [联机](docs/api/联机.md) |
| 依赖别的 Mod、和别的 Mod 抢功能 | [依赖和冲突](docs/api/依赖和冲突.md) |
| 设置界面、窗口 | [界面](docs/api/界面.md)、[生命周期与基础设施](docs/api/生命周期.md) |
| 已安装的 Mod 和运行状态 | `ModList`（主菜单"Mod"按钮打开的列表也用它），见[界面](docs/api/界面.md#modlist) |

`samples/` 下的示例不在解决方案里，单独编译、默认不部署：`dotnet build samples/Frieren -c Release`（要放进游戏试就加 `-p:DeployToGame=true`）。

### 运行记录（RunLog）
功能有没有生效、出了问题当时发生了什么，看 `BepInEx/DBModKit/trace.log`。测试时把 DBModKit 的 `FileLevel` 改成 `Debug`，
看到问题按 **Ctrl+F10** 加标记；反馈问题时在调试页点"导出"，把生成的 `report-时间.txt` 发过来。详见[生命周期与基础设施](docs/api/生命周期.md#runlog)。

## 开发
需要 .NET SDK 8+，游戏目录已装 BepInEx 5。

```powershell
pwsh tools/devtools.ps1                         # 一次性：安装 UnityExplorer + ScriptEngine（-Remove 卸载）
pwsh tools/new-mod.ps1 MyMod -Description "…"   # 新建依赖 DBModKit 的 Mod 并加入解决方案（-WithSkills 带技能示例）
dotnet build DungeonBrawlMods.sln -c Release    # 编译并部署到游戏
pwsh ./pack.ps1                                 # 生成 dist/ 下的发布包（包含解决方案里的所有插件）
```

**热重载**：装了 ScriptEngine 后，普通 Mod 会部署到 `BepInEx/scripts`，游戏开着也能编译，约 1 秒后自动重载（也可按 F6）。DBModKit 始终在 `BepInEx/plugins`，改了它要重启游戏（编译前先关游戏，否则 dll 被占用）。没装 ScriptEngine 时一律部署到 `plugins`。

**部署检查**：几个工作目录（git worktree）共用一个游戏目录，后编译的会覆盖先部署的。所以部署前会检查：当前代码要包含 `origin/main` 的最新提交，也要包含游戏里这个项目上一次部署时的提交（记在 `BepInEx/DBMods.deploy/<项目名>.commit`，里面有提交、分支和工作目录）。不满足就跳过部署并给出警告，编译照常完成；先变基或合并，确定要覆盖时加 `-p:ForceDeploy=true`。测试部署逻辑时用 `-p:DeployDir=<别的目录>` 部署到别处，不碰真实游戏目录。

**开发和平时玩分开**：平时玩的游戏目录只装工坊版（运行环境 + 订阅的 Mod），在里面放一个空文件 `BepInEx\DBMods.nodeploy`，编译就不会再往里部署（`ForceDeploy` 也不行）。
开发用另一个游戏目录：复制 `DungeonBrawl.exe`、`UnityPlayer.dll`、`MonoBleedingEdge` 等，`DungeonBrawl_Data` 用目录联接（`mklink /J`）指向原游戏，加一个内容是 `4246640` 的 `steam_appid.txt`，
装 BepInEx 和开发工具（`tools/devtools.ps1`），然后把环境变量 `DeployDir` 设成这个目录（编译时引用的游戏程序集仍从 `GameDir` 读）。注意存档是共用的，测试前先备份。

**DBStudio**：游戏内按 F9，游戏风格的调试面板：玩家属性、技能栏、敌人、事件日志、已加载插件；单人时有满血、满蓝、加金币等测试操作。规则页可以不写代码做"当……如果……就……"的玩法，强化页可以做波次结束时三选一的强化，数值页可以改技能、英雄、怪物数值，改动都存成玩法包（`BepInEx/config/DBStudio/packs/`），只在单人、房间里每个人都装了同样的玩法包、或房主允许时生效。皮肤页给每个英雄挑一套已订阅的工坊模型或 `BepInEx/skins/` 下的本地模型，只改显示、不影响玩法；Steam 联机时也装了 Mod 的队友能互相看到对方的工坊皮肤。

**UnityExplorer**：游戏内按 F7。可以浏览场景里的 UI 和对象、查看/修改字段、在 C# 控制台里执行代码，是找游戏对象和调 UI 的首选工具。

- 游戏不在默认位置：设置环境变量 `GameDir`，或加 `-p:GameDir="X:\...\地下城竞技场"`。
- 公共构建配置在 `Directory.Build.props/.targets`：Mod 项目只需写 `AssemblyName` 和对 DBModKit 的 `ProjectReference`（`Private="false"`）。模板在 `templates/DBMod`。
- 版本号（0.31 起）：整套发版用两位小数（0.31、0.32……，根目录 `VERSION`）；这次发版改过的 Mod 把 `Version` 设成同一个号（`0.31.0`），
  没改的保持原样，下次改动时再对齐。版本号只能往上走（BepInEx 的依赖检查、工坊的更新提示都按大小比），开发中的版本可以照常递增，发版时再对齐。
- 发布新版本：改源码里的 `Version` 常量 → 在对应目录的 `CHANGELOG.md` 和 `CHANGELOG.en.md`（英文，创意工坊的更新说明会带上）加条目 → 整套发版时改根目录的 `VERSION`，在 `RELEASE_NOTES.md` 写中英文发版说明（包括哪些功能没实测）→ `pack.ps1`（缺少更新日志条目或发版说明时会报错）。
  发布包是 `dist/DungeonBrawlMods-<VERSION>-full.zip`（带 BepInEx）和 `-plugins.zip`，根目录放 `安装说明.txt`、`INSTALL-EN.txt`、`RELEASE_NOTES.md`。

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
- 条目的标题、说明（按语言）、可见性、预览图、必需物品（`Requires`，写别的条目名，比如 Mod 条目都写 `["Runtime"]`，订阅时 Steam 会提示一起订阅）在 `workshop/items.json`；第一次上传后条目编号会写回这个文件，要提交。
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
dotnet $remap update --old ../backup/7.01 --notes ../tools/NOTES.md          # 只检查，看 tools/Remap/out/report.md
dotnet $remap update --old ../backup/7.01 --notes ../tools/NOTES.md --write  # 写回源码和笔记
```
1. 先跑一遍不带 `--write` 的，看报告里"需要人工处理"和标 ⚠ 的项，必要时对照反编译代码（`../tools/` 里的工具重新解密字符串、反编译）。
2. 加 `--write` 写回，检查 `git diff`，编译，进游戏看 `BepInEx/LogOutput.log`（DBModKit 会列出找不到的成员），实机测一遍。
3. 把新版本号加进 `Compat.TestedVersions`，然后 `dotnet $remap snapshot --version <新版本>` 保存这一版，作为下次的 `--old`。

**不要**把游戏的 dll、反编译出来的源码放进本仓库或发布包。

## 许可证
本仓库的代码和文档使用 [MIT 许可证](LICENSE)。完整包和运行环境条目里再分发的 BepInEx、Doorstop（LGPL-2.1）和 HarmonyX、MonoMod、Mono.Cecil（MIT）
按各自的许可证分发，见 [workshop/Runtime/licenses](workshop/Runtime/licenses)。游戏本身的代码和资源不在本仓库里。
