# DBModKit 更新日志

## 0.17.0
- 英雄专属技能 `HeroSkill`（`HeroSkill.Of(英雄)`、`Player.HeroSkill`）：hero 表里每个英雄出生时发放的那个技能，主动或被动；主动的带冷却、蓝耗、持续时间、充能、伤害参数，被动的带表里的数值
- `Player.HeroSkillSlot`（篮球巨星拿到"鸡舞"卡牌后返回换过的真·鸡舞）、`Player.FindSkill`、`SkillSlot.SkillId` / `CooldownTotal` / `ReduceCooldown`
- 技能调整 `SkillTuning` / `mod.TuneSkill(技能, SkillTune)`：伤害、冷却、蓝耗、持续时间倍率和额外充能，**只作用于本地玩家**——释放时临时替换配置表、放完换回原版，伤害按命中乘倍率；多个 Mod 调同一技能时相乘，卸载时自动撤销。重试波次时跟着还原
- 被动技能数值：`Player.PassiveValues(编号)`、`Player.SetPassiveValues(编号, 数值)`，只改身上的技能实例（内部折扣、电锯恶魔会先撤销再按新值生效）
- 释放事件 `Combat.Cast`（`mod.OnSkillCast`）：本地玩家放出技能时触发，带目标点、朝向、目标、技能书编号、是否免费；`Player.FreeCast` 用游戏的"多重施法"同款免费再放一次（不扣蓝、不进冷却）
- `Player.AddAttackSpeed`、`AddArmor`（累加，负数撤销）、`GiveInvulnerable(秒)`
- 重试波次事件 `ModEvents.CheckpointSaved` / `CheckpointRestored`（`mod.OnCheckpointSaved/Restored`）：游戏拍快照、点"重试"还原之后通知，Mod 自己的状态在这里存取
- `Game.TotalWaves`、`Game.RelicWaves`（进入哪些波次前的休息阶段弹遗物三选一）
- `ChoicePanel.IsBusy` 在开场界面开着时也为真（它一关游戏就弹开局遗物）
- `NativeTip.Show/Hide`：游戏原生的悬停提示框；`mod.SetSkillTooltip(技能, 文字)`：在本地玩家技能的悬停提示末尾追加文字（游戏自己的附加说明保留）

## 0.16.0
- `HitInfo.EffectKind` / `EffectName`：没带技能实例的伤害是哪个被动、装备（显示装备名；全身共享的装备技能显示技能名）、遗物或 Buff 造成的。
  游戏调伤害时不传这个信息，ModKit 在伤害钩子里看调用栈找出发起的类，再到本地玩家身上（Buff 在目标身上）找对应的实例取名字。
  只对本地玩家打出、且有人订阅命中事件时才取调用栈；名字按类型缓存

## 0.15.0
- `HitInfo.SourceSkillId`：造成这次伤害的技能实例的技能编号。游戏的命中记录只在来源标成"主动技能"时带技能编号（`SkillId`），
  但很多技能结算伤害时没标，这些伤害以前看起来像被动；现在补丁 `yb.iwk` / `yb.iwr`，在调用期间记下技能实例
- `HitInfo.Type`（`HitType`：普攻、技能、持续伤害、普攻特效、真实伤害）和 `HitInfo.Origin`（`HitOrigin`：光环、异常状态、反伤、被动触发、幻影武器……）；
  钩子没装上时为 Unknown（`Combat.ContextAvailable`）

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
