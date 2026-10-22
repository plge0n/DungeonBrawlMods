# 地下城竞技场 Mods

BepInEx 5 插件，目标游戏：地下城竞技场（Steam 4246640，Unity 2022.3 Mono）。

| 项目 | 说明 |
|---|---|
| [DBModKit](DBModKit) | 依赖库：游戏接口、自动释放钩子、事件、原生风格 UI、JSON 配置、兼容检查 |
| [SmartSkill](SmartSkill) | 给"右键技能图标自动释放"加上每个技能单独的释放条件 |
| [DBStudio](DBStudio) | 游戏内的 Mod 工作台（F9）：调试面板、规则、强化（海克斯式三选一）、数值编辑器 |

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
| `ModContext` | Mod 运行上下文：`OnUpdate`、`OnBattleStarted`、`UseAutoCast`、`LoadJson` 等，卸载时自动清理 |
| `ModEvents` | 每帧、进入/离开战斗、退出游戏 |
| `NativeUi` | 用游戏素材拼的面板、开关、滑条、下拉框、按钮，以及大窗口、页签、滚动列表、输入框 |
| `GameInput` / `GamePause` | 打开自己的窗口时屏蔽游戏输入、暂停游戏（都要 Dispose 释放） |
| `GameEvents` | 战斗事件：击杀、放技能、暴击、受伤、升级、波次开始/结束等 |
| `ChoicePanel` / `RelicInfo` | 用游戏的三选一界面显示自定义选项；遗物列表、发遗物 |
| `GameData` | 改技能、英雄、怪物配置表里的数值，可随时整体恢复原版 |
| `JsonConfig<T>` | `BepInEx/config` 下的 JSON 配置，自动延迟保存 |
| `ScreenNotice` | 屏幕顶部临时提示 |

## 开发
需要 .NET SDK 8+，游戏目录已装 BepInEx 5。

```powershell
pwsh tools/devtools.ps1                         # 一次性：安装 UnityExplorer + ScriptEngine（-Remove 卸载）
pwsh tools/new-mod.ps1 MyMod -Description "…"   # 新建依赖 DBModKit 的 Mod 并加入解决方案
dotnet build DungeonBrawlMods.sln -c Release    # 编译并部署到游戏
pwsh ./pack.ps1                                 # 生成 dist/ 下的发布包（包含解决方案里的所有插件）
```

**热重载**：装了 ScriptEngine 后，普通 Mod 会部署到 `BepInEx/scripts`，游戏开着也能编译，约 1 秒后自动重载（也可按 F6）。DBModKit 始终在 `BepInEx/plugins`，改了它要重启游戏（编译前先关游戏，否则 dll 被占用）。没装 ScriptEngine 时一律部署到 `plugins`。

**DBStudio**：游戏内按 F9，游戏风格的调试面板：玩家属性、技能栏、敌人、事件日志、已加载插件；单人时有满血、满蓝、加金币等测试操作。规则页可以不写代码做"当……如果……就……"的玩法，强化页可以做波次结束时三选一的强化，数值页可以改技能、英雄、怪物数值，改动都存成玩法包（`BepInEx/config/DBStudio/packs/`），只在单人时生效。

**UnityExplorer**：游戏内按 F7。可以浏览场景里的 UI 和对象、查看/修改字段、在 C# 控制台里执行代码，是找游戏对象和调 UI 的首选工具。

- 游戏不在默认位置：设置环境变量 `GameDir`，或加 `-p:GameDir="X:\...\地下城竞技场"`。
- 公共构建配置在 `Directory.Build.props/.targets`：Mod 项目只需写 `AssemblyName` 和对 DBModKit 的 `ProjectReference`（`Private="false"`）。模板在 `templates/DBMod`。
- 发布新版本：改源码里的 `Version` 常量 → 在对应目录的 `CHANGELOG.md` 加条目 → `pack.ps1`（缺少更新日志条目时会报错）。

## 游戏更新后
1. 进游戏看 `BepInEx/LogOutput.log`：DBModKit 会列出找不到的成员。
2. 用 `../_DungeonBrawl_tools/` 里的工具重新解密字符串、反编译，对照 `../_DungeonBrawl_tools/NOTES.md` 找到新名字。注意混淆器会生成诱饵方法，要靠调用关系确认真方法。
3. 修改 DBModKit 的 `GameRaw.cs`、`Hooks.cs` 和 `Compat.cs` 的检查清单，把新版本号加进 `Compat.TestedVersions`。

**不要**把游戏的 dll、反编译出来的源码放进本仓库或发布包。
