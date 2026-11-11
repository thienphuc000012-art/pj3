# Ambient Particles Essentials

Versión en español: [README_ES.md](README_ES.md)

Add subtle ambient particles with one component: Pollen & Spores, Floating Dust and Ambient Ash.
The emission volume follows your camera or player while particles drift in world space.
Each style has four independently switchable layers, organic mote trails and optional wind wisps.

- Unity 6 · URP, Built-in and HDRP.
- One-script integration, or use the ready-to-use prefab.
- Defaults: Pollen & Spores, Density 0.8, Total Particle Budget 800, Spawn Radius 20, Trail Amount 3.
- Procedural silhouettes: no textures or bloom required for the current effects.

## Start in 30 seconds

1. For Built-in or HDRP, import the matching conversion in Assets/ObsidianForge/ first.
   URP needs no conversion. See [Integration](Documentation/Integration.md#installing).
2. Open Demo/AmbientParticlesDemo.unity and press Play.
3. WASD moves, Shift sprints, Q/E turns, F1 hides the UI. The mouse only edits parameters.
4. In your scene, add Ambient Biome Particles to an empty object, camera or player.
   Assign Follow Target to the viewing camera; leave it empty to use the tagged MainCamera.
   Adding the component in the Editor selects Pollen_Default automatically.

In a build, if adding the component from code, assign a profile with SetProfile().
No forest, fire, lights, colliders or level-wide particle emitters are required.

## The three styles

| Style | Appearance |
|---|---|
| Pollen & Spores | Golden pollen, green round spores, rose halos and soft luminous meanders. |
| Floating Dust | Fine warm dust, soft round motes, defocused highlights and golden streamlets. |
| Ambient Ash | Cool drifting ash, orange embers, soft ember halos and rising spark wakes. |

Change Profile to switch styles. Duplicate a profile before customizing it: its values are shared
by every component using that asset. The Inspector foldout edits the selected profile, not a private copy.

## Main controls

| Control | Use |
|---|---|
| Global Brightness | Brightness of the entire effect; does not change opacity. |
| Density / Particle Count | Fraction and ceiling of the shared particle budget, not counts per layer. |
| Spawn Radius | Extent of camera-following coverage; a larger radius spreads particles farther apart. |
| Fine / Detail / Accent / Trail Layer | Enable or disable each layer independently. |
| Trail Amount / Trail Fade Duration | Allocate more of the same budget to trails and control their retirement fade. |
| Wind Enabled / Wind Angle / Wind Strength | Add directional wind while retaining natural drift and turbulence. |
| Wind Visuals | Hide only the wind wisps, without stopping wind motion. |

Full explanations of every component, profile and layer field are in [Scripts](Documentation/Scripts.md).
API examples, pipeline installation and troubleshooting are in [Integration](Documentation/Integration.md).

## What's inside

AmbientParticlesEssentials/

- README.md / README_ES.md — overview and quick start.
- CHANGELOG.md / CHANGELOG_ES.md — commercial release history.
- Runtime/ — controller, profiles and independent Demo assembly.
- Editor/ — component Inspector and authoring tools.
- Profiles/ — three editable default styles.
- Prefabs/ — AmbientBiomeParticles prefab.
- Materials/ — layer, wind and demo materials.
- Shaders/ — particle and wind shaders, plus shared visual core.
- Demo/ — interactive scene and procedural metric-grid shader/mesh.
- Textures/ — legacy compatibility asset; current presets do not use it.
- Documentation/ — Integration, Scripts, Elements and ThirdParty, in both languages.

The Built-in and HDRP conversion packages sit next to this folder, in Assets/ObsidianForge/.
Marketing, recording tools, tests and development history are not part of the customer asset.

## Performance

Density × Particle Count bounds the native particle systems together; the maximum is 1200.
The style mote mesh has its own 2200-mote ceiling. Disabled layers save their shares rather than
giving their quota to other layers. Longer/wider wakes and overlapping transparent particles still
cost rendering time. Profile on your target hardware; these caps are not a universal FPS guarantee.

## Documentation

- [Integration](Documentation/Integration.md) — installing, demo, game setup, API and troubleshooting.
- [Scripts](Documentation/Scripts.md) — every Inspector parameter and reset behavior.
- [Elements](Documentation/Elements.md) — styles, layers, trails, materials and customization.
- [Third-party notices](Documentation/ThirdParty.md).
- [Release changelog](CHANGELOG.md).
