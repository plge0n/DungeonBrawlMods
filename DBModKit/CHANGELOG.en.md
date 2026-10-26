# DBModKit changelog (English)

## 0.35.0
**Updated for game version 7.06. After updating, quit the game and run install.bat in the runtime item again** (zip users: extract the new "plugins" package), otherwise all mods stay disabled.

- Not play-tested; if something breaks, please export a report and let us know

## 0.34.0
**Updated for game version 7.03. After updating, quit the game and run install.bat in the runtime item again** (zip users: extract the new "plugins" package), otherwise all mods stay disabled.

- Not play-tested; if something breaks, please export a report and let us know

## 0.33.0
**Updated for game version 7.02. After updating, quit the game and run install.bat in the runtime item again** (zip users: extract the new "plugins" package), otherwise all mods stay disabled.

- Fix: when the game is installed in a folder with non-ASCII characters (the default Steam folder name is Chinese), every Harmony patch failed and all mods were disabled (a Mono path-encoding bug). ModKit now switches the working directory while patching; the installer turns on BepInEx's `LoadDumpedAssemblies`, and if it is off ModKit turns it on and asks you to restart the game once

## 0.32.0
**Updated for game version 7.01. After updating, quit the game and run install.bat in the runtime item again** (zip users: extract the new "plugins" package), otherwise all mods stay disabled.

- Every game update re-obfuscates the code; older ModKit versions detect this and disable every mod (the game itself keeps working)

## 0.31.0
**After updating, quit the game and run install.bat in the runtime item again** (zip users: extract the new "plugins" package), otherwise mods that need it (Hextech 0.31.0) won't load.

- `SkillForms`: `mod.SetBiteGrowth`, `mod.SetBombRange`, `mod.SetThunderOrbs` (signature skill tweaks)

## 0.30.0
- `mod.OnLethalDamage` (cancel a killing blow on the local player), `Player.CastSkillFree` (fire an existing game skill for free)

## 0.29.0
- `GameEventKind.Died`, `Player.AddAttackRange`, `SkillSlot.SetAutoCast`

## 0.28.0
- Teammate APIs using the game's own commands (work even if teammates don't have mods): `Game.Teammates`, `Player.HealAlly`, `Player.ShieldNearbyPlayers`, `Player.GiveBuff` (skill haste, armor, DoT damage), `Player.PlayEffect`; `DamageArea` with a null effect plays no effect, `DamageArea(..., spareTeammates)`, `Role.IsTeammateMonster`, `GameEventKind.TeammateLevelUp`, `Player.GiveGold`

## 0.27.0
- Enemy attack boost now applies wherever the attacking monster is owned (each player owns their own monsters in multiplayer); HP/shield boosts only take effect on the host
- `LobbySync.ReadHost(key)`: read the lobby owner's member data

## 0.26.0
- `mod.SetShotgunAmmo` (Shotgun hero's ammo cap and dash reload), `Player.AddAttackCount`, `Player.DropItem`, `Player.DamageArea`

## 0.25.1
- `HitInfo.RawDamage`: damage dealt including overkill (`Damage` is capped at the target's HP + shield)
- Mod icons in the three-choice panel keep their own colors (the game tints icons by quality)
- Fix: hot-reloaded plugins (ScriptEngine renames the assembly) could not find their lang/ and assets/ folders

## 0.25.0
- New APIs for generic augments: `Player.AddAttribute`, `Player.ActiveSkills`, `SkillSlot.ResetCooldown`, `Forging.Open` (a free attribute forge), `mod.SetEnemyBoost` (enemy HP/attack multipliers), `mod.SetSoulSplash` (Desert Reaper's empowered strike splashes)
- Fix: the workshop loader no longer loads a subscribed plugin that is also in `BepInEx/scripts` (dev setups ran both versions)

## 0.24.0
- Custom equipment: attach custom effects to existing equipment (`RegisterEquipEffect`), or add brand-new equipment (`RegisterEquipment`, `CustomEquipment.Give`) that can appear in the artifact pick
- Content packs support `equipEffects` and `equipment`
- Custom equipment counts towards the multiplayer handshake and is removed from build data sent to other players
- Tested in single-player; not yet tested: multiplayer, upgrading, retrying a wave, picking it from the artifact choice
