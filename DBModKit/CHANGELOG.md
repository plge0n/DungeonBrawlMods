# DBModKit 更新日志

## 0.9.0
- 创意工坊加载器 `Workshop`：游戏的工坊不加载 dll，ModKit 扫描已订阅条目（`steamapps/workshop/content/4246640/`），
  把带 `dbmod.json`（`Type = "plugin"`）的条目当成 BepInEx 插件加载
  - 每个条目第一次加载前弹窗确认（工坊 dll 就是别人写的程序），选择记在 `BepInEx/config/DBModKit.workshop.json`
  - 检查最低 DBModKit 版本、`BepInDependency`（含版本）、`BepInIncompatibility`；本地已装同一插件时用本地那份
  - 只在 ModKit 兼容、排行榜拦截已安装时加载；插件挂在自己的常驻对象上（游戏切场景会销毁 BepInEx 的管理对象）
  - 条目里不放 `manifest.json`，游戏会跳过它
  - 本地测试目录 `BepInEx/workshop-dev/`：放进来的条目不用确认，方便上传前测试
- DBStudio 的 Mod 页 → DBModKit 里列出工坊插件，可以改成允许或不加载

## 0.8.0
- 新增 `ModSettings` / `ModContext.AddSettings`：Mod 往设置界面（DBStudio 的 Mod 页）里挂自定义设置区块，卸载时自动撤下；
  设置界面和 Mod 都只依赖 ModKit，互不引用
- `SkillInfo.Find(key)`、`SkillInfo.All()`

## 0.7.0
- 新增 `ChoicePanel`：用游戏自己的肉鸽三选一界面（ui_roguelike）显示自定义选项（名字、描述、图标、品质色）；
  界面忙时不打开，避免把游戏待选的遗物随机选掉；不计入游戏的选项统计
- 新增 `RelicInfo`：所有遗物的编号、名字、品质、图标；`Player.GiveRelic` 发遗物（游戏自己的 `bbe.kqk`）
- `DataEntry.IconPath` 公开

## 0.6.0
- 新增 `GameEvents`：本地玩家的战斗事件（击杀（区分精英/Boss）、放技能、暴击、闪避、掉血、升级、复活、捡东西），
  以及波次开始、波次结束、Boss 出场、本局结束（补丁 `MyServerNetworkManager.qhu`）。用 `ModContext.OnGameEvent` 订阅
  - 只挂游戏没有返回值的回调；会替换伤害结果的 `damageEvent` 不碰，掉血改为逐帧比较血量
- `Player.AddStats`：加力量、敏捷、体力、攻击（加在加成上，不触发防篡改校验；负数撤销）
- `Game.Wave`：当前波次

## 0.5.0
- 新增 `HeroSkins`：每个英雄从已安装的创意工坊英雄模型、本地皮肤文件夹（`BepInEx/skins/`）里选一套换上，
  或者强制用原版；没选的英雄按游戏自己的工坊设置。补丁 `buj.pwu`（游戏查找英雄模型文件夹的入口），
  本地玩家正在用这个英雄时立即重新换模型
- `HeroSkins.OnlyLocalPlayer`：只给本地玩家换模型。游戏原本按英雄换，选了同一英雄的其他玩家在你这边也会被换掉
  （补丁 `buj.pwh`、`buj.pwj`，变身时通过 `bgt.mqp`、`bbp.lob` 确认是谁在变身）
- 只改本机显示，不联机同步；启动参数 `-disable-mods` 时不生效

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
