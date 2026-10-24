# DBModKit 更新日志

## 0.33.0
**适配游戏 7.02。更新后请退出游戏，重新运行运行环境条目里的 install.bat**（压缩包用户：覆盖「仅插件」包），否则所有 Mod 仍是停用状态。

- 用 `tools/Remap` 重建了映射，大厅成员数据的读写手动核对

## 0.32.0
**适配游戏 7.01。更新后请退出游戏，重新运行运行环境条目里的 install.bat**（压缩包用户：覆盖「仅插件」包），否则所有 Mod 仍是停用状态。

- 游戏每次更新都会重新混淆代码，旧版 ModKit 检查到不兼容后会停用所有 Mod（游戏照常可玩）。这一版用 `tools/Remap` 重建了映射，大厅成员数据的读写手动核对

## 0.31.0
**更新后请退出游戏，重新运行运行环境条目里的 install.bat**（压缩包用户：覆盖「仅插件」包），否则依赖新版的 Mod（Hextech 0.31.0）不会加载。

海克斯招牌卡用的接口（`SkillForms`，**未实测**）：
- `mod.SetBiteGrowth(倍率)`：哥布林之王撕咬击杀加的生命上限乘倍率（补丁 `bbe.kjv`，服务器上算，只在单人或房主时生效）
- `mod.SetBombRange(倍率)`：炸弹客的十字爆炸范围乘倍率（补丁 `bhk.msj` 后缀改 `bhlh`）
- `mod.SetThunderOrbs(加几个)`：雷电雷之一闪的雷球上限（`PlayerThunderMode.maxOrbCount`，游戏最多 32）。改过的实例都记下原值，撤销时全部改回去；
  每秒检查一次，变身结束、新一局英雄重建后重新写上（英雄模型来自对象池，可能跨局复用）
- 这三项是可选功能，不进兼容检查的必需列表：游戏更新后对不上时只停用对应的招牌卡，不影响整个 ModKit

## 0.30.0
海克斯中等触发卡用的接口（**未实测**）：
- `mod.OnLethalDamage(伤害 => 是否拦下)`：本地玩家受到致命伤害前调用，返回 true 免死（伤害改成 0）。补丁 `yb.iwl` 前缀（所有伤害最后结算的地方），
  `Combat.LethalGuardAvailable` 表示装上了
- `Player.CastSkillFree(技能, 位置, 朝向点)`：免费放一个游戏现成的技能（`yb.CmdCreateSkill`，装备落雷、月光大剑同款），身上不需要有这个技能；
  剑气类技能不读朝向点，用的是你面朝的方向
- 致命伤害拦截：死后不拦（持续伤害、远端伤害不查死亡就进结算）；联机客户端把还没同步回来的伤害算进去；按游戏的取整比较
- 修复：兼容检查表里 `ez` 和 `ActiveSkillEnum` 各有两条，后一条覆盖前一条，`ez` 的大部分成员没被检查

## 0.29.0
海克斯第三批用的接口（**未实测**）：
- `GameEventKind.Died`：本地玩家死亡（原版 `dieEvent`，Target = 自己）
- `Player.AddAttackRange(米)`：攻击距离（`yb.iqg`，遗物"瞄准镜"同款）
- `SkillSlot.SetAutoCast(开/关)`：和右键技能图标一样开关自动释放，表里不允许自动的技能不会打开；`SkillSlot.IsOnSkillBar`
- 范围伤害截到 20 亿以内（游戏按 int 转，超过会溢出成负数）

## 0.28.0
队友相关的接口（都走游戏原版的命令，队友没装 Mod 也生效；**未实测**，要两人联机）：
- `Game.Teammates()`：除了自己以外的玩家
- `Player.HealAlly(队友, 量)`：治疗队友（`yb.iwz`，和装备 MadMan 治疗队友一样）
- `Player.ShieldNearbyPlayers(半径, 比例)`：周围所有玩家（含自己）各加 生命上限 × 比例 的护盾（`CmdAddNearbyHumanityShield`，服务器限制半径 ≤ 50、比例 ≤ 1）
- `Player.GiveBuff(玩家, PlayerBuff, 数值, 秒)`：上原版 buff（`CmdAddBuff`）：技能急速、护甲、持续伤害（"圣焰祝福"只加持续伤害，普攻和技能不吃）
- `Player.PlayEffect(特效, 位置, 缩放)`：播放特效
- `Player.DamageArea` 的特效传 null 时不播特效（频繁判定时用）；新重载 `DamageArea(..., spareTeammates)` 不打队友的怪
- `GameEventKind.TeammateLevelUp`：队友升级（Target = 队友，原版 `onTeammateLevelUp`）
- `Player.GiveGold(队友, 花费, 掉落)`：花自己的金币，在队友脚下掉金币（照抄商店"丢金币"，掉的可以比花的多）
- `Role.IsTeammateMonster`：联机时属于队友的怪（每个玩家有自己的怪，打别人的怪收益归动手的人；原版判断 `bee.mec`）

## 0.27.0
海克斯联机用的改动（**未实测**，要两人联机）：
- 敌人攻击倍率（`EnemyBoosts`）改为在"攻击的怪归本机"时放大：联机时每个玩家有自己的怪，怪的命中在主人本机算，以前只对本地玩家挨打生效，
  打到别的玩家时漏掉了。生命、护盾倍率只有房主的有效（只在主机刷怪时乘）
- `LobbySync.ReadHost(键)`：读房主写的大厅数据

## 0.26.0
海克斯第二批用的接口（2026-10-23 单人实测通过）：
- `mod.SetShotgunAmmo(上限, 冲锋装填数)`（`Shotgun`）：男枪的弹药上限（原版写死 2）和冲锋装填数（原版 1），上限可以随属性变（每次需要时调用），最多 12；
  头顶弹药条会补上多出来的弹壳。替换了弹药类 `bcc` 的 4 个真方法（lpz、lqb、lqc、lpx），补了 `PlayerShotgunMode.huq` 的战术装填和 `llk` 的同步编码
- `Player.AddAttackCount` / `AttackCount`：多重攻击
- `Player.DropItem(道具, 位置, 数量)`：在地上掉游戏的道具
- `Player.DamageArea(伤害, 中心, 半径, 特效)`：范围伤害（游戏 `nn.fsp`，死灵祭司被动用的同一个）
- `Safe.Skip`：补丁前缀用，出错时自动退回原方法

## 0.25.1
- `HitInfo.RawDamage`：打出的伤害，不受目标剩余血量限制（`Damage` 被游戏截到目标的血量 + 护盾，秒杀时溢出的部分不算）。补丁 `bdc.lvz` 前缀
- 三选一卡片上 Mod 注册的图标（`mod.LoadIcon` / `RegisterIcon`）保留原色：游戏会按品质给图标染色，浅色的自制图标会整张变成紫色 / 橙色（补丁 `bpw.ork` 后缀）
- 修复：ScriptEngine 热重载的插件（程序集名会被加上 "-数字"）找不到 `scripts/<名字>/` 下的 lang/、assets/，`mod.Folder` 为 null，图标和翻译都读不到

## 0.25.0
给海克斯通用强化用的接口（2026-10-23 单人实测通过；敌人攻击倍率改成受伤时放大后才真正生效）：
- `Player.AddAttribute(键, 数值)`：按游戏的属性键加成（遗物、卡牌、锻体用的同一套，键名见注释），例如 `CoolDown` 技能急速、`AddDamage` 总伤害
- `Player.ActiveSkills()`：身上所有主动技能（含技能书学到的）；`SkillSlot.ResetCooldown()`：冷却立刻转好
- `Forging.Open(ForgeTier)`：免费用一次属性锻造器，和商店买的一样，价格不涨；`Forging.CanOpen` 判断界面空闲
- `mod.SetEnemyBoost(生命, 攻击, 护盾)`（`EnemyBoosts`）：怪物变强，多个 Mod 相乘，乘在 GameData 数值包之上。生命、护盾在刷怪时乘；攻击在怪物打到本地玩家时放大伤害（实测小怪表里的攻击只有 1~3，乘表取整等于没乘）
- `mod.SetSoulSplash(SoulSplash)`（`SoulDevourer`）：沙漠死神噬魂一击的强化普攻溅射到周围敌人，溅射打死的也叠层
- 修复：开发环境里 `BepInEx/scripts` 有同一个插件时，工坊加载器还会再加载订阅的那份，两个版本同时运行（实测 Hextech 0.1.1 和 0.2.0 一起弹三选一）

## 0.24.0
自定义装备（2026-10-23 单人实测通过：挂特效、新装备、内容包、构筑数据过滤；**没测**：联机、装备强化、重试波次、在神器三选一里实际选中）：
- 新增 `CustomEquipEffects` / `EquipEffectDef` / `EquipEffectBehaviour`（`mod.RegisterEquipEffect`）：装备特效，
  可以用 `AttachTo` 挂到游戏已有的装备上（买到时一起生效，说明里多出这个特效），也可以给自定义装备用。
  行为有 `OnAdded` / `OnRemoved` / `OnLevelChanged` / `OnUpdate` / `OnGameEvent` / `OnHit`，只在本地玩家身上运行
- 新增 `CustomEquipment` / `EquipmentDef`（`mod.RegisterEquipment`）：全新装备，属性（`Attributes`，键名见 `CustomEquipment.AttributeNames`）、
  自定义特效（`Effects`）、游戏已有特效（`GameEffects`）、进神器三选一（`Artifact`）；`CustomEquipment.Give` 直接发给本地玩家，
  `CountOnLocalPlayer` 数身上有几件。不进商店、不掉落、不能强化
- 装备和特效算改玩法的内容，计入联机握手；同步给队友、国王对决、镜像 Boss 的构筑数据会去掉自定义装备
- 内容包新增 `equipEffects` 和 `equipment` 两个列表，翻译键 `equipEffect.<key>.name/desc`、`equip.<key>.name/desc`
- SkillSample 和内容包模板各加了一个挂到 2 号装备上的特效和一件新神器

## 0.23.2
联机测试（2026-10-22）发现的问题：
- 屏幕顶部提示（`ScreenNotice`）在这个游戏里一直显示不出来（OnGUI 画不出来），改用 uGUI，优先用游戏字体。
  联机握手、排行榜拦截、加标记等提示现在才真正能看到；进战斗时的握手提示晚 3 秒弹出，避开加载画面
- 下拉框展开后每个选项只有左边的勾选标记能点，整行都能点了
- 自定义遗物的名字显示成 `pickitem_编号`
- 技能栏里的被动被当成 0 号技能（D闪电）：`SkillSlot.SkillId` 对被动返回 -1，`Info` 为 null
- 联机一栏里内容包显示成 `content_xxx`：签名里保留冒号，能显示内容包的名字
- 内容包的指纹按解析后的 JSON 算，换行符、缩进不同不再算"装的不一样"
- 内容包效果的运行记录来源是内容包的名字（原来显示成 `Assembly`）
- 满血时回血、满蓝时加蓝不做任何事、不记日志（命中回血类效果原来刷屏）
- 离开房间时不再报"清除大厅数据：Steamworks is not initialized"
- 离开别人的房间时，运行记录写"离开了别人的房间"（原来写成"有玩家加入"）
- 战斗中降级的提示写成实际原因，并说明已经拿到的不收回

## 0.23.1
- 修复：全新安装的 BepInEx（`HideManagerGameObject` 默认关）下，游戏切场景会销毁 BepInEx 的管理对象，
  plugins 目录里的所有 Mod 跟着被卸载，Mod 列表显示"已停用：DBModKit 和游戏版本不兼容"。
  DBModKit 现在启动时就把管理对象设成 HideAndDontSave（等同于打开这个设置），不用玩家改 BepInEx.cfg
- Mod 列表：被卸载的 Mod 显示"已卸载"，不用 ModKit 的插件不再显示成"不兼容"
- 修复：没有数值的自定义被动（比如芙莉莲示例的"无限魔力"）发放时空引用。表里空的格子原来写成 null，游戏读表时是空字符串，
  `bht.msz` 读到 null 就抛异常；作为变体的专属技能在出生时触发，联机时 Mirror 会因此断开连接、全房间掉线。
  自定义主动技能的 `damageExValue`、自定义遗物的空格子同样改成空字符串

## 0.23.0
- dbmod.json 新增 `Dependencies`（GUID 或内容包 Id、最低版本、工坊物品号）：工坊插件条目和内容包按依赖排序加载，缺依赖时不加载，
  在 Mod 设置里提示并给出"订阅"按钮（打开工坊页面）。内容包关掉时依赖它的包跟着停用，互相依赖时报循环
- **注意**：`AutoCast` / `MoveControl` 的处理器改为"玩家指定的优先，否则先注册的优先"，后注册的排队、生效的卸载后自动轮到下一个
  （原来是后注册的覆盖先注册的）。新增 `Owner`、`Contenders`、`Prefer`
- 新增 `ModConflicts` 和 Mod 设置 → DBModKit → 依赖和冲突：列出缺的依赖、抢处理器的 Mod（可以在这里指定）、
  两个 Mod 改了同一项数值（`GameData`，后改的生效；载入时清掉别人的改动也会提示）、内容包 Id 重复、同一个技能被几个 Mod 调整（倍率相乘）

## 0.22.0
- 新增自定义遗物（`CustomRelics`）：`mod.RegisterRelic(new RelicDef { … })` 注册后和游戏的遗物一样，按品级进遗物三选一、显示在遗物栏、可以升级；
  名字和说明走 Mod 的 `Text`（键 `relic.<Key>.name/desc`），说明里的 `{0}` 由游戏换成数值（`ValueTypes` 决定显示成原值还是百分比）。
  行为继承 `RelicBehaviour`，重写 `OnAdded` / `OnRemoved` / `OnLevelChanged` / `OnUpdate` / `OnGameEvent` / `OnHit`；`CustomRelics.Give` 直接发放。
  编号由 Mod 的 GUID 和 Key 算出（3000000 起）
- 联机：遗物只在自己的客户端生效；不是团队遗物、没有获取次数上限，选遗物时不会给房主发消息。算改玩法的内容，联机握手不允许时不进三选一。
  同步给队友、国王对决、镜像 Boss 的构筑数据会去掉自定义遗物
- 内容包支持 `relics`：字段同 `RelicDef`，触发器和被动一样；升级时自动按新等级重新生效
- `RelicInfo.All` 包括自定义遗物（注册、注销后重新读）

## 0.21.0
- 新增内容包（`ContentPacks`）：不写代码的 Mod。文件夹里放 `dbmod.json`（`"Type": "content"`）、JSON、图片和翻译，
  ModKit 从创意工坊、`BepInEx/workshop-dev/`、`BepInEx/content/` 读取，注册主动技能、被动和变体英雄，不用逐个确认。
  写法见 `docs/内容包.md`，示例在 `templates/ContentPack/`
- 内置效果库：伤害（范围 / 锁定目标）、治疗、回蓝、金币（只在单人时给）、加属性 / 攻速 / 护甲（可限时，否则跟着技能走）、特效、换动作、屏幕提示；
  触发器 onCast / onTick / onEnd（主动），onAdded / onRemoved / onHit / onKill / onCrit / onHurt / onWaveStart / onCast / onTick（被动）；
  条件 chance / hpBelow / isBoss / cooldown；数值可以写表达式（`{n}`、damage、maxHp、str 等和四则运算）
- JSON 写错时只跳过写错的条目，错误带文件和位置，显示在 Mod 设置 → DBModKit → 内容包，也写进运行记录；不认识的字段也算错
- 内容包可以在 Mod 设置里关掉、重新启用（重新读文件）；战斗中不能切换
- 联机：内容包里的技能和改了属性的变体算改玩法的内容，按包的 Id、版本和 JSON 内容的哈希参与联机握手
- `dbmod.json` 新增 `Id`、`Content`、`Lang` 字段；`pack.ps1 -Content <文件夹>` 和仓库 `content/` 下的内容包会做成工坊条目
- 联机握手：签名里不合格的字符换成下划线，不再整条丢掉（丢掉会把装的不一样当成一样）

## 0.20.0
（0.19.0 留给 feat/mod-menu）
- 新增联机握手（`Multiplayer`）：装了 ModKit 的玩家通过 Steam 大厅成员数据（键 `dbmods.mods`）公布 ModKit 版本和自己改玩法的内容，
  `Multiplayer.Policy` 给出 Solo（只有自己）/ AllHaveSame（每个人装的一样）/ HostAllowed（房主允许）/ Blocked，`GameplayAllowed` 是前三种。
  进入战斗时锁定，本局只会降级不会升级；局域网联机没有大厅，确认不了，算 Blocked
- 改玩法的内容：自定义被动、主动技能，改了属性或专属技能的变体英雄（自动算），以及调用 `mod.DeclareGameplay(指纹)` 声明的 Mod。
  "装的一样"按"GUID@版本#指纹"整套比较。外观（皮肤、变体的模型和名字）不受影响
- 自定义被动、主动技能在 `GameplayAllowed` 为 false 时不进技能书三选一，`Give` 也不给；变体英雄的属性和专属技能改为看 `GameplayAllowed`（原来只看单人）
- 房主可以在 DBStudio 的 Mod 页 → DBModKit → 联机 里勾选"在这个房间里允许"，换房间自动取消；装了 ModKit 的人都会收到提示。
  进入战斗时策略为 Blocked 而自己有改玩法的内容，屏幕上会说明原因
## 0.19.1
- 修复：代码搭的 Mod 特效（`mod.RegisterEffect` 传自己建的对象）切场景后就失效，`ActiveSkill.Effect` 报空引用。
  原来用 `scene.IsValid()` 区分"代码搭的对象"和"AssetBundle 预制体"，但插件 Awake 时（游戏启动早期）新建的对象 scene 也无效，
  被当成预制体留在启动场景里。现在按"是不是从 `ModBundle` 读出来的"区分；模板根节点也改为 HideAndDontSave。
  模板被销毁时 `Effect` 不再交给游戏，并在日志里说明原因
- 实测（0.19.1 之前的 0.15~0.18 功能）：被动、主动（自动释放、伤害统计、持续伤害）、图标（技能栏、三选一）、施法动作、变体英雄（属性、成长、专属技能、名字）、构筑过滤、Mod 卸载清理都正常

## 0.19.0
- 主菜单"创意工坊"下面加了"Mod"按钮（照抄原版按钮的样式和悬停效果），后面显示正在运行的 Mod 数，有要处理的问题时显示红色"!"。
  点开是 Mod 列表：游戏版本和兼容状态、排行榜规则、已安装的 Mod 和运行状态、BepInEx 没能加载的插件（缺前置、版本旧）、
  创意工坊插件（有等确认的可以直接去确认）、其他插件；"打开 Mod 文件夹"打开 BepInEx/plugins。
  ModKit 和游戏版本不兼容时照样显示，告诉玩家 Mod 已停用；没装 DBStudio 也能用
- `ModList.Plugins()`：已加载的插件和状态（`ModInfo.State` 运行中 / 已停用 / 启动出错 / 不用 ModKit）、是否热重载、来自哪个工坊条目；
  `ModList.LoadErrors`、`ModList.ProblemCount()`；`ModContext.Failed`
- `ModSettings.SetOpener` / `Open(guid)` / `CanOpen`：设置界面登记打开方法，Mod 列表的"设置"按钮跳过去

## 0.18.0
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
- 施法动作：`ActiveSkill.PlayAnimation(片段 / 游戏动作名, 速度, 时长)` 在技能持续期间把施法者的动作换掉（游戏旋风斩、鸡舞的同款做法：
  换掉待机片段并锁住动画，结束时还原），每个装了 Mod 的客户端都能看到；`StopAnimation` 提前换回。片段可以来自 AssetBundle（Humanoid），
  也可以用游戏自带的（`Frank_RPG_2Hand_Skill01_WhirlWind` 等）。和别的换动作技能重叠时会先结束对方，游戏的旋风斩叠在后面时结束后会自动还原
- 新增变体英雄（`HeroVariants`）：`mod.RegisterHeroVariant(new HeroVariantDef { BaseHero = 英雄, … })`，底层仍是原英雄，
  出生时换属性（三维、成长、攻击、护甲、生命、法力、回复、移速、攻速）、专属技能（游戏的或自定义的主动/被动）、模型（Mod 自带的模型文件夹或工坊模型）和名字。
  属性和专属技能和玩法包一样只在单人时生效，联机时只换名字和模型（模型只在自己这边显示）。玩家在 DBStudio 的 Mod 页"英雄变体"里给每个英雄选，
  也可以用 `HeroVariants.Select`；`HeroVariants.LocalVariant` / `LocalGameplayActive` 是这一局的情况
- 新增 Mod 资源（`ModAssets`）：`mod.LoadIcon(键, "图片.png")` / `mod.RegisterIcon(键, Sprite)` 返回图标名，填进技能的 `Icon`，
  技能栏、三选一、图鉴、构筑面板都会显示；`mod.RegisterEffect(键, 模板)` 返回特效路径，传给 `ActiveSkill.Effect`，走游戏的对象池；
  `mod.LoadBundle("xxx.bundle")` 打开 AssetBundle（`Sprite` / `Prefab` / `Clip` / `Load<T>`），卸载时自动关闭。`mod.Folder` 是插件所在文件夹
- 插件项目里的 `lang/`、`assets/` 编译时跟着 dll 部署（热重载时在 `BepInEx/scripts/<程序集名>/`，`mod.Folder` 会找到这里），`pack.ps1` 也会打进包和工坊条目；
  原来 `lang/` 翻译文件不会被部署和打包
- 图标靠 `Resources.Load` 补丁（`ModAssets.IconsAvailable`）；装不上时技能栏按钮单独补上
- 映射重建工具：重写游戏虚方法的声明、带命名空间的类型名字符串也会跟着改；编译检查也读项目文件自己的引用
- 修复：兼容检查清单里 `AttackType`、`DamageSourceKind`、`qp`、`bff` 写了两次，后写的空清单覆盖了前面的成员检查

## 0.17.1
- 排行榜规则补漏：在别人的房间里，国王对决结算时不再把自己的构筑快照发给房主（`bbe.ktb`）。房主没装 Mod 时，
  原来会把我们（可能改过数值的）构筑和队伍一起上传；现在队伍照常上传，只是不带我们，并在屏幕上提示。
  自己当房主时不变（最后的上传本来就会被拦截）。这道拦截装不上时 ModKit 整体停用，和上传拦截一样

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
