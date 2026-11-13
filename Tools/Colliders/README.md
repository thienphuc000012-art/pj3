# Positive-scale environment BoxColliders

The 2026-11-12 repair corrected 788 mirrored BoxCollider instances in 15 original/refined chunk scenes, including `Railings/SM_Railing_2643`.

The visual object retains its original transform, mesh, materials and prefab property overrides. Its collider uses an empty child named `Positive Scale Collision`, with the inherited reflection cancelled. The eight world-space box corners remain unchanged. Layer, tag, static flags, material, contact settings and enabled state are retained. No extra mesh colliders are required.

To adjust one of these collision shapes, edit the BoxCollider on `Positive Scale Collision`. Keep this child's resulting world scale positive; the parent's negative scale is still used to mirror the artwork.

`MapStreamingBuilder.TryBox` also creates valid positive-scale primitives when simplifying an exact cube mesh, so rebuilding the chunks does not recreate the warning.

## Editor tools

- **Tools > Adventure > Repair > Audit Negative Box Colliders** checks all project scenes under `Assets/Scenes` and enabled Build Settings scenes, including inactive and inherited prefab colliders.
- **Tools > Adventure > Repair > Fix Negative Box Colliders** uses the latest audit, backs up affected scenes to `Tools/Colliders/BeforePositiveBoxes-*`, fixes static environment boxes, and validates the saved scenes after reloading. Gameplay hitboxes require explicit review and are not silently moved by this tool.

Reports are written under `Library/CombatValidation/NegativeBoxes.json` and `Library/CombatValidation/RepairNegativeBoxes.json`. The repair also tests moving the refined chunks into a combat arena. All checks run in Edit Mode without starting a campaign or writing player save data.

The backup contains the exact scene files from immediately before this repair, including all earlier lighting and collider edits. Do not restore it unless reverting this collider repair intentionally.
