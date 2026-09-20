# Mapgame streaming

Mapgame keeps the player, cameras, UI, world interaction state and MapGame Systems.
Environment roots are in 46 additive scenes under `Assets/Scenes/MapChunks`.
The original saved map and Build Settings are backed up under `Backup-20260920-145533`.
Do not run the one-time converter again on an already converted map.

## Runtime

- MapGame Systems / MapChunkStreamer: load distance 45 m, unload distance 75 m,
  safety distance 15 m. Distances are measured from the chunk's environment bounds,
  not from its center; large meshes spanning a cell boundary stay available.
- Cell size used during conversion: 40 m. Each prefab hierarchy stays together.
- Initial loading waits for nearby chunks after applying the saved player position.
  A Canvas loading overlay blocks movement until colliders are ready. Nearby missing
  chunks also block movement, including after a teleport. Missing build entries show
  an error instead of releasing the player into empty space.
- Stream one scene at a time, nearest first. Keep the base map as the active scene.
- Combat transitions wait for the pending chunk operation before replacing scenes.
- Chunk unloading removes GameObjects and colliders. Shared mesh/texture assets can
  remain resident; unused assets are reclaimed after at least four unloads while a
  menu is open, and on the normal Single scene transition. No claim of immediate
  complete asset-memory release is made for shared assets.
- Only nearby local lights receive shadows: 6 shadow faces total (one point light
  uses 6, a spot uses 1), within 25 m. Directional sunlight is unaffected. Local light
  shadow maps are capped at 256 during conversion. Adjust budget/distance in Inspector.
- Existing Ground/Surface/Obstacle/Climb/ladder layers and query masks are retained.
  Distant physics geometry is removed with its chunk. Do not swap collision layers
  by distance: the movement, climbing and ground queries rely on these masks.
- Existing 6,508 BoxColliders are retained. Only geometrically exact closed box meshes
  can be automatically replaced by BoxColliders. None of the 140 remaining MeshColliders
  passed this conservative replacement rule; terrain/stair geometry is preserved.

## Editing in Unity

1. Open mapgame. Empty environment groups in its Hierarchy are expected.
2. Tools > Adventure > Streaming > Open all chunks for editing (or open only the desired
   chunk scene additively from Project). This temporarily loads the full environment.
3. Edit objects in their chunk scene. Keep player, UI, cameras and MapGame Systems in
   mapgame. New save points/chests/enemies may stay in mapgame; the cost is small.
4. After moving/adding geometry, use Update chunk bounds after editing. Save all scenes.
5. Use Close chunks before Play so runtime can load only nearby scenes.
6. Validate chunks checks build entries, bounds math and missing scripts.

WorldInteraction.savedWorldId captures each old interaction ID before conversion,
so existing collection/defeat state continues to match. Newly authored points receive
a scene-independent ID. Use the point Inspector's duplicate button for a unique ID;
do not copy savedWorldId between different interactions.

## Verification

Unity scene validation: 46 unique enabled chunk scenes; 698 moved roots; no missing
scripts or duplicated map/player systems. The default spawn requires 20 of 46 chunks.
These are structural measurements, not measured frame-rate or memory improvements.
Library/CombatValidation contains conversion, scene validation and Play smoke reports.
Profile a standalone development build for startup time, main-thread spikes, physics,
rendering and peak memory before tuning distances or reducing mesh collision detail.

## Restore original environment layout

Close Unity before manual rollback. Restore the saved mapgame.unity and
EditorBuildSettings.asset from the backup into their original paths. Keep the original
mapgame .meta (its GUID must not change). Chunk files can remain unused. Runtime script
changes also support a map without MapChunkStreamer.
