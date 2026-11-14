# 发版历史 / Release history

整套 Mod 每次发版的改动，最新的在前。各 Mod 自己的详细改动见源码目录里的 `CHANGELOG.md`（英文 `CHANGELOG.en.md`）。
每个版本适配的游戏版本见[最新的发版说明](RELEASE_NOTES.md)。

[English](#english)

## 0.44 的改动

- **DBModKit 0.44.0：适配游戏 7.19**。**这一版没有实机测试**，只确认了编译通过、游戏启动后所有 Mod 正常加载。遇到问题请在 Mod 列表里导出报告反馈。

## 0.43 的改动

- **DBModKit 0.43.0：适配游戏 7.18**。**这一版没有实机测试**，只确认了编译通过、游戏启动后所有 Mod 正常加载。遇到问题请在 Mod 列表里导出报告反馈。

## 0.42 的改动

- **DBModKit 0.42.0：适配游戏 7.16**。**这一版没有实机测试**，只确认了编译通过、游戏启动后所有 Mod 正常加载。遇到问题请在 Mod 列表里导出报告反馈。

## 0.41 的改动

- **DBModKit 0.41.0：适配游戏 7.15**。**这一版没有实机测试**，只确认了编译通过、游戏启动后所有 Mod 正常加载。遇到问题请在 Mod 列表里导出报告反馈。
- **AutoForge 0.41.0**：游戏 7.15 自带了「自动锻造器」（商店批量做黄金锻造，不弹选择窗口），和 AutoForge 不冲突；AutoForge 继续处理普通/黄金锻造器、英雄被动、Hextech 等弹出的三选一。只更新了说明，代码没改。

## 0.40 的改动

- **打榜白名单**：经游戏作者同意，DBModKit、DamageMeter、SmartSkill、AutoPickup、SmartMove、AutoForge 可以打榜；只装这些 Mod 时不用开启打榜模式。
- **打榜模式**：在主菜单「Mod」里开启，重启后停用 Hextech、DBStudio、内容包等玩法 Mod；关闭后再重启恢复。
- **上传资格**：其他插件、内容包、ScriptEngine、UnityExplorer、额外补丁或不合格的队友会阻止上传，游戏里会写明原因。联机时全队都要合格，旧版 ModKit 队友需更新。
- **AutoForge 0.40.0：自动锻体**。支持自定义属性权重和均衡模式，F10 开关，在「Mod → AutoForge → 设置」里调整。候选全是权重 0 的属性时照常手选；不会自动购买锻造器，联机只操作本机。
- **DBStudio 0.40.0**：Mod 页的排行榜说明改为新规则；装着时不能打榜，可以在 Mod 菜单开启打榜模式并重启。
- **以上都没有实机测试**，只检查编译和启动到主菜单，有问题请反馈。

## 0.39 的改动

- **DBStudio 0.39.0：新增「构筑」页**。单人战斗中可添加、替换或移除技能，增删被动，调整装备和遗物等级。
- **卡牌全解锁提供两种方式**：默认关闭的「卡牌全解锁（不改存档）」提供每张卡至少 7 张、记忆容量 50，关闭后恢复原收藏、卡组和预设，保留期间新获得的卡牌；开启期间不能合成、分解或升级容量，战斗中切换只影响下一局。「写进存档（永久）」在局外确认后先备份再写入，结果会被 Steam 云同步，卸载 Mod 后仍保留，恢复需使用备份。
- **DBModKit 0.39.0**：提供局内构筑编辑、卡牌全解锁和存档备份接口，支持 Mod 注册的内容。构筑页与卡牌全解锁已于 2026-11-04 实机测试通过。

## 0.38 的改动

- **DBModKit 0.38.0：适配游戏 7.13**。**这一版没有实机测试**，只确认了编译通过、游戏启动后所有 Mod 正常加载。遇到问题请在 Mod 列表里导出报告反馈

## 0.37 的改动

- **DBModKit 0.37.0：适配游戏 7.11**。**这一版没有实机测试**，只确认了编译通过、游戏启动后所有 Mod 正常加载。遇到问题请在 Mod 列表里导出报告反馈
- 游戏版本不兼容时，提示里写明这一版适配的游戏版本，可以等 Mod 更新，也可以在 Steam 里把游戏切回那个版本；Mod 列表顶部也显示适配的游戏版本
- 创意工坊的运行环境条目在标题和说明里注明适配的游戏版本，说明里加了固定游戏版本的方法
- 发版说明只写这一版：以前各版的改动挪到本文件，冲突处理、未实测的功能、已知问题挪到 `docs/常见问题.md`（英文 `docs/FAQ.md`）

## 0.36 的改动

- **DBModKit 0.36.0：适配游戏 7.10**。**这一版没有实机测试**，只确认了编译通过、游戏启动后所有 Mod 正常加载。遇到问题请在 Mod 列表里导出报告反馈

## 0.35 的改动

- **DBModKit 0.35.0：适配游戏 7.06**。**这一版没有实机测试**，只确认了编译通过、游戏启动后所有 Mod 正常加载。遇到问题请在 Mod 列表里导出报告反馈

## 0.34 的改动

- **DBModKit 0.34.0：适配游戏 7.03**。**这一版没有实机测试**，只确认了编译通过、游戏启动后所有 Mod 正常加载。遇到问题请在 Mod 列表里导出报告反馈

## 0.33 的改动

- **DBModKit 0.33.0：适配游戏 7.02**。实测了一局单人（遗物三选一、海克斯、自动释放、走位、自动拾取、伤害统计），没有发现问题
- **修复：游戏装在带中文的文件夹里时，所有 Mod 都可能停用**（日志里是 `String conversion error: Illegal byte sequence`）。
  Steam 默认的游戏文件夹名就是「地下城竞技场」。这是 Mono 运行时的路径编码 bug，现在 ModKit 会绕开它：
  `install.bat` 会自动打开 BepInEx 的 `LoadDumpedAssemblies` 设置；如果没打开（比如手动装的），ModKit 第一次启动时自动补上，并提示**重启一次游戏**

## 0.32 的改动

- **DBModKit 0.32.0：适配游戏 7.01**。游戏每次更新都会重新混淆代码，旧版 ModKit 检查到不兼容后会停用所有 Mod，更新运行环境后恢复。
  实测了一局单人（海克斯三选一和招牌卡、自动释放、伤害统计、自动拾取、内容包），没有发现问题
- 开发者工具 `tools/Remap` 第一次用在真实的游戏更新上，修了三处改名遗漏 / 误改

## 0.31 的改动

从这一版起，版本号和整套发版对齐：整套发版用两位小数（0.31、0.32……），这一版改过的 Mod 用同样的版本号（DBModKit、Hextech 都是 0.31.0，
Hextech 从 0.3.0 直接跳到 0.31.0，中间的 0.4.0–0.8.0 是开发中的版本，没有单独发布）。没改的 Mod 保持原来的版本号，下次改动时再对齐。

- **Hextech 0.31.0：开放联机，新增约 50 张卡**
  - **联机**：房间里每个人都装了同样的 Mod（或者房主允许）时生效，以前是"有人进房就停用"。联机时游戏不能暂停，三选一只在休息阶段弹。
    敌人海克斯以房主的设置为准（敌人生命对全房间生效，敌人攻击只对装了 Hextech 的人生效）
  - **队友卡**（参照 LoL 斗魂竞技场，只在有队友时出现，队友不用装 Mod）：甘愿牺牲、复仇、果汁盒、防御策略、灵魂连接、号令之旗、音爆、神圣干预、晾衣绳
  - **让经济**（把经济让给能 C 的队友）：慷慨供养、后排成长、守护之誓、举国之力。选中后指定要供养的队友
  - **触发、属性、任务、代价**：俯冲轰炸、慢炖、无休恢复、瞄准镜、以巧补拙、以力带巧、棱彩蛋、乌尔夫的勇士、嬗变：黄金、神秘拳、自动施放、
    玻璃大炮、孤注一掷、重装
  - **中等触发**：魔法飞弹、飞剑、天雷（追加现成技能的落雷、剑气）、破晓决意、依然在此、不灭（免死）
  - **招牌卡**：哥布林之王撕咬（贪吃 / 叠角龙 / 叠角龙·极）、炸弹客（大炸弹 / 核弹 / 核弹·极）、雷电雷之一闪（雷暴 / 万雷 / 雷神）
  - 联机时每个玩家有自己的怪，在队友身边的范围伤害不打队友的怪（不抢收益）
  - 新卡都有手绘图标
- **DBModKit 0.31.0**：上面这些用的接口（队友治疗 / 护盾 / buff、送金币、免死拦截、免费放现成技能、攻击距离、自动释放开关、招牌技能形态……）；
  修复兼容检查表里两条重复的键（游戏更新后可能检查不出不兼容）

**这次的新内容都没有在游戏里实测过**，见下面"未实测的功能"。遇到问题请导出报告反馈。

## 0.2 的改动

- **Hextech 0.3.0**：根据玩家反馈，参照 LoL 海克斯大乱斗大改
  - 新增**通用强化**，所有英雄都能抽：有专属卡的英雄每次"专属 2 张 + 通用 1 张"，没有参数的被动英雄（二刀流等）也能开海克斯了
  - 通用卡 21 张，例如：大力、灵巧、嗜血、淘金（击杀概率得属性锻造）、由心及物（蓝转血）、魔宗、左脚踩右脚、奥术公理（杀 Boss 刷新冷却）、
    无限循环往复（击杀叠技能急速）、坦克引擎、豪猪、大法师、双持……
  - 招牌卡（银 / 金 / 棱彩三档）：沙漠死神「邦」（噬魂一击变范围伤害，溅射击杀也叠层）、男枪「上膛 / 大炮」（爆伤转弹药上限，最多 12 发）
  - 属性锻造攒着，休息时依次打开
  - 可选的**敌人海克斯**（配置 `EnemyAugments`，默认关）：你每选一张，敌人也拿一张同品质的强化，作为补偿每次多给 1 次刷新
  - 通用卡和招牌卡换成手绘图标
- **DamageMeter 0.4.1**："最高一击"按溢出前的伤害算（原来一下秒杀时只算到怪物剩余的血量）
- **DBModKit 0.26.0**：给上面这些加的接口（属性加成、锻造、怪物倍率、男枪弹药、多重攻击、掉道具、范围伤害……）；
  修复开发环境里 `scripts` 和工坊同时有同一个插件时两个版本一起运行
- 创意工坊新增内容包示例「[芙莉莲：无限魔力](https://steamcommunity.com/sharedfiles/filedetails/?id=3824936651)」

## 0.1.1 的改动

- **SmartMove 0.3.0**：开关躲避的快捷键默认改成 **F8**（原来是 F9，和 DBStudio 打开工作台撞键）。自己改过快捷键的，会沿用你设的键
- 创意工坊每次更新的说明现在同时有中文和英文

## 0.1（第一个公开版本）

第一个公开版本。所有功能都基于游戏 6.45 版本开发和测试，游戏更新后如果不兼容，会在进游戏时提示，Mod 自动停用，游戏照常可玩。

---

## English

Changes in each suite release, newest first. Per-mod details are in each mod's `CHANGELOG.en.md` in the source.
The game version each release supports is listed in the [latest release notes](RELEASE_NOTES.md).

## Changes in 0.44

- **DBModKit 0.44.0: updated for game 7.19**. **Not play-tested**; we only checked that it builds and that every mod loads when the game starts. If something breaks, please export a report from the Mod list.

## Changes in 0.43

- **DBModKit 0.43.0: updated for game 7.18**. **Not play-tested**; we only checked that it builds and that every mod loads when the game starts. If something breaks, please export a report from the Mod list.

## Changes in 0.42

- **DBModKit 0.42.0: updated for game 7.16**. **Not play-tested**; we only checked that it builds and that every mod loads when the game starts. If something breaks, please export a report from the Mod list.

## Changes in 0.41

- **DBModKit 0.41.0: updated for game 7.15**. **Not play-tested**; we only checked that it builds and that every mod loads when the game starts. If something breaks, please export a report from the Mod list.
- **AutoForge 0.41.0**: game 7.15 added its own Auto Forger (bulk gold forging from the shop, no choice window). It doesn't conflict with AutoForge, which keeps handling the pick-one-of-three choices from regular/gold forgers, hero passives, Hextech and so on. Documentation only, no code changes.

## Changes in 0.40

- **Leaderboard whitelist**: with the game author's approval, DBModKit, DamageMeter, SmartSkill, AutoPickup, SmartMove and AutoForge can upload scores. Ranked Mode is optional if you only use these mods.
- **Ranked Mode**: enable it in the main-menu Mod window and restart to disable Hextech, DBStudio, content packs and other gameplay mods. Turn it off and restart to restore them.
- **Upload eligibility**: other plugins, content packs, ScriptEngine, UnityExplorer, additional patches or ineligible teammates block uploads; the game explains why. Everyone in a multiplayer team must qualify; teammates with older ModKit versions need to update.
- **AutoForge 0.40.0: automatic forging choices**. Use custom attribute weights or balanced mode, toggle with F10, and configure under Mod → AutoForge → Settings. If all offered attributes have weight 0, choose manually as usual. It never buys forging items and only controls the local player in multiplayer.
- **DBStudio 0.40.0**: the Mod page explains the new leaderboard rules. Having it loaded blocks uploads; enable Ranked Mode in the Mod window and restart to disable it.
- **None of the above has been play-tested**; only compilation and main-menu startup were checked. Please report problems.

## Changes in 0.39

- **DBStudio 0.39.0: new Build page**. During single-player combat, add, replace or remove skills, add or remove passives, and adjust equipment and relic levels.
- **Two ways to unlock all cards**: the runtime unlock is off by default and provides at least 7 copies of each card and 50 memory capacity. Disabling it restores the original collection, decks and presets while keeping newly earned cards. Crafting, dismantling and capacity upgrades are blocked while enabled; toggling during combat affects the next run. Permanent save writing requires confirmation outside combat and creates a backup first. Steam Cloud syncs the result, which remains after uninstalling; recovery requires the backup.
- **DBModKit 0.39.0**: adds loadout editing, card unlock and save backup APIs, including registered mod content. The Build page and card unlock were play-tested successfully on 2026-11-04.

## Changes in 0.38

- **DBModKit 0.38.0: updated for game version 7.13**. **This release was not play-tested**; we only checked that it builds and that every mod loads when the game starts. If something breaks, please export a report from the mod list and let us know

## Changes in 0.37

- **DBModKit 0.37.0: updated for game version 7.11**. **This release was not play-tested**; we only checked that it builds and that every mod loads when the game starts. If something breaks, please export a report from the mod list and let us know
- When the game version is incompatible, the notice now says which game version this release is for: wait for a mod update, or switch the game back to that version on Steam. The mod list also shows it at the top
- The Workshop runtime item now states the supported game version in its title and description, plus how to stay on a game version
- Release notes now cover only the current release: earlier changes moved to this file; conflicts, untested features and known issues moved to `docs/FAQ.md`

## Changes in 0.36

- **DBModKit 0.36.0: updated for game version 7.10**. **This release was not play-tested**; we only checked that it builds and that every mod loads when the game starts. If something breaks, please export a report from the mod list and let us know

## Changes in 0.35

- **DBModKit 0.35.0: updated for game version 7.06**. **This release was not play-tested**; we only checked that it builds and that every mod loads when the game starts. If something breaks, please export a report from the mod list and let us know

## Changes in 0.34

- **DBModKit 0.34.0: updated for game version 7.03**. **This release was not play-tested**; we only checked that it builds and that every mod loads when the game starts. If something breaks, please export a report from the mod list and let us know

## Changes in 0.33

- **DBModKit 0.33.0: updated for game version 7.02**. Tested one single-player run (relic picks, Hextech, auto-cast, movement, auto pickup, damage meter) with no problems
- **Fix: all mods could be disabled when the game is installed in a folder with non-ASCII characters** (the log says `String conversion error: Illegal byte sequence`).
  The default Steam folder name for the game is Chinese ("地下城竞技场"). This is a path-encoding bug in the Mono runtime; ModKit now works around it:
  `install.bat` turns on BepInEx's `LoadDumpedAssemblies` setting, and if it is off (e.g. a manual install) ModKit turns it on at first launch and asks you to **restart the game once**

## Changes in 0.32

- **DBModKit 0.32.0: updated for game version 7.01**. Every game update re-obfuscates the code; the old ModKit detects this and disables all mods until the runtime is updated.
  Tested one single-player run (Hextech picks and signature augments, auto-cast, damage meter, auto pickup, content packs) with no problems
- The developer tool `tools/Remap` was used on a real game update for the first time; fixed three cases it renamed wrongly or missed

## Changes in 0.31

From this release on, version numbers follow the suite release: releases use two decimals (0.31, 0.32, …), and mods changed in a release get the same number
(DBModKit and Hextech are both 0.31.0; Hextech jumps from 0.3.0 to 0.31.0, the 0.4.0–0.8.0 in between were development versions and never released). Unchanged mods keep their version until they change.

- **Hextech 0.31.0: multiplayer, and about 50 new augments**
  - **Multiplayer**: works when everyone in the room has the same mods (or the host allows it); it used to switch off as soon as anyone joined. The game can't pause online, so picks only appear during the rest phase.
    Enemy augments follow the host's setting (enemy HP applies to the whole room, enemy attack only to players with Hextech)
  - **Teammate augments** (inspired by LoL Arena; only offered when you have teammates, who don't need the mods): Willing Sacrifice, Vengeance, Juice Box, Defensive Maneuvers, Spirit Link, Banner of Command, Sonic Boom, Divine Intervention, Clothesline
  - **Feed the carry**: Generous Patron, Backline Growth, Guardian's Oath, All In — pick the teammate you support after choosing one
  - **Triggers, stats, quests, trade-offs**: Dive Bomber, Slow Cooker, Restless Restoration, Scopes, two stat conversions, Prismatic Egg, Urf's Champion, Transmute: Gold, Mystic Punch, Auto-cast,
    Glass Cannon, All or Nothing, Heavy
  - **Medium triggers**: Magic Missile, Flying Sword, Sky Strike (extra lightning / sword waves from existing skills), Dawnbringer's Resolve, Still Here, Undying (cheat death)
  - **Signature augments**: Goblin King's Bite (Stackosaurus), Bomber's bomb (Nuke), Raiden's Thunder Flash (more orbs)
  - In multiplayer each player has their own monsters; area damage near teammates skips their monsters so you don't steal their rewards
  - Hand-drawn icons for all new augments
- **DBModKit 0.31.0**: the APIs behind the above (healing / shielding / buffing teammates, giving gold, cheat-death hook, free casts of existing skills, attack range, auto-cast toggle, signature skill tweaks…);
  fixed two duplicate keys in the compatibility table (a game update could have gone undetected)

**None of the new content has been tested in game yet**; see "Not yet tested in game" below. Please export a report if something goes wrong.

## Changes in 0.2

- **Hextech 0.3.0**: reworked after player feedback, inspired by LoL's ARAM: Mayhem
  - New **generic augments** that every hero can roll: heroes with signature-skill cards get 2 of those + 1 generic per pick, and heroes whose passive has no parameters now get augments too
  - 21 generic augments, e.g. Blunt Force, Deft, Goredrink, Gold Rush (kills may grant an attribute forge), Mind to Matter, Manamune, Arcane Axiom (boss kills reset cooldowns),
    Infinite Recursion, Tank Engine, Porcupine, Archmage, Dual Wield…
  - Signature augments in three tiers: Desert Reaper's "Bonk" (the empowered strike splashes and splash kills grant stacks) and the Shotgun hero's "Cannon" (crit damage turns into ammo, up to 12 shells)
  - Attribute forges are saved up and open during the rest phase
  - Optional **enemy augments** (config `EnemyAugments`, off by default): enemies get an augment of the same tier whenever you pick one, and you get one extra reroll per pick
  - Hand-drawn icons for generic and signature augments
- **DamageMeter 0.4.1**: "biggest hit" counts overkill damage (it used to stop at the target's remaining HP)
- **DBModKit 0.26.0**: the APIs behind the above (attributes, forges, enemy multipliers, shotgun ammo, multi-attack, item drops, area damage…);
  fixed dev setups running two copies of a plugin that was both in `scripts` and subscribed on the Workshop
- New Workshop content pack sample: [Frieren: Infinite Mana](https://steamcommunity.com/sharedfiles/filedetails/?id=3824936651)

## Changes in 0.1.1

- **SmartMove 0.3.0**: the dodge toggle now defaults to **F8** (it was F9, which also opens DBStudio). If you had changed the key yourself, it's kept
- Steam Workshop update notes now include English as well as Chinese

## 0.1 (first public release)

First public release. Everything was built and tested against game version 6.45. If a game update breaks compatibility, you'll see a notice on start-up and the mods disable themselves; the game keeps working.
