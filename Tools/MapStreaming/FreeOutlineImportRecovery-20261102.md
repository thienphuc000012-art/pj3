# Free Outline import crash and map floor recovery

The native crash on 2026-11-02 occurred during package import, inside
`libfbxsdk` / `UnityFBX::DoImportScene` / `FBXImporter::DoMeshImport`.
The current import at the crash was `SM_InteriorFloor_2.fbx`; the preceding
`SM_Castle_Gate_01.fbx` import also failed. This was not a URP render stack.

On restart Unity explicitly created a DefaultAsset for the floor FBX because
its last importer crashed. The source FBX and GUID were intact. All four LOD
mesh references were unavailable, affecting 21 floor instances across
`Cell_-1_-3` and `Cell_-1_-4`. Independent BoxColliders remained present.
The base scene, 89 chunk files and their build entries were intact.

## Changes

- Scoped Linework Lite's `SmoothNormalsMeshImporter` to models explicitly
  labelled `SmoothNormalsLineworkLite`. Unrelated models retain their authored
  UV8. Removed per-vertex logging and duplicate/null mesh processing.
- Reimported only the two failed castle FBX assets after compilation.
- Added an explicit repair command at **Tools → Adventure → Repair → Reimport
  damaged castle meshes**, with source/GUID/settings and prefab-reference checks.
- Added **Tools → Adventure → Streaming → Open nearby chunks for editing**,
  using the existing player position and streaming radius. Opened the 20 nearby
  chunks in Edit Mode and kept `mapgame` active. Scene data was not saved/rewritten.

The package importer touched every model before this patch, explaining why
installing it initiated widespread model processing. The native stack does not
prove that its UV8 mutation directly caused the FBX SDK crash; the patch removes
that unrelated-asset mutation rather than claiming to repair Unity's native SDK.
Changes to AssetPostprocessor assemblies can still invalidate import dependencies.

## Verification

- Unity compiled the runtime, Editor and Linework assemblies successfully.
- Floor FBX: 4 meshes; prefab: 4 valid mesh references.
- Gate FBX: 5 meshes; prefab: 4 valid mesh references.
- FBX bytes, .meta/import settings and GUIDs remained unchanged.
- Affected chunks: 21 floor instances, 84 valid LOD mesh references, all floor
  BoxColliders present and enabled.
- No new native crash during selective reimport or nearby-chunk preview.
- No live game saves were used; no scene or prefab files were rewritten.

Evidence: `Library/CombatValidation/MapMeshImportRecovery.result.txt` and local
Unity Editor logs. Original crash log:
`C:/Users/Phuc/AppData/Local/Temp/Unity/Editor/Crashes/Crash_2026-11-02_145933990/Editor.log`.
