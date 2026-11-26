# 地下城竞技场 Mods 0.46 · 适配游戏 7.20

## 升级

1. 退出游戏。
2. 创意工坊用户：等 Steam 把 Hextech 的更新下载完即可。**运行环境没变（DBModKit 仍是 0.45.0），不用重新运行 `install.bat`。**
   压缩包用户：把「仅插件」包（plugins）解压到游戏目录，覆盖旧文件。
3. 启动游戏，点主菜单的「Mod」按钮，确认 Hextech 是 0.46.0。联机时房间里每个人都要更新到同一版本。

升级不会改动设置和存档；使用永久写入功能会修改存档。Mod 没恢复、联机提示"装的不一样"等情况见[常见问题](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/%E5%B8%B8%E8%A7%81%E9%97%AE%E9%A2%98.md)。

## 本版改动

- **Hextech 0.46.0：修复长局里海克斯只在开局弹一次**（玩家在狂欢模式里反馈）。
  - 错过的三选一会在之后的休息阶段补上，每次休息最多补一次。
  - 三选一次数按波数定：20/30/40/50 波默认 4/5/6/7 次（含开局），棱彩上限跟着提高；设置 `General/Rounds` 可以手动设为 4–8 次。4 次时抽到的品质和以前一样。
  - 联机中途有人装的不一样而暂停三选一时，屏幕上会说明原因，F7 也能看到。运行环境会把这种暂停保持到本局结束。
  - 防守模式仍然只有开局一次，见常见问题。
- **验证范围：** 编译、离线测试、主菜单启动和 Mod 加载通过；**局内没有实测**。遇到问题请在 Mod 列表里导出报告反馈。

## 适配的游戏版本

每个 Mod 版本只适配一个游戏版本。游戏作者会在 Steam 放出历史版本，想固定游戏版本就下载下表里对应的「仅插件」包，步骤见[常见问题](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/%E5%B8%B8%E8%A7%81%E9%97%AE%E9%A2%98.md)。

| Mod 版本 | 适配游戏版本 | DBModKit |
|---|---|---|
| 0.46 | 7.20 | 0.45.0 |
| 0.45 | 7.20 | 0.45.0 |
| 0.44 | 7.19 | 0.44.0 |
| 0.43 | 7.18 | 0.43.0 |
| 0.42 | 7.16 | 0.42.0 |
| 0.41 | 7.15 | 0.41.0 |
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

## DungeonBrawl Mods 0.46 · updated for game 7.20

### Upgrading

1. Quit the game.
2. Workshop users: just let Steam download the Hextech update. **The runtime is unchanged (DBModKit stays at 0.45.0), so there is no need to run `install.bat` again.**
   Zip users: extract the "plugins" package into the game folder, overwriting the old files.
3. Start the game, click the main-menu "Mod" button and check that Hextech is 0.46.0. In multiplayer everyone needs the same version.

Upgrading leaves settings and saves intact; permanent card unlock modifies the save. If the mods don't come back or multiplayer says "installed differently", see the [FAQ](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/FAQ.md).

### Changes

- **Hextech 0.46.0: fixed Hextech picks stopping after the opening pick in long runs** (reported in Carnival mode).
  - Missed picks carry over to later breaks, up to one per break.
  - The number of picks follows the run length: 4/5/6/7 for 20/30/40/50 waves (including the opening pick), with a higher prismatic cap; `General/Rounds` sets a fixed 4–8. With 4 picks the tiers roll exactly as before.
  - If picks pause mid-run because someone in the lobby has different mods, a screen notice and the F7 list explain why. The runtime keeps that pause until the run ends.
  - Defense mode still only gets the opening pick; see the FAQ.
- **Verification:** Build, offline tests, startup to the main menu, and mod loading passed; **not play-tested in a match**. If something breaks, please export a report from the Mod list.

### Game versions

Each mod release works with exactly one game version. The game's author is publishing older game versions on Steam; to stay on one, download the matching "plugins" package from the table below (steps in the [FAQ](https://github.com/plge0n/DungeonBrawlMods/blob/main/docs/FAQ.md)).

| Mods version | Game version | DBModKit |
|---|---|---|
| 0.46 | 7.20 | 0.45.0 |
| 0.45 | 7.20 | 0.45.0 |
| 0.44 | 7.19 | 0.44.0 |
| 0.43 | 7.18 | 0.43.0 |
| 0.42 | 7.16 | 0.42.0 |
| 0.41 | 7.15 | 0.41.0 |
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
