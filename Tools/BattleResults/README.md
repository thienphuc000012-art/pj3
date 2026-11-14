# Battle result UI

Prefab: `Assets/Resources/Adventure/UI/BattleResultCanvas.prefab`.

- Victory uses the same `Pattern` / diamond hierarchy as `AdventureCanvas/Menu`.
- `BattleResultPresentation` on this Canvas controls `Statistics Seconds`, `Experience Seconds`, `Member Stagger Seconds`, `Full Bar Hold Seconds`, and `Level Up Pulse Seconds`.
- Statistics and loot quantities count up. Each character's XP bar starts at its actual pre-reward XP, holds briefly when full, advances the level, then continues with the remaining XP. A gold level-up label and portrait pulse appear on a level change.
- The animation reads the before/after snapshots captured by `CampaignSession.FinishBattle`; it never grants or saves XP. Existing gameplay awards and save timing remain authoritative.
- The first Continue/Enter during counting finishes the presentation. Continue/Enter afterward returns to the map using the existing battle-return flow.
- Defeat has a separate `Defeat` hierarchy with a blurred, darkened combat snapshot, an ink edge, fine red geometry, and `Back`. Back/Enter uses the existing defeat/checkpoint policy. It does not start a new game.
- VI/EN labels refresh through the existing localization system.

Setup: **Tools > Adventure > UI > Setup Battle Result Presentation**.

Validation: **Tools > Adventure > UI > Validate Battle Result Presentation**. Native Edit Mode checks cover zero/small XP, single/multiple levels, full-bar hold, the level-100 cap, count-up while timeScale is zero, exact final totals, skip, locale changes and the return button. Timestamped win/lose PNGs are generated here. Campaign saves and live scenes are not written by validation.

`ReferenceVideo.png` and `InspectVideo.ps1` are inspection fixtures for the supplied recording. They are outside Assets and are not included in the game. Timestamped `Before-*` folders preserve the original prefab and localization table before setup.
