# Changelog

## 0.40.0

- First release: automatic forging choices using custom attribute weights, or balanced mode to prioritize attributes that have gained less from forging this run. If all offered attributes have weight 0, choose manually as usual.
- Toggle with F10; configure mode, weights and hotkey under Mod → AutoForge → Settings. Shows a notice after choosing. Never buys forging items automatically.
- Only controls the local player in multiplayer. Approved by the game author for leaderboards. Retrying a wave restores accumulated forging gains with the checkpoint.
- **No gameplay testing**; only compilation and main-menu startup were checked. Automatic choices, multiplayer and retry restoration still need verification.
