# 地下城竞技场 Mods 0.31

[English](#dungeonbrawl-mods-03)

## 升级须知（必读）

**这次更新了运行环境（DBModKit 0.26.0 → 0.31.0），升级后必须重新安装运行环境，否则新版海克斯不会加载。**

- **从创意工坊装的**：等 Steam 把条目更新下载完，**退出游戏**，到运行环境的条目文件夹
  （`<Steam 库>\steamapps\workshop\content\4246640\3824690979\`）里**再双击一次 `install.bat`**，然后启动游戏。
  游戏里看到"运行环境有更新"的提示也是这个意思。
- **用压缩包装的**：退出游戏，把「仅插件」包（plugins）解压到游戏目录、覆盖旧文件。**所有 Mod 要一起更新**，不要只换 Hextech。
- 装好后点主菜单的「Mod」按钮，确认 DBModKit 是 0.31.0、Hextech 是 0.31.0，都在运行。

### 可能的冲突和处理

| 现象 | 原因 | 处理 |
|---|---|---|
| 「Mod」按钮上有红色「!」，Hextech 显示"需要 DBModKit 0.31.0" | 运行环境还是旧的 | 退出游戏，重新运行运行环境的 `install.bat`（压缩包用户：覆盖「仅插件」包） |
| 订阅了工坊的新版，游戏里还是旧版（Mod 列表显示"已经装了同一个插件，用的是本地那份"） | `BepInEx\plugins` 里有以前用压缩包装的同名 Mod，**本地那份优先** | 二选一：要用工坊版，就删掉 `BepInEx\plugins` 下对应的文件夹（比如 `Hextech`；**不要删 `DBModKit`**，它由 install.bat 管理）；要用压缩包版，就用新的压缩包覆盖 |
| 开发者环境：`BepInEx\scripts` 里有同名 dll | 同上，`scripts` 里的优先 | 删掉 `scripts` 里的旧 dll |
| 联机时提示"装的不一样"，海克斯等改玩法的内容停用 | 房间里有人还是旧版本（联机握手会比对 DBModKit 和每个 Mod 的版本） | **房间里每个人都更新到 0.31**；或者房主在 F9 → Mod 页 → DBModKit → 联机里勾选"允许改玩法的内容" |
| 关掉了 Hextech 的总开关，联机还是提示"装的不一样" | 0.31 起关着的 Hextech 不算改玩法的内容；对方如果还是旧版，旧版会把自己的 Hextech 算进去 | 让对方也更新 |
| 更新后进游戏报错、Mod 都没加载 | 运行环境文件不完整（比如游戏开着时运行了 install.bat） | 退出游戏，运行 `uninstall.bat`，再运行 `install.bat`；工坊条目可以在 Steam 里"验证"后再装 |

设置文件（`BepInEx\config` 下的 .cfg、.json）和存档都不受影响，不需要删。

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

## 包含的 Mod

| Mod | 版本 | 一句话 |
|---|---|---|
| DBModKit | 0.31.0 | 公共依赖库，其他 Mod 都需要它；主菜单"Mod"按钮、Mod 列表、联机握手、创意工坊加载器、内容包 |
| SmartSkill | 0.7.0 | 自动释放技能时，给每个技能单独设条件（血量、敌人数量、Boss、斩杀……） |
| SmartMove | 0.3.0 | 自动躲避地面红色预警（F8 开关） |
| AutoPickup | 0.1.0 | 战斗中自动捡身边的掉落物，可按类别开关 |
| DamageMeter | 0.4.1 | 伤害统计：按普攻、技能、召唤物、装备等拆开；Steam 联机时显示队友的伤害 |
| Hextech | 0.31.0 | 海克斯强化：每局 4 次三选一，专属技能强化 + 参照海克斯大乱斗、斗魂竞技场的通用卡和队友卡；联机时每个人装的一样才生效 |
| DBStudio | 0.10.0 | 游戏内的 Mod 工作台（F9）：调试、规则、强化、数值、皮肤、Mod 设置 |

安装和用法见包里的《安装说明.txt》。

也可以从 Steam 创意工坊安装：先订阅 [Mod 运行环境](https://steamcommunity.com/sharedfiles/filedetails/?id=3824690979)，退出游戏后运行条目文件夹里的 `install.bat`，再订阅想要的 Mod（[SmartSkill](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691150)、[SmartMove](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691259)、[AutoPickup](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691366)、[DamageMeter](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691468)、[Hextech](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691564)、[DBStudio](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691666)）。第一次加载时游戏里会让你确认。

各 Mod 的详细改动见源码目录里的 `CHANGELOG.md`。

## 必读

- **装了 Mod 不上传排行榜**（游戏作者的规则）。结算成绩不会上传，其他功能不受影响。想冲榜请先卸载。
- **联机**：装了 Mod 的玩家之间会自动比对"改玩法的内容"（自定义技能、遗物、装备、内容包、DBStudio 玩法包）。
  单人、房间里每个人装的都一样、或房主手动允许时才生效，否则本局只保留外观类功能（皮肤）。辅助类功能（自动释放、躲避、拾取、伤害统计）只影响你自己，不受影响。
- 许可证：本套 Mod 使用 MIT 许可证（包里的 `LICENSE`）；完整包里的 BepInEx 等第三方组件的许可证在 `licenses/` 文件夹。
- 遇到问题：在 DBStudio（F9）的调试页点"导出"，把生成的 `BepInEx/DBModKit/report-时间.txt` 发给作者。

## 未实测的功能

下面这些已经写好、能编译，但还没有在游戏里完整验证过。用到时出问题的可能性比较大，欢迎反馈。

| 功能 | 没测的部分 |
|---|---|
| 自定义装备（DBModKit 0.24.0） | 单人下已测（挂特效、新装备、内容包、构筑数据过滤）；没测：联机、装备强化、重试波次、在神器三选一里实际选中 |
| 依赖和冲突（DBModKit 0.23.0） | 缺依赖时的提示和"订阅"按钮、两个 Mod 抢自动释放/走位时在设置里指定、两个 Mod 改同一项数值的提示 |
| 排行榜拦截 | 实际打到结算、出现"是否上传排名"时的拦截（拦截装上了，但还没在真实结算时触发过） |
| 国王对决 | 在别人的房间里不把国王快照发给房主 |
| 和没装 Mod 的人同房 | 握手应显示"有人没装 · 停用"；你放自定义内容时对方不应掉线 |
| 局域网模式 | 局域网没有 Steam 大厅，确认不了别人装了什么：改玩法的内容一律停用，皮肤同步、队伍伤害统计不支持。整体没测过 |
| Hextech 0.4.0–0.31.0（0.31 新增，**全部未实测**） | 联机（握手、休息时才弹、房主的敌人海克斯）；全部队友卡和让经济的卡（要两人联机）；第三批到第五批的卡（单人也没测）：免死、追加落雷剑气、棱彩蛋和嬗变的额外选择、自动施放、代价卡、撕咬 / 炸弹 / 雷之一闪招牌 |
| Hextech（0.3.0 及以前） | "回响"等释放时触发的效果、坦克引擎死亡时扣一半、第二批的卡（男枪、坦克引擎、豪猪等）在重试波次时的还原 |
| DamageMeter | 联机时召唤物伤害的归属 |
| SmartSkill 斩杀预测 | 处决、点金术、撕咬的"能斩杀的敌人 ≥"条件和命中校准 |
| 游戏更新后的映射重建（开发者工具 `tools/Remap`） | 还没经历过真实的游戏更新 |

## 已知问题

- 自定义技能、遗物、装备在玩家信息面板和结算界面里看不到。
- 没装 ModKit 的玩家收不到联机握手的提示。
- 界面目前只有中文（Mod 内部已经预留多语言接口）。

---

# DungeonBrawl Mods 0.31

[中文](#地下城竞技场-mods-03)

## Upgrading (read this first)

**This update changes the runtime (DBModKit 0.26.0 → 0.31.0). You must reinstall the runtime after updating, otherwise the new Hextech won't load.**

- **Installed from the Steam Workshop**: wait for Steam to download the update, **quit the game**, and **double-click `install.bat` again** in the runtime item's folder
  (`<Steam library>\steamapps\workshop\content\4246640\3824690979\`), then start the game. The in-game "runtime update available" notice means the same thing.
- **Installed from the zip**: quit the game and extract the "plugins" package into the game folder, overwriting the old files. **Update all mods together**, not just Hextech.
- Afterwards, click the main-menu "Mod" button and check that DBModKit is 0.31.0 and Hextech is 0.31.0, both running.

### Possible conflicts

| Symptom | Cause | Fix |
|---|---|---|
| Red "!" on the "Mod" button, Hextech says it needs DBModKit 0.31.0 | The runtime is still the old version | Quit the game and run the runtime's `install.bat` again (zip users: extract the new "plugins" package) |
| You subscribed to the new Workshop version but the game still runs the old one (the mod list says "already installed, using the local copy") | `BepInEx\plugins` has an older copy from the zip; **the local copy wins** | Pick one: to use the Workshop version, delete that mod's folder under `BepInEx\plugins` (e.g. `Hextech`; **don't delete `DBModKit`**, install.bat manages it); to use the zip version, extract the new zip |
| Developer setups: a same-named dll in `BepInEx\scripts` | Same as above, `scripts` wins | Delete the old dll in `scripts` |
| In multiplayer, "installed differently" and Hextech / other gameplay content is disabled | Someone in the room is still on an old version (the handshake compares DBModKit and every mod's version) | **Everyone updates to 0.31**, or the host ticks "allow gameplay content" under F9 → Mod → DBModKit → Multiplayer |
| Hextech's main switch is off but multiplayer still says "installed differently" | From 0.31 a disabled Hextech doesn't count as gameplay content, but an older version on the other side still counts its own | Ask them to update too |
| After updating, errors on start and no mods load | Incomplete runtime files (e.g. install.bat ran while the game was open) | Quit the game, run `uninstall.bat`, then `install.bat`; for the Workshop item you can "verify" it in Steam first |

Your settings (the .cfg / .json files under `BepInEx\config`) and saves are unaffected; don't delete them.

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

## What's included

| Mod | Version | Summary |
|---|---|---|
| DBModKit | 0.31.0 | Shared library required by all other mods; main-menu "Mod" button, mod list, multiplayer handshake, Workshop loader, content packs |
| SmartSkill | 0.7.0 | Per-skill conditions for auto-cast (HP, enemy count, bosses, execute thresholds, …) |
| SmartMove | 0.3.0 | Automatically steps out of red ground telegraphs (toggle with F8) |
| AutoPickup | 0.1.0 | Picks up nearby drops during combat, filterable by category |
| DamageMeter | 0.4.1 | Damage breakdown by basic attacks, skills, summons, equipment…; shows teammates' damage in Steam lobbies |
| Hextech | 0.31.0 | Hextech augments: 4 picks per run — signature-skill upgrades plus generic and teammate augments inspired by ARAM: Mayhem and Arena; in multiplayer everyone needs the same mods |
| DBStudio | 0.10.0 | In-game mod workbench (F9): debug, rules, augments, values, skins, mod settings |

See `INSTALL-EN.txt` in the package for installation and usage.

You can also install from the Steam Workshop: subscribe to the [Mod Runtime](https://steamcommunity.com/sharedfiles/filedetails/?id=3824690979), quit the game and run `install.bat` in the item's folder, then subscribe to the mods you want ([SmartSkill](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691150), [SmartMove](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691259), [AutoPickup](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691366), [DamageMeter](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691468), [Hextech](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691564), [DBStudio](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691666)). You'll be asked to confirm the first time they load.

 Per-mod change logs are the `CHANGELOG.md` files in the source.

## Read this first

- **No leaderboard uploads while mods are installed** (the game author's rule). Your run results are not uploaded; nothing else is affected. Uninstall the mods if you want to compete on the leaderboard.
- **Multiplayer**: players with the mods compare their "gameplay-changing content" (custom skills, relics, equipment, content packs, DBStudio packs).
  It only takes effect when you're alone, when everyone in the room has exactly the same content, or when the host explicitly allows it; otherwise only cosmetics (skins) stay on for that run. Helper mods (auto-cast, dodging, pickup, damage meter) only affect yourself and always work.
- License: these mods are MIT licensed (`LICENSE` in the package); licenses of BepInEx and other third-party components in the full package are in the `licenses/` folder.
- Problems? In DBStudio (F9), Debug page → "导出" (Export), and send the generated `BepInEx/DBModKit/report-<time>.txt` to the author.

## Not yet tested in game

These are implemented and compile, but haven't been fully verified in the game yet. They're the most likely to break — reports are welcome.

| Feature | Untested part |
|---|---|
| Custom equipment (DBModKit 0.24.0) | Tested in single-player (effects on existing equipment, new equipment, content packs, build-data filtering); untested: multiplayer, upgrading, retrying a wave, actually picking it from the artifact choice |
| Dependencies and conflicts (DBModKit 0.23.0) | Missing-dependency notice and "Subscribe" button, choosing which mod handles auto-cast/movement, warning when two mods change the same value |
| Leaderboard block | Blocking at the real "upload your score?" dialog at the end of a run (the hook is installed but hasn't fired in a real run yet) |
| King duel | Not sending the king snapshot to the host when you're in someone else's room |
| Rooms with players who don't have the mods | Handshake should show "someone doesn't have it · disabled"; they shouldn't disconnect when you use custom content |
| LAN mode | There's no Steam lobby on LAN, so the mods can't check what others have installed: gameplay-changing content is always disabled, skin sync and team damage aren't supported. Not tested at all |
| Hextech 0.4.0–0.31.0 (new in 0.31, **all untested**) | Multiplayer (handshake, rest-phase picks, host's enemy augments); all teammate and feed-the-carry augments (need two players); batches 3–5 (not even single-player): cheat death, extra lightning / sword waves, extra picks from Prismatic Egg and Transmute, Auto-cast, trade-offs, Bite / Bomb / Thunder Flash signatures |
| Hextech (0.3.0 and earlier) | On-cast effects such as "Echo", Tank Engine losing half on death, restoring the second-batch augments (Shotgun, Tank Engine, Porcupine…) after retrying a wave |
| DamageMeter | Attribution of summon damage in multiplayer |
| SmartSkill execute prediction | The "killable enemies >=" condition for 处决 / 点金术 / 撕咬 and its hit-based calibration |
| Remapping after a game update (developer tool `tools/Remap`) | Not yet run against a real game update |

## Known issues

- Custom skills, relics and equipment don't appear in the player info panel or the results screen.
- Players without ModKit don't see the multiplayer handshake notice.
- The in-game UI is Chinese only for now (the mods already have a localization API in place).
