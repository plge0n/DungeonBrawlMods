# Changelog

## 0.41.0

- Documented how this mod relates to the game's own Auto Forger (game 7.15): the Auto Forger buys gold forgings in bulk without showing choices, while AutoForge keeps handling the pick-one-of-three choices from regular/gold forgers, hero passives, Hextech and so on. They don't conflict; no code changes.
- Game 7.15 removed epic forging choices, so epic-tier offers no longer appear.

## 0.40.0

- First release: automatic forging choices using custom attribute weights, or balanced mode to prioritize attributes that have gained less from forging this run. If all offered attributes have weight 0, choose manually as usual.
- Toggle with F10; configure mode, weights and hotkey under Mod → AutoForge → Settings. Shows a notice after choosing. Never buys forging items automatically.
- Only controls the local player in multiplayer. Approved by the game author for leaderboards. Retrying a wave restores accumulated forging gains with the checkpoint.
- **No gameplay testing**; only compilation and main-menu startup were checked. Automatic choices, multiplayer and retry restoration still need verification.
