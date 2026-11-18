# Camera, enemy spawn and cheat panel

- Enemy Encounter → World Interaction → **Enemy Spawn**:
  - `Respawn`: enables repeat encounters for regular enemies.
  - `Respawn Seconds`: 300 seconds of recorded play time by default. Loading/offline time is excluded. Each enemy can have its own delay.
  - `Time Of Day`: Always, Day Only (06:00–18:00), Night Only (18:00–06:00).
  - Bosses (`BattleUnit.isBoss`), the configured combat tutorial, and Main Quest portal targets are permanent and ignore these settings.
  - Respawning waits until the player is at least 6 metres from the authored spawn (or beyond interaction range + 2 metres). Patrol and animation restart at that original spawn.
  - Timers persist in campaign saves. Older saves seed a fresh cooldown for already-defeated regular enemies.
- Press **O** on a map or in campaign combat to open Cheats. O, Esc or × closes it.
  - Select a party member with ‹ / ›; enter absolute base stats and press Apply.
  - Set real minutes per game day and the current game hour. Lighting changes remain limited to scene `m`.
  - Add levels, unlock all party skills, or unlock all campaign map save points.
  - Cheats write current campaign progress, preserving manual save slots. Character/skill prefabs and global world-time settings are not edited.
  - Input, cursor, time scale and combat animation speeds are restored on close or scene change.
- Save-point cheat catalog: `Assets/Resources/Adventure/RestPointCatalog.asset`.
  - Refresh after adding/moving save points with **Tools → Adventure → Cheats → Rebuild Save Point Catalog**.
  - It also rebuilds before a player build. Existing visited points keep their recorded standing positions.
- Camera discards initial cursor-warp deltas after loading, focus, menus and cutscenes. Teleports snap follow and Cinemachine damping history together.
- An opaque persistent cover precedes environment staging; Loading remains until combat actors and camera have initialized.

Validation: **Tools → Adventure → Cheats → Validate World Systems**. Runs isolated Edit Mode fixtures; does not enter Play Mode or write a real campaign save. Results: `Tools/WorldSystems/Validation.txt`; panel preview: `CheatPanel.png`. Full live scene-transition playback is not part of this Edit Mode validation.
