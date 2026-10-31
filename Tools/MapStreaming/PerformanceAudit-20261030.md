# Mapgame loading audit — 2026-10-30

## Findings

- The old 46-chunk manifest selected 20 chunks at the authored spawn
  `(-20.875, 280.876, -97.125)`, containing 25,164 of 26,089 environment renderers
  and 6,421 of 6,619 colliders. Large grouping hierarchies caused most environment
  objects to load even with a small streaming radius.
- Some light influence bounds also covered a large area. Their real ranges are retained;
  artificially shrinking the bounds could unload lighting or collision geometry too early.
- Every chunk previously triggered a global light scan and global interaction refresh.
  The loading/map canvases also repeated UI theme initialization and dynamic font creation.
- Editor scene-load timings vary substantially between cold and warm caches. They include
  Editor/import/integration overhead and are not standalone frame-time measurements.

## Applied changes

- Refined pure grouping nodes into 89 spatial scene cells, keeping prefab/functional
  subtrees and connected references together. Original ancestor transforms are recreated
  to preserve non-uniform scale and shear. Collider shapes/layers and mesh data are unchanged.
- Initial selection now loads 19,218 renderers and 4,866 colliders: 23.63% fewer renderers
  and 24.22% fewer colliders. Counts include inactive objects and LOD renderers.
- Cached loaded-scene paths; removed per-frame LINQ chunk searches/scene lookups.
- Cached per-scene lights and reused shadow selection buffers. Register only the newly
  loaded scene's lights and interaction points instead of scanning every resident scene.
- Spread scene postprocessing over frames and use Low background integration priority
  during exploration; restore the previous priority when each operation ends.
- Keep the Loading screen through final map initialization and block gameplay until it
  closes. Keep movement blocked near a pending chunk until collider/world setup completes.
- Apply Adventure UI theme once, hide menu pages before runtime initialization, and share
  the dynamic language font between overlapping loading/map canvases.
- Skip instantiating already collected chest/pickup models when returning to a map.

## Validation

- Runtime and Editor C# assembly compilation passed.
- Refiner compared world matrices, active/enabled states, layers, renderer mesh/materials,
  collider definitions, light data and serialized object references before commit and
  after saving/reopening all output scenes.
- The original-manifest Play Mode startup smoke test passed: 20/46 chunks, movement gate
  released, ground collider `SM_Interior_Floor_Box71_464` found below the player.
- The refined-manifest Play Mode startup smoke test passed: 20/89 chunks, all 20 expected
  initial cells resident, final MapGameSetup ready, movement gate released, the same ground
  collider found, 19,218 renderers and 4,866 colliders loaded. The run's Editor elapsed
  startup counter was 41.71 s; there is no matching baseline elapsed counter, so this is
  not evidence of a before/after loading-time change. `Smoke-20261030.txt` retains the result.
- SHA-256 comparison confirmed all 46 original scene files match the pre-refinement backup.
  No FPS or complete asset-memory reduction is asserted by this audit.

## Recovery/editing

- Active environment scenes: `Assets/Scenes/MapChunksRefined`.
- Original environment scenes remain at `Assets/Scenes/MapChunks`.
- Pre-refinement backup: `Tools/MapStreaming/BeforeRefine-20261030-211901-497`.
- Continue using **Tools → Adventure → Streaming → Open all chunks for editing** and
  **Close chunks before Play**. The latter now recognizes refined scenes as well.
- Do not rerun the original one-time converter or remove old source scenes until the
  new map has been reviewed in your normal playthrough.
