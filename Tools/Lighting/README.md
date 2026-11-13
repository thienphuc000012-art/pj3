# URP local-light shadow repair

The chunk builder previously selected URP's **Custom** resolution while setting
`Light.shadowCustomResolution = 256`. In this installed URP version, Custom reads
the integer stored in `Light.shadowResolution`, which was still `-1`. Casting it
to an unsigned atlas request produced **65535**, triggering 64–256x reductions.

The affected Point/Spot lights now use **Low** (256 in `PC_RPAsset`), and the chunk
builder uses the same setting. The atlas remains 2048. Valid custom resolutions,
light intensity/range, and Directional shadows are preserved. The existing
`MapChunkStreamer` still manages nearby shadow budgets.

- Audit: `python Tools/Lighting/fix_shadow_resolutions.py`
- Repair with timestamped backup outside Assets:
  `python Tools/Lighting/fix_shadow_resolutions.py --apply`
- Unity Edit Mode check:
  **Tools > Adventure > Repair > Validate URP Local Light Shadows**
- **Full prefab-aware check:**
  **Tools > Adventure > Repair > Audit Resolved Chunk Shadows**. This loads real
  chunk assets in isolated preview scenes and resolves inherited Light components,
  stripped GameObjects, and prefab property overrides. Results are in
  `Library/CombatValidation/UrpChunkShadows.json`.
- To repair only invalid IDs identified by Unity, with a backup:
  `python Tools/Lighting/fix_shadow_resolutions.py --resolved-report Library/CombatValidation/UrpChunkShadows.json --apply`

The Editor check repairs stale invalid values in loaded scenes and renders an
isolated Point/Spot shadow fixture. It does not start gameplay or save open scene
edits. Results are in `Library/CombatValidation/UrpShadowResolution.result.txt`.

The direct YAML audit cannot resolve inherited prefab lights. The resolved audit
found another 596 invalid values after the first 120 direct lights were fixed.
Both sets are now Low, including two inherited property overrides. The builder
also records prefab modifications explicitly, so saving a chunk retains the tier.
Use the resolved audit to validate future chunk rebuilds, not the direct audit alone.

## Combat / additive arena lighting

Opening several correct chunks together can still overfill an atlas. The
reproduction of the boss arena contained 345 local lights and requested up to 69
visible shadow faces. Unlike exploration, the combat scene had no shadow budget.

`CombatShadowBudget` now starts on Loading before chunk activation, registers each
chunk in `sceneLoaded`, and moves to the combat scene with `CombatManager`. The
arena builder, initial party, summons, and tracked VFX also register their lights.
It reuses cached light references before rendering, with no global search or extra
camera cull. It preserves light intensity/color/range and the Directional Light.

Defaults on the **Combat Shadow Budget** object in the runtime Hierarchy:

- **Maximum Shadow Faces = 12** (Point costs 6, Spot costs 1).
- **Shadow Distance = 30** metres around the arena/player starting position.
- **Atlas Usage = 0.75**, also checks each light's requested resolution so a large
  Point shadow cannot silently force URP to rescale the entire atlas.
- **Allow Vfx Shadows = off**. VFX still illuminate the scene.

You can add the component to a scene root in Edit Mode to keep custom settings.
The component restores original shadow flags on disable/destruction; it never
writes runtime choices back to source chunk/prefab assets. The atlas remains 2048.

**Tools > Adventure > Repair > Audit Combat Shadows** reconstructs the reported
17-chunk boss arena in preview scenes, verifies Loading-to-Combat budget transfer,
late VFX illumination and atlas area limits, renders four combat views, and checks
restoration. It does not run the campaign. See
`Library/CombatValidation/CombatShadows.json`.

If selecting Custom manually in URP's Light Inspector, enter a valid power-of-two
resolution of at least 128. Do not use the legacy `shadowCustomResolution` API to
configure URP Custom shadows.
