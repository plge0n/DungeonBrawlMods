# DBModKit 更新日志

## 0.3.0
- 走位接口 `MoveControl` / `ModContext.UseMovement`：玩家没按方向键时由处理器决定移动方向，效果等同按住方向键
- `Arena`：地面预警（敌人技能、陷阱的红圈/扇形/矩形，含剩余时间）、场地边界检查、场内障碍物检测（`FreeDistance` / `ObstacleClearance`，按玩家碰撞体检测）
- 命中事件 `Combat.Hit` / `ModContext.OnHit`：伤害、是否打死、技能编号、是否暴击
- `Role`：护盾、无敌、普攻距离、移速、状态；`Player`：普攻冷却、是否已出手、变身中、技能效果剩余时间、增伤
- `SkillSlot`：开关技能状态、充能层数、斩杀类技能的伤害估算；`SkillInfo.IsToggle`、`HasKillReward`
- 开关技能开着时，自动释放处理器可以决定保持开启（原版会直接关掉）

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
