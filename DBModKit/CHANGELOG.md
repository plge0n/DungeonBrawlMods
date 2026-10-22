# DBModKit 更新日志

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
