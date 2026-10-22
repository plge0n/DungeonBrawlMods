# DBModKit 接口文档

每页讲一组模块：用途、示例、联机时的行为、限制。完整成员看源码里的 XML 注释（IDE 里悬停就能看到）。
可以直接编译的完整 Mod 在 [samples/](../../samples)，不写代码的做法见 [内容包](../内容包.md)。

| 页面 | 模块 |
|---|---|
| [生命周期与基础设施](生命周期.md) | `ModContext` `ModEvents` `JsonConfig` `ModSettings` `RunLog` `ModText` `ScreenNotice` |
| [游戏状态](游戏状态.md) | `Game` `Role` `Player` `SkillInfo` `SkillSlot` `SkillBar` `HeroSkill` `Loot` `Arena` |
| [战斗事件](战斗事件.md) | `GameEvents` `Combat` |
| [自动释放、走位与技能调整](自动释放与走位.md) | `AutoCast` `MoveControl` `SkillTuning` `SkillTooltips` |
| [自定义技能](自定义技能.md) | `PassiveSkills` `ActiveSkills` `ModAssets` `ModBundle` |
| [自定义遗物](遗物.md) | `CustomRelics` `RelicDef` `RelicBehaviour` |
| [自定义装备](装备.md) | `CustomEquipEffects` `EquipEffectDef` `EquipEffectBehaviour` `CustomEquipment` `EquipmentDef` |
| [英雄](英雄.md) | `HeroVariants` `HeroSkins` |
| [数值与三选一](数值与三选一.md) | `GameData` `ChoicePanel` `RelicInfo` |
| [界面](界面.md) | `NativeUi` `NativeWindow` `NativeTabs` `GameInput` `GamePause` `NativeTip` |
| [联机](联机.md) | `Multiplayer` `LobbySync` |
| [创意工坊与内容包](工坊.md) | `Workshop` `ContentPacks` |
| [依赖和冲突](依赖和冲突.md) | `ModConflicts` `ManifestDependency`，`AutoCast` / `MoveControl` 的接管规则 |

另见：[AssetBundle 打包](../AssetBundle.md)、[内容包写法](../内容包.md)。

## 接口约定

- **1.0 之前**（现在）：公开接口可以改，改了会写进 [CHANGELOG](../../DBModKit/CHANGELOG.md)。Mod 用 `[BepInDependency(ModKitPlugin.Guid, "最低版本")]` 声明要求。
- **1.0 之后**：公开接口只增不改。要删除或改签名时，先标 `[Obsolete("改用 …")]` 保留至少一个小版本，下一个小版本才删。
  行为变化（比如联机规则收紧）也写进 CHANGELOG。
- `internal` 的成员和 `GameRaw` / `Hooks` / `Compat` 里的东西不是接口，会随游戏更新变化，Mod 不要用反射去调。

## 所有接口都要注意

- **兼容检查**：游戏版本和 ModKit 对不上时 `ModKit.Compatible` 为 false，`ModContext.Start` 不会调用 `onLoad`。只在 `onLoad` 里注册的东西不用另外判断。
- **本地玩家**：改数值的方法（`Player.Heal`、`AddStats` 等）只能对本地玩家用，对别人调用会抛异常。
- **联机**：游戏不检查 Mod，伤害由各自的客户端算。改玩法的功能看 [`Multiplayer.GameplayAllowed`](联机.md)，不要自己判断 `Game.IsSolo`。
- **排行榜**：装了 ModKit 一律不上传（游戏作者的规则），任何 Mod 都不能绕过。
- **异常**：通过 ModKit 注册的回调各自做了异常保护，一个 Mod 出错不影响别的；同一处错误只记一次日志。
