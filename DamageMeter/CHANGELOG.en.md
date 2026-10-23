# DamageMeter changelog (English)

## 0.4.1
- Fix: "max hit" stopped growing. The game caps recorded damage at the target's remaining HP, so one-shots only counted the monster's HP. Max hit now uses the damage dealt including overkill (requires DBModKit 0.25.1); totals, DPS and shares still use actual damage

## 0.4.0
- The "passives & equipment" bucket is split by actual source: equipment, passive, relic and buff names are shown separately
