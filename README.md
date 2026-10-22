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
| [Hextech](Hextech) | 海克斯强化：每局 4 次三选一，自动生成强化英雄专属技能的卡（只在单人时生效） |

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
完整的接口文档按模块分页放在 **[docs/api/](docs/api/README.md)**（用途、示例、联机时的行为、限制），包括接口约定（1.0 之后只增不改）。

| 想做什么 | 看哪里 |
|---|---|
| 读游戏状态、改本地玩家数值 | [游戏状态](docs/api/游戏状态.md)、[战斗事件](docs/api/战斗事件.md) |
| 自动释放、走位、调技能 | [自动释放与走位](docs/api/自动释放与走位.md) |
| 新技能、新被动 | [自定义技能](docs/api/自定义技能.md)、示例 [samples/SkillSample](samples/SkillSample) |
| "新英雄" | [英雄](docs/api/英雄.md)、示例 [samples/Frieren](samples/Frieren)（芙莉莲：无限魔力 + 杀人魔法） |
| 自己的图标、特效、动作 | [AssetBundle](docs/AssetBundle.md)、示例 [samples/BundleSkill](samples/BundleSkill) |
| 不写代码做 Mod | [内容包](docs/内容包.md)、示例 [templates/ContentPack](templates/ContentPack) |
| 联机时该怎么办 | [联机](docs/api/联机.md) |
| 设置界面、窗口 | [界面](docs/api/界面.md)、[生命周期与基础设施](docs/api/生命周期.md) |

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

**DBStudio**：游戏内按 F9，游戏风格的调试面板：玩家属性、技能栏、敌人、事件日志、已加载插件；单人时有满血、满蓝、加金币等测试操作。规则页可以不写代码做"当……如果……就……"的玩法，强化页可以做波次结束时三选一的强化，数值页可以改技能、英雄、怪物数值，改动都存成玩法包（`BepInEx/config/DBStudio/packs/`），只在单人、房间里每个人都装了同样的玩法包、或房主允许时生效。皮肤页给每个英雄挑一套已订阅的工坊模型或 `BepInEx/skins/` 下的本地模型，只改显示、不影响玩法；Steam 联机时也装了 Mod 的队友能互相看到对方的工坊皮肤。

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
