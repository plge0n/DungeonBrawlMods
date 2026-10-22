# DBModKit changelog (English)

## 0.24.0
- Custom equipment: attach custom effects to existing equipment (`RegisterEquipEffect`), or add brand-new equipment (`RegisterEquipment`, `CustomEquipment.Give`) that can appear in the artifact pick
- Content packs support `equipEffects` and `equipment`
- Custom equipment counts towards the multiplayer handshake and is removed from build data sent to other players
- Tested in single-player; not yet tested: multiplayer, upgrading, retrying a wave, picking it from the artifact choice
