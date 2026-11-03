# Combat skill list

The combat skill menu keeps four normal skill slots and one ultimate. A row
contains the icon, skill name, resource number and a separate resource label.
Hovering or navigating to a row displays its focus banner and updates one shared
description panel beneath the list. The banner and resource number gently grow and shrink;
the icon, name, resource word and pointer hit rectangle remain stationary.

In `Assets/Scenes/combattest.unity`, edit:

- `Canvas/SubMenuPanel/Skill Presentation/Rows` for the list area.
- The inactive row template under Rows for icon/name/number/resource word layout.
  Enable it temporarily in Edit Mode to preview changes, then turn it off again.
- `Skill Presentation/Description Panel` for the shared description background/text.
- `CombatSkillRowUI` on the template for `Background Scale Amount`, `Number Scale Amount`
  and `Pulse Cycles Per Second`. These are fractions: `0.035` means a 3.5% background
  size pulse and `0.10` means a 10% number pulse. Positions remain fixed.

Existing authored RectTransforms are reused when the menu opens. Items retain
their existing quantity and per-row descriptions. Skills remain fixed in place
instead of being draggable through the inventory ScrollRect.
The skill presentation and description start hidden and only open through the
skill menu. Returning to Items or the main action menu, closing the submenu or
hiding battle UI also closes the description.

Use **Tools → Adventure → UI → Setup Combat Skill List** to add missing hierarchy.
It backs up and saves only the combat scene. It refuses to overwrite an already
open, unsaved combat scene and preserves the shared item/button prefab.
Use **Tools → Adventure → UI → Update Skill Pulse and Visibility** to update an
existing skill Canvas from the old vertical bob to a scale pulse and save the
presentation hidden by default. The update keeps authored RectTransforms and
makes a scene backup.

`CombatSkillUISetup.SetupBatch` also checks the isolated UI without entering
combat or touching player saves. Results are written to
`Library/CombatValidation/CombatSkillUISetup.result.txt`, with a preview image at
`Tools/CanvasUI/SkillListPreview.png`.
