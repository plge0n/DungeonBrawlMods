# 地下城竞技场 Mods 0.1

[English](#dungeonbrawl-mods-01)

第一个公开版本。所有功能都基于游戏 6.45 版本开发和测试，游戏更新后如果不兼容，会在进游戏时提示，Mod 自动停用，游戏照常可玩。

## 包含的 Mod

| Mod | 版本 | 一句话 |
|---|---|---|
| DBModKit | 0.24.0 | 公共依赖库，其他 Mod 都需要它；主菜单"Mod"按钮、Mod 列表、联机握手、创意工坊加载器、内容包 |
| SmartSkill | 0.7.0 | 自动释放技能时，给每个技能单独设条件（血量、敌人数量、Boss、斩杀……） |
| SmartMove | 0.2.0 | 自动躲避地面红色预警（F9 开关） |
| AutoPickup | 0.1.0 | 战斗中自动捡身边的掉落物，可按类别开关 |
| DamageMeter | 0.4.0 | 伤害统计：按普攻、技能、召唤物、装备等拆开；Steam 联机时显示队友的伤害 |
| Hextech | 0.1.1 | 海克斯强化：每局 4 次三选一，强化英雄的专属技能（只在单人时生效） |
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
| Hextech | "回响"等释放时触发的效果、重试波次后还原强化、有人进房后停用 |
| DamageMeter | 联机时召唤物伤害的归属 |
| SmartSkill 斩杀预测 | 处决、点金术、撕咬的"能斩杀的敌人 ≥"条件和命中校准 |
| 游戏更新后的映射重建（开发者工具 `tools/Remap`） | 还没经历过真实的游戏更新 |

## 已知问题

- SmartMove 的开关和 DBStudio 都默认用 F9：可以在 DBStudio 的 Mod 页里给其中一个改快捷键。
- 自定义技能、遗物、装备在玩家信息面板和结算界面里看不到。
- 没装 ModKit 的玩家收不到联机握手的提示。
- 界面目前只有中文（Mod 内部已经预留多语言接口）。

---

# DungeonBrawl Mods 0.1

[中文](#地下城竞技场-mods-01)

First public release. Everything was built and tested against game version 6.45. If a game update breaks compatibility, you'll see a notice on start-up and the mods disable themselves; the game keeps working.

## What's included

| Mod | Version | Summary |
|---|---|---|
| DBModKit | 0.24.0 | Shared library required by all other mods; main-menu "Mod" button, mod list, multiplayer handshake, Workshop loader, content packs |
| SmartSkill | 0.7.0 | Per-skill conditions for auto-cast (HP, enemy count, bosses, execute thresholds, …) |
| SmartMove | 0.2.0 | Automatically steps out of red ground telegraphs (toggle with F9) |
| AutoPickup | 0.1.0 | Picks up nearby drops during combat, filterable by category |
| DamageMeter | 0.4.0 | Damage breakdown by basic attacks, skills, summons, equipment…; shows teammates' damage in Steam lobbies |
| Hextech | 0.1.1 | Hextech augments: 4 picks per run that upgrade your hero's signature skill (single-player only) |
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
| Hextech | On-cast effects such as "Echo", restoring augments after retrying a wave, disabling when someone joins |
| DamageMeter | Attribution of summon damage in multiplayer |
| SmartSkill execute prediction | The "killable enemies >=" condition for 处决 / 点金术 / 撕咬 and its hit-based calibration |
| Remapping after a game update (developer tool `tools/Remap`) | Not yet run against a real game update |

## Known issues

- SmartMove's toggle and DBStudio both default to F9: change one of them in DBStudio's Mod page.
- Custom skills, relics and equipment don't appear in the player info panel or the results screen.
- Players without ModKit don't see the multiplayer handshake notice.
- The in-game UI is Chinese only for now (the mods already have a localization API in place).
