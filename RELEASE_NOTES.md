# 地下城竞技场 Mods 0.40 · 打榜白名单 + 自动锻体

## 升级

1. 退出游戏。
2. 创意工坊用户：等 Steam 把更新下载完，到运行环境的条目文件夹（`<Steam 库>\steamapps\workshop\content\4246640\3824690979\`）**重新运行 `install.bat`**。
   压缩包用户：把「仅插件」包（plugins）解压到游戏目录，覆盖旧文件。
3. 启动游戏，点主菜单的「Mod」按钮，确认 DBModKit、DBStudio 和 AutoForge 都是 0.40.0。打榜模式开启时 DBStudio 会停用，这是正常现象。

升级不会改动设置和存档；使用永久写入功能会修改存档。Mod 没恢复、联机提示"装的不一样"等情况见[常见问题](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/%E5%B8%B8%E8%A7%81%E9%97%AE%E9%A2%98.md)。

## 本版改动

- **打榜白名单**：经游戏作者同意，DBModKit、DamageMeter、SmartSkill、AutoPickup、SmartMove、AutoForge 可以打榜；只装这些 Mod 时不用开启打榜模式。
- **打榜模式**：在主菜单「Mod」里开启，重启后停用 Hextech、DBStudio、内容包等玩法 Mod；关闭后再重启恢复。
- **上传资格**：其他插件、内容包、ScriptEngine、UnityExplorer、额外补丁或不合格的队友会阻止上传，游戏里会写明原因。联机时全队都要合格，旧版 ModKit 队友需更新。
- **AutoForge 0.40.0：自动锻体**。支持自定义属性权重和均衡模式，F10 开关，在「Mod → AutoForge → 设置」里调整。候选全是权重 0 的属性时照常手选；不会自动购买锻造器，联机只操作本机。
- **DBStudio 0.40.0**：Mod 页的排行榜说明改为新规则；装着时不能打榜，可以在 Mod 菜单开启打榜模式并重启。

以上都没有实机测试，有问题请反馈。只检查了编译和启动到主菜单；真实结算、国王快照、联机、模式重启与恢复、自动锻体及重试恢复仍待验证。

## 适配的游戏版本

每个 Mod 版本只适配一个游戏版本。游戏作者会在 Steam 放出历史版本，想固定游戏版本就下载下表里对应的「仅插件」包，步骤见[常见问题](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/%E5%B8%B8%E8%A7%81%E9%97%AE%E9%A2%98.md)。

| Mod 版本 | 适配游戏版本 | DBModKit |
|---|---|---|
| 0.40 | 7.13 | 0.40.0 |
| 0.39 | 7.13 | 0.39.0 |
| 0.38 | 7.13 | 0.38.0 |
| 0.37 | 7.11 | 0.37.0 |
| 0.36 | 7.10 | 0.36.0 |
| 0.35 | 7.06 | 0.35.0 |
| 0.34 | 7.03 | 0.34.0 |
| 0.33 | 7.02 | 0.33.0 |
| 0.32 | 7.01 | 0.32.0 |
| 0.31 | 6.45 | 0.31.0 |
| 0.2 | 6.45 | 0.26.0 |
| 0.1、0.1.1 | 6.45 | 0.24.0 |

[完整更新历史](https://github.com/plge0n/DungeonBrawlMods/blob/main/CHANGELOG.md) · [常见问题](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/%E5%B8%B8%E8%A7%81%E9%97%AE%E9%A2%98.md) · [创意工坊：Mod 运行环境](https://steamcommunity.com/sharedfiles/filedetails/?id=3824690979)

<details>
<summary>English</summary>

## DungeonBrawl Mods 0.40 · leaderboard whitelist + AutoForge (game 7.13)

### Upgrading

1. Quit the game.
2. Workshop users: once Steam has downloaded the update, **run `install.bat` again** in the runtime item's folder (`<Steam library>\steamapps\workshop\content\4246640\3824690979\`).
   Zip users: extract the "plugins" package into the game folder, overwriting the old files.
3. Start the game, click the main-menu "Mod" button and check that DBModKit, DBStudio and AutoForge are all 0.40.0. DBStudio is disabled when Ranked Mode is active; this is expected.

Upgrading leaves settings and saves intact; permanent card unlock modifies the save. If the mods don't come back or multiplayer says "installed differently", see the [FAQ](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/FAQ.md).

### Changes

- **Leaderboard whitelist**: with the game author's approval, DBModKit, DamageMeter, SmartSkill, AutoPickup, SmartMove and AutoForge can upload scores. Ranked Mode is optional if you only use these mods.
- **Ranked Mode**: enable it in the main-menu Mod window and restart to disable Hextech, DBStudio, content packs and other gameplay mods. Turn it off and restart to restore them.
- **Upload eligibility**: other plugins, content packs, ScriptEngine, UnityExplorer, additional patches or ineligible teammates block uploads; the game explains why. Everyone in a multiplayer team must qualify; teammates with older ModKit versions need to update.
- **AutoForge 0.40.0: automatic forging choices**. Use custom attribute weights or balanced mode, toggle with F10, and configure under Mod → AutoForge → Settings. If all offered attributes have weight 0, choose manually as usual. It never buys forging items and only controls the local player in multiplayer.
- **DBStudio 0.40.0**: the Mod page explains the new leaderboard rules. Having it loaded blocks uploads; enable Ranked Mode in the Mod window and restart to disable it.

None of the above has been play-tested; please report problems. Only compilation and main-menu startup were checked. Real results, king snapshots, multiplayer, mode restart/restore, automatic forging and retry restoration remain unverified.

### Game versions

Each mod release works with exactly one game version. The game's author is publishing older game versions on Steam; to stay on one, download the matching "plugins" package from the table below (steps in the [FAQ](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/FAQ.md)).

| Mods version | Game version | DBModKit |
|---|---|---|
| 0.40 | 7.13 | 0.40.0 |
| 0.39 | 7.13 | 0.39.0 |
| 0.38 | 7.13 | 0.38.0 |
| 0.37 | 7.11 | 0.37.0 |
| 0.36 | 7.10 | 0.36.0 |
| 0.35 | 7.06 | 0.35.0 |
| 0.34 | 7.03 | 0.34.0 |
| 0.33 | 7.02 | 0.33.0 |
| 0.32 | 7.01 | 0.32.0 |
| 0.31 | 6.45 | 0.31.0 |
| 0.2 | 6.45 | 0.26.0 |
| 0.1, 0.1.1 | 6.45 | 0.24.0 |

[Release history](https://github.com/plge0n/DungeonBrawlMods/blob/main/CHANGELOG.md) · [FAQ](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/FAQ.md) · [Workshop: Mod Runtime](https://steamcommunity.com/sharedfiles/filedetails/?id=3824690979)

</details>
