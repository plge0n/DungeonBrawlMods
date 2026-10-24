# DBModKit changelog (English)

## 0.28.0
- Teammate APIs using the game's own commands (work even if teammates don't have mods): `Game.Teammates`, `Player.HealAlly`, `Player.ShieldNearbyPlayers`, `Player.BlessPlayer`, `Player.PlayEffect`; `DamageArea` with a null effect plays no effect, `DamageArea(..., spareTeammates)`, `Role.IsTeammateMonster`, `GameEventKind.TeammateLevelUp`, `Player.GiveGold`

## 0.27.0
- Enemy boosts follow the host in multiplayer and apply to the whole room (the host now scales monster damage to every player, not just itself); boosts set on clients have no effect
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
