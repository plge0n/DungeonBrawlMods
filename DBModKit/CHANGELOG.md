# DBModKit 更新日志

## 0.15.0
- 新增自定义被动技能（`PassiveSkills`）：`mod.RegisterPassive(new PassiveSkillDef { … })` 注册后和游戏的被动一样，
  会出现在技能书三选一里（按品级进池，可关）、占一个技能格、图鉴里能看到；名字和说明走 Mod 的 `Text`（键 `passive.<Key>.name/desc`），
  说明里的 `{0}` 等换成数值（含等级成长）。行为继承 `PassiveSkill`，重写 `OnAdded` / `OnRemoved` / `OnUpdate` / `OnGameEvent` / `OnHit`，
  只发给本地玩家技能栏里的被动。`PassiveSkills.Give` 直接发放，`OnLocalPlayer` 找到身上的实例。Mod 卸载时自动注销并从技能栏移除
- 编号由 Mod 的 GUID 和 Key 算出（1000000 起），每次启动都一样
- 联机：被动只在自己的客户端生效，别人不用装。同步给队友、上传国王对决、镜像 Boss 的构筑数据会去掉自定义被动，
  没装 Mod 的人不会因此出错（代价是这些地方也看不到它）
- 新增自定义主动技能（`ActiveSkills`）：`mod.RegisterSkill(new ActiveSkillDef { … })`，蓝耗、冷却、充能、瞄准方式（None/Range/Vector/Target）、
  施法距离、范围、伤害公式都按游戏的表填，扣蓝、冷却、瞄准、自动释放、施法动作走游戏原来的流程；技能书三选一、图鉴里也有。
  技能生效时创建 `ActiveSkill` 行为对象（`OnCast` / `OnUpdate` / `OnEnd`），提供 `Position`、`Direction`、`Target`、`Radius`、
  `FormulaDamage`，以及走游戏伤害结算的 `DamageArea` / `Damage`、对象池特效 `Effect`。`ActiveSkills.Give` 直接发放
- 联机：技能在每个装了这个 Mod 的客户端上都会创建（能看到效果），伤害只在施法者那边结算；没装的人只看到施法动作，不会出错。
  编号 2000000 起
- 映射重建工具：重写游戏虚方法的声明、带命名空间的类型名字符串也会跟着改

## 0.14.0
- 新增 `LobbySync`：通过 Steam 大厅成员数据和其他装了 Mod 的玩家交换少量文字数据（`Publish` / `Read` / `Clear`）。
  皮肤同步用的同一个通道，没装 Mod 的玩家不受影响；局域网模式没有大厅，不可用
- `Game.Players()`：场上所有玩家；`Player.NetId`、`Name`、`HeroId`、`HeroName`

## 0.13.0
- `HitInfo.IsNormalAttack`：这一下是普攻（游戏命中记录的 `Normal`）
- `Game.Wave`（以及 `GameEvent.Wave`）改为从 1 开始，和游戏界面一致；原来比显示的少 1，DBStudio 规则的"波次至少"也因此差一波；运行记录里的波次同样改为从 1 开始
- `Role.IsSummon`、`Role.IsSummonOf(Player)`：召唤物和它的主人（`roleType = Summon`，`authorityId` = 主人的 netId）

## 0.12.0
- 新增 `Loot`：读地上的掉落物（编号、`LootKind` 类别、位置、能不能捡、占不占背包格子、名字），
  `Loot.TryPick` 替本地玩家拾取（游戏的 `CmdPickItem`）。服务器不检查拾取距离，所以 ModKit 限制在按 C 的 5 米内；
  死亡中、别人的物品、背包满时不发请求
- `Loot.BagCount`、`Loot.KeyPickTalisman`（游戏设置"护符需按键拾取"）、`Loot.PlayPickSound`
- `Role.DistanceTo(Vector3)`

## 0.11.0
- 运行记录 `RunLog`：所有 Mod 共用的结构化日志（时间、来源 Mod、分类、级别、键=值数据、波次、帧号），
  内存保留最近 3000 条，同时写进 `BepInEx/DBModKit/trace.log`（上一次启动的在 trace.prev.log）。
  Mod 用 `mod.RunLog.Info("分类", "内容", ("键", 值))` 写；Warning/Error 同时进 BepInEx 日志
- ModKit 自动记录：Mod 加载/卸载/启动失败、进出战斗（带初始属性）、房间人数变化、
  `Player` 的回血/加蓝/加金币/发遗物/改属性（前后对比，自动标出是哪个 Mod 调用的）、配置表修改、三选一的弹出和选择、
  所有被拦下的异常；Debug 级：每个战斗事件、自动释放决策变化、输入屏蔽和暂停的获取/释放
- `RunLog.Mark()` 插带编号的标记（默认 Ctrl+F10），`RunLog.ExportReport()` 导出诊断报告（版本、兼容、状态、全部记录）
- 配置 `RunLog.FileLevel`（写文件的最低级别，测试时改 Debug）、`RunLog.MarkHotkey`
- 工坊加载器识别运行环境条目（`dbmod.json` 的 `Type = "runtime"`）：工坊里的版本比装的新时在游戏里提示运行 install.bat
- 多语言接口 `ModText`（`mod.Text["键", "中文原文"]`，翻译放在插件旁的 `lang/<语言>.json`）和 `Localization.Language` / `Changed`（跟随游戏语言）
- `NativeUi.ListItemBackground`：列表项的方框背景（放在不参与布局的子物体上，列表项不再被贴图边框撑到至少 220 高）
- 修复：克隆的原版按钮文字框比按钮宽，挨着放时会抢走旁边按钮的点击（点"导出"触发"清空"）
- 修复：定宽的 `NativeUi.Column` 仍会和旁边的栏平分剩余宽度；大窗口的标题和底部状态栏压在面板边框上；
  `GameData.Entries(Hero)` 的英雄名显示成编号、`Entries(Enemy)` 的怪物名显示成代码名

## 0.10.1
- 修复：`Player.Enemies()` / `EnemiesWithin()` / `PickEnemy()` 始终返回空（误用了只认玩家的 `Player.Wrap`）。
  受影响：SmartSkill 的选目标和按敌人判断的条件、SmartMove、DBStudio 的附近敌人和规则条件

## 0.10.0
- Steam 联机时同步皮肤（`HeroSkins.SyncInLobby`、`ShowOtherPlayers`、`OtherPlayers`）：每个装了 Mod 的玩家把
  "角色 netId + 英雄 + 工坊皮肤"写进自己的大厅成员数据（`cft.tqj`），别人读到后，自己也订阅了就给对方换上，没订阅显示原版。
  游戏不用成员数据，没装 Mod 的玩家不受影响；不用自定义 Mirror 消息（没装 Mod 的一方收到会断开连接）。
  本地（局域网）模式没有大厅，不同步；本地皮肤不同步
- `HeroSkins.OpenWorkshopPage`：打开工坊物品页面（订阅由玩家自己点）

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
  本地玩家正在用这个英雄时立即重新换模型，并刷新血条上的英雄名（`roleName` 原本只在出生时按模型的 heroName 算一次）
- `HeroSkins.OnlyLocalPlayer`：只给本地玩家换模型。游戏原本按英雄换，选了同一英雄的其他玩家在你这边也会被换掉，
  名字也会变成模型里的 heroName。补丁 `bbe.kzz`（出生）、`bgt.mqp` / `bbp.lob`（变身）记下当前角色，
  `buj.pwu` 对其他玩家返回原版，模型和名字都保持原样
- 启动参数 `-disable-mods` 时皮肤不生效
- 修复 `NativeUi.Row` 在竖向布局里抢占剩余高度：DBStudio 的标题栏、页签被撑高，正文区只剩很小一块

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
