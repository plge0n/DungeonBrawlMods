# FAQ

中文：[常见问题.md](常见问题.md)

## What's included

| Mod | Summary |
|---|---|
| DBModKit | Shared library required by all other mods; main-menu "Mod" button, mod list, multiplayer handshake, Workshop loader, content packs |
| SmartSkill | Per-skill conditions for auto-cast (HP, enemy count, bosses, execute thresholds, …) |
| SmartMove | Automatically steps out of red ground telegraphs (toggle with F8) |
| AutoPickup | Picks up nearby drops during combat, filterable by category |
| DamageMeter | Damage breakdown by basic attacks, skills, summons, equipment…; shows teammates' damage in Steam lobbies |
| Hextech | Hextech augments: 4 picks per run — signature-skill upgrades plus generic and teammate augments inspired by ARAM: Mayhem and Arena; in multiplayer everyone needs the same mods |
| DBStudio | In-game mod workbench (F9): debug, rules, augments, values, skins, mod settings |

See `INSTALL-EN.txt` in the package for installation and usage.

You can also install from the Steam Workshop: subscribe to the [Mod Runtime](https://steamcommunity.com/sharedfiles/filedetails/?id=3824690979), quit the game and run `install.bat` in the item's folder, then subscribe to the mods you want ([SmartSkill](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691150), [SmartMove](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691259), [AutoPickup](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691366), [DamageMeter](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691468), [Hextech](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691564), [DBStudio](https://steamcommunity.com/sharedfiles/filedetails/?id=3824691666)). You'll be asked to confirm the first time they load.

Changes in each release are in [CHANGELOG.md](../CHANGELOG.md); per-mod change logs are the `CHANGELOG.en.md` files in the source.

## Read this first

- **No leaderboard uploads while mods are installed** (the game author's rule). Your run results are not uploaded; nothing else is affected. Uninstall the mods if you want to compete on the leaderboard.
- **Multiplayer**: players with the mods compare their "gameplay-changing content" (custom skills, relics, equipment, content packs, DBStudio packs).
  It only takes effect when you're alone, when everyone in the room has exactly the same content, or when the host explicitly allows it; otherwise only cosmetics (skins) stay on for that run. Helper mods (auto-cast, dodging, pickup, damage meter) only affect yourself and always work.
- License: these mods are MIT licensed (`LICENSE` in the package); licenses of BepInEx and other third-party components in the full package are in the `licenses/` folder.
- Problems? In DBStudio (F9), Debug page → "导出" (Export), and send the generated `BepInEx/DBModKit/report-<time>.txt` to the author.

## Problems after an update

| Symptom | Cause | Fix |
|---|---|---|
| On start-up: "game version … is not compatible with DBModKit", all mods disabled (the game itself still works) | The game updated and the installed runtime isn't the one made for this game version | If a new mod release is out: quit the game and run `install.bat` in the runtime item again (zip users: extract the new "plugins" package). If not: wait for it, or switch the game back to the version the mods support (see "Staying on a game version" below) |
| You subscribed to the new Workshop version but the game still runs the old one (the mod list says "already installed, using the local copy") | `BepInEx\plugins` has an older copy from the zip; **the local copy wins** | Pick one: to use the Workshop version, delete that mod's folder under `BepInEx\plugins` (e.g. `Hextech`; **don't delete `DBModKit`**, install.bat manages it); to use the zip version, extract the new zip |
| Developer setups: a same-named dll in `BepInEx\scripts` | Same as above, `scripts` wins | Delete the old dll in `scripts` |
| In multiplayer, "installed differently" and Hextech / other gameplay content is disabled | Someone in the room has a different version (the handshake compares DBModKit and every mod's version) | **Everyone updates to the same release**, or the host ticks "allow gameplay content" under F9 → Mod → DBModKit → Multiplayer |
| Hextech's main switch is off but multiplayer still says "installed differently" | From 0.31 a disabled Hextech doesn't count as gameplay content, but a pre-0.31 version on the other side still counts its own | Ask them to update too |
| After updating, errors on start and no mods load | Incomplete runtime files (e.g. install.bat ran while the game was open) | Quit the game, run `uninstall.bat`, then `install.bat`; for the Workshop item you can "verify" it in Steam first |

Your settings (the .cfg / .json files under `BepInEx\config`) and saves are unaffected; don't delete them.

## Staying on a game version

Every game update re-obfuscates the game's code, so each mod release works with exactly one game version (the main-menu "Mod" button shows your game version).
The game's author is publishing older game versions on Steam (right-click the game in your library → Properties → Betas). To stay on a game version instead of following updates:

1. In the "Game versions" table of the release notes (`RELEASE_NOTES.md`, or the latest page on [GitHub Releases](https://github.com/plge0n/DungeonBrawlMods/releases)), find the mod release for that game version and download its "plugins" package.
2. Quit the game and extract it into the game folder, overwriting files. Zip-installed mods (in `BepInEx\plugins`) take priority over the Workshop ones, so later Workshop updates won't interfere.
3. While you stay on an older game version, **don't** run the runtime item's `install.bat` when it updates; you can ignore the in-game "runtime update available" notice.

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

## Known issues

- Custom skills, relics and equipment don't appear in the player info panel or the results screen.
- Players without ModKit don't see the multiplayer handshake notice.
- The in-game UI is Chinese only for now (the mods already have a localization API in place).
