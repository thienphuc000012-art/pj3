# Vegetation and combat VFX — 15 November 2026

## Applied settings

Scene `m`, `Terrain Main` → Terrain Settings:

| Setting | Original | Current |
| --- | ---: | ---: |
| Tree Distance | 1000 m | 200 m |
| Billboard Start | 300 m | 80 m |
| Detail Distance | 200 m | 80 m |

All 200,014 painted instances and 90 palette entries are preserved. TerrainData's on-disk SHA256 is unchanged (see `Vegetation.txt`). Collider, material, density and terrain height data are unchanged. At the authored Player position, radius candidates fall from 200,014 to 87,359; this is a distance calculation, **not** a rendered tree count or FPS measurement. Test from several camera positions to judge the shorter horizon.

Most painted instances are grass entered through Paint Trees. They obey **Tree Distance**, even when the prefab is named grass. **Detail Distance** controls Terrain Details only. For further vegetation optimization, use Terrain Details for new small grass patches, retain LODs/billboards for trees, and inspect dense areas before reducing or converting existing painted data.

## Combat VFX reuse

`CombatManager` → **Visual VFX Performance**:

- **Pool Visual Vfx**: enabled by default. Disable to compare with the previous lifecycle.
- **Cached Visual Vfx Per Prefab**: 4 inactive instances.
- **Cached Visual Vfx Total**: 24 inactive instances across all prefabs.

Cache limits are read when the pool is first created; change them before entering combat. These are inactive cache limits, not a cap on active skill effects.

The pool is owned and disposed by the combat scene's CombatManager. It resets particles, audio, child transforms and original simulation speed before reuse. Lifetime deadlines use combat's scaled time; turn cleanup returns effects early. Supported mesh-particle renderers enable GPU instancing on clones only.

Only visual particle hierarchies with supported renderer/light/audio components are eligible. Scripted RFX4 effects, physics, Animator, beam casts and particles with self-destroy/disable/callback stop actions retain their original lifecycle. The current ActionData audit finds **2 of 40** candidate prefabs eligible; see `VFX-Validation.txt`. This does not optimize every skill or reduce transparent overdraw. Damage/parry events, projectile travel and particle emission counts have not been changed.

## Validation and restoration

Unity menu **Tools → Adventure → Performance → Validate Visual VFX Reuse** runs isolated Edit Mode checks for reuse, reset, exact expiry, stale deadlines, cache limits, external object destruction, and actual CombatManager hit/turn cleanup. It never starts gameplay or writes campaign saves.

Parry (GolemLava Attack4), Free Slash projectile flight/damage ownership, and Free Slash ground effects were also checked in isolated native Unity previews after the runtime change. Reports are in `Tools/GolemLavaParry/Validation.txt` and `Library/CombatValidation/SwordSlash*Validation.result.txt`.

Before each terrain setup, `Before-*/m.disk.unity` backs up the saved scene; `m.current.unity` also preserves any in-memory edits when scene m is open. Prefer adjusting the three Terrain Settings values above to restore the original rendering distances; copying an entire backup could overwrite later scene edits. Original settings are 1000 / 300 / 200.

## Next performance measurement

Profile a Development Build on the target machine, with the same resolution, camera path and combat action sequence. Compare CPU/GPU frame time, rendering batches, transparent overdraw, shadow cost and GC Alloc before/after. Stress repeated attacks with several enemies. Pooling reduces creation/destruction costs for eligible effects; large transparent particles, lights and shadows need separate GPU measurements. No measured FPS improvement is claimed by these correctness checks.
