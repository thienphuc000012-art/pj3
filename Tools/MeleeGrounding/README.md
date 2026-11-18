# Melee landing height — OakTreeEnt / Sword Attack 3

Reference: `Screen Recording 2026-11-18 120037.mp4`. Sampled frames are in `ReferenceVideo.png`.

The shared melee approach calculation used the attacker's starting world Y even when moving onto lower/higher ground. On the real OakTreeEnt encounter at (61.070, -95.550, -20.174), radial approaches reproduced both hovering and buried destinations. Actual hover error reached approximately 0.55 m.

CombatManager now queries the ground at the approach position, excluding triggers, character controllers, unit and adventure gameplay colliders, and colliders belonging to other scenes. It retains the caster's original pivot clearance, authored X/Y/Z offsets and the enemy-specific approach settings. If the destination has no valid floor, the caster retains its starting position. Return movement still uses the exact original slot.

Ground layers: CombatManager > Melee Approach Position > Melee Ground Layers. Search range comes from the active Battle Encounter Setup > Arena Spawn > Floor Search Height/Depth, with default 2.5 m above and 3 m below the two units' height range. Player Melee Position Offset Y remains an intentional extra height.

Validation covers flat/slope terrain, large enemy body and effect-trigger exclusion, caster pivot clearance, player and per-enemy offsets, missing floors, eight real OakTreeEnt terrain directions and exact movement endpoints. The Sword Attack 3 action asset and animation trigger are preserved. Details are in `Validation.txt`.

Checks use isolated Edit Mode scenes. No Play Mode, real save writes, live player movement, or scene/prefab saves. Original manager and action data are backed up in `Before/`.
