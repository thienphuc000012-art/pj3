# Scripts

Versión en español: [Scripts_ES.md](Scripts_ES.md)

Reference for every visible parameter. Names match Unity's fields; names in parentheses identify
code/serialization. API: [Integration](Integration.md#runtime-api).

## Files and responsibilities

| File | Responsibility |
|---|---|
| Runtime/Components/AmbientBiomeParticles.cs | Only component required in game; volume, layers, wind and wakes. |
| Runtime/Profiles/AmbientParticleProfile.cs | Shared style asset and defaults. |
| Runtime/Profiles/AmbientParticleLayer.cs | Serializable layer definition, not another component to add. |
| Editor/AmbientBiomeParticlesEditor.cs | Inspector and inline profile editing; excluded from builds. |
| Editor/Demo/AmbientParticlesDemoSceneCreator.cs | Regenerates/restores demo and presets; Editor tool. |
| Runtime/Demo/AmbientParticlesDemo.cs | Demo UI; not needed in your game. |
| Runtime/Demo/DemoOrbitCamera.cs | Demo keyboard navigation. |
| Runtime/Demo/DemoEndlessGround.cs | Demo floor following. |
| Runtime/AssemblyMarker.cs | Assembly identity; no controls or visual behavior. |

## AmbientBiomeParticles: component Inspector

| Field / code name | Range / default | Purpose and when to use it |
|---|---|---|
| Global Brightness (globalBrightness) | 0–8; default 1 | Multiplies style/layer brightness without changing alpha; wind uses global × wind-layer brightness. |
| Profile (profile) | Pollen_Default | Appearance and motion asset. Select before component overrides; null stops all layers. |
| Particle Material (particleMaterial) | Optional | Overrides particle materials. Leave empty for style-specific materials; custom shaders must support the effect's properties. |
| Follow Target (followTarget) | Optional | Camera/player transform to surround; assigning a camera makes forward coverage match the view. |
| Use Main Camera When Target Missing (useMainCameraWhenTargetMissing) | On | Fallback to active tagged MainCamera. Off with no target uses the host transform. |
| Recenter Radius Fraction (recenterRadiusFraction) | 0.1–1; default 0.25 | Movement distance as a fraction of radius before recycling distant particles; lower reacts sooner. |
| Forward Bias (forwardBias) | 0–0.8; default 0.4 | Shifts emission toward the observer's horizontal forward while keeping peripheral coverage; no movement prediction. |
| Retained Volume Padding (retainedVolumePadding) | 1–2; default 1.15 | Extra retention margin for existing particles; larger values reduce recycling but retain more distant particles. |
| Fine Layer (fineLayer) | On | Layer 0. Disable stops/clears it and saves its quota. |
| Detail Layer (detailLayer) | On | Layer 1. Independent detail toggle. |
| Accent Layer (accentLayer) | On | Layer 2. Independent halo/accent toggle. |
| Trail Layer (trailLayer) | On | Layer 3; toggles heads, mote wakes and detached residue together. |
| Density (density) | 0.01–1; default 0.8 | Fraction of Particle Count used. It is not particles per cubic unit. |
| Particle Count / Total Particle Budget (particleCount) | 16–1200; default 800 | Total ceiling shared by all native systems, including residue/wind; multiplied by Density. |
| Trail Amount (trailAmount) | 0–5; default 3 | Multiplies fourth-layer weight before budget normalization; more trails means fewer other particles. Zero removes heads/residue/wakes. |
| Trail Fade Duration (trailFadeDuration) | 0.2–2 s; default 0.85 | Time for heads/wakes to retire near the observer or outside coverage; independent of normal lifetime and residue fade. |
| Particle Size Multiplier (particleSizeMultiplier) | 0.1–4; default 1 | Multiplies profile/layer particle sizes. |
| Movement Speed Multiplier (movementSpeedMultiplier) | 0–4; default 1 | Scales base speed, natural drift, vertical speed, spin and residue drift. Directional wind has its own strength; zero does not disable noise/wind. |
| Turbulence Multiplier (turbulenceMultiplier) | 0–4; default 1 | Scales native noise strength; zero removes that turbulence. |
| Wind Enabled (windEnabled) | Off | Enables additive horizontal directional wind; natural profile motion still runs. |
| Wind Angle (windAngle) | 0–360°; default 65° | World yaw: 0 = +Z, 90 = +X, 180 = −Z, 270 = −X. Zero is a valid direction. |
| Wind Strength (windStrength) | 0–1.5; default 0.12 | Directional-wind speed/force control. Zero suppresses wind wisps; does not stop natural drift. |
| Wind Source (windSource) | Optional WindZone | Directional zone's forward yaw and windMain drive the above wind controls. Inactive zone disables wind; spherical zones/pulses ignored. |
| Wind Visuals (windVisuals) | On, armed | Allows wisps only while wind is enabled with positive strength. Disable hides visuals, not wind movement. |
| Wind Wisp Count (windWispCount) | 0–12; default 8 | Desired wind particle ceiling, further limited to 5% of the shared budget. |
| Wind Wisps (windWisps) | Layer settings | Independent wind appearance/motion block; its Budget Share is ignored because Wisp Count controls its quota. |
| Opacity (opacity) | 0–1; default 1 | Multiplies particle transparency. Zero hides particles without disabling simulation. |
| Spawn Radius (spawnRadius) | 2–32; default 20 | Coverage size at scale 1, in world units. Emission is a forward-biased box, not an exact sphere; larger spreads the same budget. |

Fine/Detail/Accent/Trail use indices 0/1/2/3. Systems are generated and reused; do not add scripts
to their children. Disabling a layer clears particles/wakes and saves its quota, not increases others.
Uniform host scale affects coverage, size and movement; durations do not scale.

## AmbientParticleProfile: style Inspector

Create profiles using Assets > Create > Obsidian Forge > Ambient Particles Essentials > Particle Profile,
or duplicate a preset in Profiles/. The component's Style brightness and independent layers foldout
edits this exact shared asset.

| Field / code name | Range / default | Purpose and when to use it |
|---|---|---|
| Brightness (brightness) | 0–8 | Style-only brightness; global/layer multipliers still apply. Does not affect wind wisps. |
| Display Name (displayName) | Text | Name in the demo selector. |
| Description (description) | Text | Description shown by the demo. |
| Density (density) | 0.05–1; default 0.8 | Default component density when selecting/resetting this profile. |
| Max Particles (maxParticles) | 16–1200; default 800 | Default total component budget. |
| Spawn Radius (spawnRadius) | Default 20 | Default component coverage. Component limits it to 2–32 when validated/set by its controls. |
| Vertical Range (verticalRange) | World units; Y > X | Minimum/maximum height relative to the observer, scaled with the host. |
| Minimum Camera Distance (minimumCameraDistance) | 0 to below profile radius | Near-observer exclusion distance. Non-trail particles are removed inside; trail heads retire gradually. Zero disables exclusion/fade. |
| Lifetime (lifetime) | Seconds; ≥0.5 | Baseline lifetime, multiplied by each layer with per-particle variation. |
| Particle Size (particleSize) | Min/max world size | Random particle size range. Uniform X/Y keeps silhouettes round. |
| Color (color) | RGBA | Base style colour/alpha, multiplied by layer tint and component opacity. |
| Movement Speed (movementSpeed) | Min/max speed | Baseline random starting speed; layer and component speed multipliers apply. |
| Natural Drift Direction (windDirection) | Vector3 | Direction of baseline drift, normalized; may include vertical motion. Retained with directional wind off/on. |
| Natural Drift Strength (windStrength) | ≥0 | Baseline drift magnitude, affected by Movement Speed Multiplier; separate from directional Wind Strength. |
| Turbulence (turbulence) | ≥0 | Baseline noise strength, combined with component/layer noise multipliers. |
| Gravity Modifier (gravityModifier) | Unity gravity multiplier | Gravity contribution to style motion; positive normally falls, negative reverses gravity. Zero leaves drift/noise. |
| Layers (layers) | Four shipped layers | Ordered independent layer definitions; see below. Keep the four-slot layout for predictable controls. |

## AmbientParticleLayer: per-layer controls

These fields appear in a profile's Layers or the component's Wind Wisps. Residue controls apply
only to the fourth style layer. Wind's quota is reserved inside the total separately: its Budget
Share and residue/mote controls are unused.

| Field / code name | Range / default | Purpose and when to use it |
|---|---|---|
| Name (name) | Text | Layer label in Inspector/demo. |
| Budget Share (budgetShare) | 0–1 | Relative weight normalized with other layers and Trail Amount; not an absolute count. Disabled shares are not redistributed. |
| Material (material) | Optional | Layer particle material; default style materials carry procedural silhouette settings. |
| Brightness (brightness) | 0–8 | Independent layer brightness, multiplied by profile/global brightness (wind does not use profile brightness). |
| Size Multiplier (sizeMultiplier) | ≥0.01 | Scale baseline particle size. |
| Lifetime Multiplier (lifetimeMultiplier) | 0.1–4 | Scale baseline lifetime; does not multiply the independent residue lifetime. |
| Speed Multiplier (speedMultiplier) | ≥0 | Scale random starting speed, not directional wind or the Vertical Speed range. |
| Noise Multiplier (noiseMultiplier) | ≥0 | Scale turbulence strength; zero removes this layer's native noise. |
| Noise Frequency (noiseFrequency) | ≥0.01 | Noise grain/frequency; higher produces smaller-scale changes. |
| Noise Scroll (noiseScroll) | ≥0 | Speed at which the noise pattern evolves; zero keeps the pattern stationary. |
| Vertical Speed (verticalSpeed) | Min/max world speed | Additional Y velocity; negative falls, positive rises. Host scale and component movement multiplier apply. |
| Angular Speed (angularSpeed) | Min/max radians/s | Billboard spin range, affected by component movement multiplier. |
| Brightness Variation (brightnessVariation) | 0–1 | Random brightness/colour variation between particles; higher widens the dark-to-bright range. |
| Tint (tint) | RGBA | Multiplies profile colour/alpha; alpha controls this layer independently. |
| Trails (trails) | Toggle | Enables deposited mote wakes on fourth style layer; native ribbons on wind only. Other style layers do not generate wakes. |
| Trail Material (trailMaterial) | Optional | Native wind ribbon material only. Style wakes use the fourth layer's round-particle material. |
| Trail Ratio (trailRatio) | 0–1 | Fraction of heads that leave mote/ribbon trails; does not disable detached residue. |
| Trail Lifetime (trailLifetime) | 0.1–5 s; wind ≤3 s | Duration of deposited style motes; maximum native wind trail duration. More increases persistence/cost. |
| Trail Width (trailWidth) | 0.01–1.5; wind ≤0.5 | Style-wake spatial spread or native wind ribbon width at scale 1. |
| Trail Vertex Distance (trailVertexDistance) | 0.04–0.3 | Spacing between spatial samples; smaller adds detail/cost. Slow heads still deposit periodically. |
| Trail Opacity (trailOpacity) | 0–1 | Opacity of mote wakes/native wind trails, separate from the head. |
| Trail Motes Per Sample (trailMotesPerSample) | 1–4; default 3 | Desired motes at each style sample. Detail is reduced automatically to fit the shared geometry cap; unused for native wind. |
| Shed Residue (shedResidue) | Fourth layer only | Enable larger independent particles shed behind heads, separate from Trails. |
| Residue Budget Share (residueBudgetShare) | 0.5–0.9; default 0.75 | Reserve part of fourth layer's quota for detached residue, leaving at least one head when quota permits. |
| Residue Lifetime (residueLifetime) | 0.2–3 s | Independent residue lifetime with particle variation. |
| Residue Rate (residueRate) | 1–12 | Desired emissions per head per second; automatically limited by available quota/lifetime. |
| Residue Size (residueSize) | 0.1–3.5 | Residue size relative to its source head, with random variation and expansion. |
| Residue Spread (residueSpread) | 0.01–1.2 | World-space emission scatter around the head, scaled by host. |
| Residue Drift (residueDrift) | 0–0.5 | Independent random drift; component movement multiplier applies. Wind remains additive. |
| Residue Opacity (residueOpacity) | 0–1 | Residue transparency relative to the source head. |
| Residue End Tint (residueEndTint) | RGBA colour | Cooling/dispersal colour over time; also influences aging style-wake colour. Fade controls final opacity. |

Aspect (aspect) is hidden legacy compatibility data; current runtime uses uniform X/Y size.
It cannot stretch the current round billboards.

## Reset, profiles and Play-mode editing

- ResetToProfile and Reset restore profile Density, Particle Count and Spawn Radius; size, movement,
  turbulence and opacity multipliers to 1; Trail Amount to 3; and enable all four layers.
- SetProfile does the same. Global Brightness, wind, follow/material settings and Trail Fade Duration
  are retained. Switching profiles does not undo edits to the profile asset itself.
- Inspector profile edits affect all consumers and can persist as asset edits when leaving Play.
  Do not treat a shared profile as a temporary copy.
- Scripted layer edits require RefreshSettings on each consumer. Profile SetBrightness updates
  its revision; scripted preset configuration needs notification/reselection.
- Budget, radius, size, movement, opacity and profile changes can refill the volume.
  Do not use them as continuous animation if you need to preserve the exact existing particles.

## Demo scripts and UI

AmbientParticlesDemo:

- Ambient Particles (ambientParticles): controller driven by the panel.
- Profiles (profiles): style list/order; the supplied list starts with pollen.
- Show Ui (showUi): panel visibility; F1 toggles it.
- UI controls: style, four layers, global brightness, density, budget, Trail Amount, size, movement,
  turbulence, opacity, radius, wind enabled, wind visuals, direction and strength.
  These read the component values, not a duplicate configuration.

DemoOrbitCamera:

- Walk Speed (walkSpeed): world units/second, default 5.
- Sprint Multiplier (sprintMultiplier): factor while holding Shift, default 2.
- Turn Speed (turnSpeed): degrees/second with Q/E, default 105.
- No mouse-look or cursor locking; focus the Game view for keyboard navigation.

DemoEndlessGround:

- Observer (observer): follow transform; empty falls back to MainCamera.
- Snap Distance (snapDistance): world floor repositioning step, default 10, effective minimum 1.
  Does not control the particle volume.

Create Ambient Particles Demo restores shipping profiles/materials and prefab/scene.
Back up customizations first. It is intended for the base URP edition; use the supplied converted
demos in Built-in/HDRP.
