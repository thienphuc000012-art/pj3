# Integration

Versión en español: [Integration_ES.md](Integration_ES.md)

## Requirements

Unity 6 and a configured Built-in, URP or HDRP project. The runtime has no Input System, Recorder,
test-framework, Shader Graph or third-party package dependency. Bloom is optional, not required.

## Installing

The main asset lives in Assets/ObsidianForge/AmbientParticlesEssentials/.
The initial edition is URP. Before opening the demo, install the conversion next to that folder:

| Project pipeline | Import from Assets/ObsidianForge/ |
|---|---|
| URP | No extra package. |
| Built-in | AmbientParticlesEssentials_BuiltIn.unitypackage |
| HDRP | AmbientParticlesEssentials_HDRP.unitypackage |

Import every listed replacement. These packages preserve GUIDs and replace shaders and the demo
in place; they do not configure your project's pipeline. HDRP also installs the demo Volume profile.
Back up customized shaders/demo scenes before converting. Do not import both conversions together.
To return to URP, reimport the original distribution and remove the demo HDRP Volume asset if it remains.

The HDRP demo uses fixed exposure 0 and no tonemapping. Your own scenes do not need its Volume;
the effects use authored unlit brightness independent of HDRP exposure. Your post-processing can
still change the final appearance.

## Demo scene

Open Demo/AmbientParticlesDemo.unity and press Play. Pollen & Spores appears first.
WASD moves, Shift sprints, Q/E turns, F1 toggles the panel. The mouse only edits controls.
The panel scrolls on smaller screens. Reset restores profile-controlled settings and enables all
four layers; global brightness and wind controls are retained.

The camera can move freely across the open grid floor. This is the customer's interactive demo,
not the internal promotional scene: recording and marketing buttons are not included.

## In your game

1. Add Ambient Biome Particles from Add Component > Obsidian Forge > Ambient Particles Essentials,
   or drag Prefabs/AmbientBiomeParticles.prefab into the scene.
2. Assign Profile and Follow Target. In the Editor, adding the component selects Pollen_Default.
3. Use the viewing camera as Follow Target for view-weighted coverage. If unassigned, the component
   can use the active camera tagged MainCamera. With fallback disabled, it uses its host transform.
4. Press Play. No explicit start call is required.

The component may live on a separate object, player or camera. It never moves its host.
Particles stay in world space; only the generated emission volume follows the observer.
Use one observer-following effect for a normal single-camera scene. Multiple instances each have
their own budget and increase total cost; one instance does not provide separate coverage for
multiple distant cameras.

## Code setup

Namespace: ObsidianForge.AmbientParticlesEssentials. Assign a serialized profile when creating the
component in a build; automatic default assignment only runs when adding it in the Editor.

```csharp
using UnityEngine;
using ObsidianForge.AmbientParticlesEssentials;

public sealed class AmbientSetup : MonoBehaviour
{
    [SerializeField] private Camera viewingCamera;
    [SerializeField] private AmbientParticleProfile pollen;

    private void Start()
    {
        var effect = gameObject.AddComponent<AmbientBiomeParticles>();
        effect.FollowTarget = viewingCamera.transform;
        effect.SetProfile(pollen); // Set before overrides: profile switching resets them.
        effect.SetParticleCount(800);
        effect.SetSpawnRadius(20f);
        effect.SetDensity(0.8f);
        effect.SetWind(90f, 0.35f);
        effect.SetWindEnabled(true);
    }
}
```

## Runtime API

Changes apply in LateUpdate during Play. Some appearance/budget changes refill the volume and clear
old wakes; they are configuration changes, not a promise of seamless interpolation.
See [Scripts](Scripts.md) for limits and reset behavior.

| Member | Purpose |
|---|---|
| SetProfile(profile) / ResetToProfile() | Select a profile or restore its defaults. Null profile stops all layers. |
| SetLayerEnabled(index, enabled) | Toggle layer 0–3; clears that layer on disable. |
| SetDensity(value) / SetParticleCount(count) | Fraction and total native-particle ceiling. |
| SetSpawnRadius(radius) | Observer-following coverage. |
| SetTrailAmount(value) / SetTrailFadeDuration(seconds) | Trail budget weight and retirement fade duration. |
| SetParticleSize(value) / SetMovementSpeed(value) | Multipliers for size and motion. |
| SetTurbulence(value) / SetOpacity(value) | Noise and transparency multipliers. |
| SetGlobalBrightness(value) | Component-wide brightness. |
| SetWind(degrees, strength) / SetWindEnabled(enabled) | Set horizontal wind and explicitly enable it. |
| SetWind(Vector3 direction, strength) | Horizontal yaw; nonzero X/Z enables wind, zero X/Z disables it. |
| SetWindVisuals(enabled) | Enable only the wind wisps; does not stop wind. |
| RefreshSettings() | Reapply settings after scripted profile/layer edits. |
| FollowTarget / ParticleMaterial / WindSource / WindTrailMaterial | Assign observer, optional material override, directional WindZone or wind trail material. |
| Profile / LayerCount / GetLayerName(index) / IsLayerEnabled(index) | Inspect style and layer state. |
| GetLayerSystem(index) / ParticleSystem | Inspect layer 0–3 or the fine-layer ParticleSystem. |
| WindParticleSystem / TrailResidueSystem | Inspect wind or detached-residue systems after initialization. |
| Density, ParticleCount, SpawnRadius, TrailAmount, TrailFadeDuration | Read current component values. |
| GlobalBrightness, ParticleSizeMultiplier, MovementSpeedMultiplier, TurbulenceMultiplier, OpacityMultiplier | Read current multipliers. |
| WindEnabled, WindVisuals, WindAngle, WindStrength, WindDirection | Read directional-wind settings; WindDirection is a normalized horizontal vector. |
| profile.SetBrightness(value) / profile.GetLayer(index) | Edit style brightness or access a layer; notify consumers with RefreshSettings after scripted layer edits. |
| profile.ConfigureFloatingDustDefaults(), ConfigurePollenAndSporesDefaults(), ConfigureAmbientAshDefaults() | Replace a profile's settings/layers with the built-in baseline; notify consumers and reselect/reset if needed. |

Profile exposes read-only values for its identity, volume, appearance and motion, plus Revision for
tracking Inspector changes. Layer fields are editable. Generated systems are owned by the controller;
direct module changes may be overwritten when it reapplies settings.

## Troubleshooting and performance

| Symptom | Check |
|---|---|
| Pink particles or floor | Wrong conversion or missing configured pipeline. Reimport the correct conversion; do not auto-convert the custom unlit materials to Lit. |
| No particles | Assign a profile/observer, check opacity and brightness, active layers and GameObject state. Click the Game view for demo keyboard focus. |
| Style changes reset my values | Select the profile before applying overrides. Duplicate profiles instead of editing shared defaults. |
| Wakes disappear | Check Trail Layer, Trail Amount, fourth-layer Trails, opacity and budget share. Shed Residue is a separate toggle. |
| No wind wisps | Enable Wind Enabled and Wind Visuals, use nonzero strength and Wisp Count. An assigned WindZone controls wind. |
| Unexpected wind | Use a directional WindZone, or unassign it to control angles manually. Spherical zones/gusts are not sampled. |
| Oversized effect | Prefer uniform scale. The largest absolute scale axis controls coverage, size and movement; avoid rotated nonuniform parents. |
| Low FPS | Lower density/count, disable layers, reduce wake lifetime/width/detail and overlapping transparent coverage. Measure in a player build on target hardware. |
| Different brightness in my scene | Check Global/Profile/Layer brightness and your bloom/tonemapping; particles themselves do not require scene lighting. |

Native particles share a maximum of 1200 per component, including wind and detached residue.
Style wakes have a separate 2200-mote geometry cap. Defaults 800 × 0.8 allow up to 640 native
particles before budget rounding/disabled shares; this is a ceiling, not an exact live count.
Increase radius for coverage, not density: the same budget is spread over a larger volume.
