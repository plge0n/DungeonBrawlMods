# DBModKit 更新日志

## 0.6.0
- 新增 `ChoicePanel`：用游戏自己的肉鸽三选一界面（ui_roguelike）显示自定义选项（名字、描述、图标、品质色）；
  界面忙时不打开，避免把游戏待选的遗物随机选掉；不计入游戏的选项统计
- 新增 `RelicInfo`：所有遗物的编号、名字、品质、图标；`Player.GiveRelic` 发遗物（游戏自己的 `bbe.kqk`）
- `DataEntry.IconPath` 公开

## 0.5.0
- 新增 `GameEvents`：本地玩家的战斗事件（击杀（区分精英/Boss）、放技能、暴击、闪避、掉血、升级、复活、捡东西），
  以及波次开始、波次结束、Boss 出场、本局结束（补丁 `MyServerNetworkManager.qhu`）。用 `ModContext.OnGameEvent` 订阅
  - 只挂游戏没有返回值的回调；会替换伤害结果的 `damageEvent` 不碰，掉血改为逐帧比较血量
- `Player.AddStats`：加力量、敏捷、体力、攻击（加在加成上，不触发防篡改校验；负数撤销）
- `Game.Wave`：当前波次

## 0.4.0
- 新增 `GameData`：改技能、英雄、怪物配置表里的数值。改动先记在内存里，`Active` 为 true 时写进游戏的表，
  为 false 时表恢复原版；第一次改某一行时记下原版，恢复时写回
- 怪物按倍率调整（`DataField.IsScale`），出场次数表覆盖的生命、攻击、护盾也会乘上倍率（补丁 `oo.gaf`、`oo.gad`）
- `GameData.Entries` 列出表里的每一行（本地化名字、图标）
- `JsonConfig` 的文件名可以带子目录，保存时自动创建
- 技能表改动后 `SkillInfo` 会重新读取

## 0.3.0
- 新增房间状态：`Game.IsSolo`（房间里只有自己）、`Game.IsHost`、`Game.PlayerCount`
- `Role` 新增属性：攻击、力量、敏捷、体力、攻速、暴击率、暴伤、护甲、闪避、吸血、移速
- `Player` 新增 `Gold`，以及只能对本地玩家使用的 `Heal`、`AddMana`、`AddGold`（走游戏自己的方法，不会触发防篡改校验）
- 新增 `GameInput.Block()`：屏蔽移动、技能键、Tab、ESC；`GamePause.Acquire()`：暂停游戏，可以嵌套
- `NativeUi` 新增大窗口 `Window`、页签 `Tabs`、滚动列表 `ScrollView`、输入框 `Input`、`Heading`、`Separator`、`Column`、
  `Overlay`（自己的 Canvas，缩放设置和游戏 UI 一致）、`SetEnabled`、`IsTyping`

## 0.2.0
- 遵守游戏作者的规则：装了 Mod 时强制拦截排行榜上传，并在屏幕上提示；拦截补丁装不上时 ModKit 整体停用
- 新增 `ModContext`：Mod 通过它订阅的事件、自动释放处理器、JSON 配置在插件卸载时自动清理，支持 ScriptEngine 热重载
  （用组合而不是基类：插件继承 ModKit 里的类时，ScriptEngine 解析不到继承链，热重载会崩溃）
- 初始化改为首次访问 `ModKit.Compatible` 时进行，不再依赖插件加载顺序
- `JsonConfig<T>` 实现 `IDisposable`

## 0.1.0
- 首个版本，从 SmartSkill 中抽出
- 游戏接口：`Game`、`Role`、`Player`、`SkillInfo`、`SkillSlot`、`SkillBar`
- 自动释放钩子 `AutoCast`：注册处理器决定放不放、打谁
- 事件 `ModEvents`：每帧、进入/离开战斗、退出游戏，订阅者各自做异常保护
- 原生风格 UI 组件 `NativeUi`、屏幕提示 `ScreenNotice`、JSON 配置 `JsonConfig<T>`
- 启动时检查依赖的游戏成员，不兼容时整体停用并提示
