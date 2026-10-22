# 地下城竞技场 Mods

BepInEx 5 插件，目标游戏：地下城竞技场（Steam 4246640，Unity 2022.3 Mono）。

## SmartSkill
给游戏自带的"右键技能图标自动释放"加上每个技能单独的释放条件。玩家说明见 [安装说明.txt](安装说明.txt)，更新记录见 [CHANGELOG.md](CHANGELOG.md)。

| 文件 | 作用 |
|---|---|
| `Plugin.cs` | 入口：版本号、配置、兼容性检查、每帧 Runner |
| `Compat.cs` | 启动时用反射检查依赖的游戏成员，不兼容就整体停用 |
| `GameApi.cs` | **所有混淆名只出现在这里**（以及 Compat 的检查清单） |
| `Patches.cs` | Harmony 补丁：`ez.dxx` 决定放不放，`ez.dxy` 决定打谁 |
| `SkillBrain.cs` | 按规则判断 |
| `SkillRule.cs` | 规则模型与 JSON 存取（`BepInEx/config/SmartSkill.rules.json`） |
| `Recommend.cs` | "按标签推荐"生成的默认规则 |
| `NativeUi.cs` | 从游戏 UI 预制体克隆控件、取贴图 |
| `SkillPopup.cs` | 齿轮按钮与设置弹窗 |

## 开发
需要 .NET SDK 8+，游戏目录已装 BepInEx 5。

```powershell
dotnet build SmartSkill -c Release              # 编译并复制到游戏的 BepInEx/plugins（需先关闭游戏）
dotnet build SmartSkill -c Release -p:GameDir="X:\...\地下城竞技场"   # 游戏不在默认位置
pwsh ./pack.ps1                                 # 生成 dist/ 下的发布包
```

发布新版本：改 `Plugin.Version` → 在 `CHANGELOG.md` 加对应条目 → `pack.ps1`（缺少更新日志条目时会报错）。

## 游戏更新后
游戏代码经过 GUPS 混淆，每次更新混淆名都可能全部改变。

1. 进游戏看日志：`Compat` 会列出找不到的成员。
2. 用 `../_DungeonBrawl_tools/` 里的工具重新解密字符串、反编译，对照 `../_DungeonBrawl_tools/NOTES.md` 找到新名字。注意混淆器会生成诱饵方法，要靠调用关系确认真方法。
3. 修改 `GameApi.cs`、`Patches.cs` 里的名字和 `Compat.cs` 的检查清单，把新版本号加进 `Compat.TestedVersions`。

**不要**把游戏的 dll、反编译出来的源码放进本仓库或发布包。
