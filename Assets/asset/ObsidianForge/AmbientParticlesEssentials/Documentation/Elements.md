# Elements

Versión en español: [Elements_ES.md](Elements_ES.md)

How the visible pieces work together and how to customize them. Parameter reference:
[Scripts](Scripts.md). Setup and pipeline installation: [Integration](Integration.md).

## Styles and layers

Every shipped profile uses four enabled layers. These are one atmosphere, not four separate
effects to install. The demo labels come from each layer's Name.

| Layer | Pollen & Spores | Floating Dust | Ambient Ash |
|---|---|---|---|
| Fine (0) | Golden pollen | Fine warm dust | Cool fine ash |
| Detail (1) | Round green spores | Soft round motes | Warm orange embers |
| Accent (2) | Soft rose halos | Defocused golden highlights | Diffuse ember halos |
| Trail (3) | Luminous organic meanders | Golden drifting streamlets | Rising spark wakes |

Round silhouettes have soft edges and bright cores. Accent halos intentionally look larger and
more diffuse. These are subtle ambient effects, not smoke volumes, flames or dense weather.

Pollen is the default. Selecting a different profile changes its palette, materials and motion,
and resets the profile-driven component defaults. See [reset behavior](Scripts.md#reset-profiles-and-play-mode-editing).

## Camera-following coverage

The component follows the observer without moving its host GameObject. New particles populate
a forward-biased volume around the target. Existing particles remain in world space, so moving
the camera does not drag a whole cloud with it. Particles outside retained coverage are recycled;
native particles fade in over the beginning of their lifetime.

Use the viewing camera as Follow Target for camera-aligned forward coverage. A player transform
works too, but its forward may not match an independently rotating camera. This is one observer's
local atmosphere, not persistent simulation over the whole level or strict frustum-only spawning.
No movement prediction is used.

Spawn Radius increases the region, not its particle count. With the same budget, a larger volume
looks sparser. Minimum Camera Distance and shader proximity fading avoid large particles passing
through the viewer. These controls are not physics collision; there is no collider-based response.

## Three different trailing elements

| Element | Purpose | Controls |
|---|---|---|
| Heads | Fourth-layer moving particles that seed wakes. | Trail Layer, Trail Amount, layer size/motion. |
| Mote wakes | Small bright, soft round motes left along the head's world-space path. | Trails, Trail Ratio, Lifetime, Width, Vertex Distance, Opacity, Motes Per Sample. |
| Detached residue | Larger independent particles shed by heads; drift and fade after birth. | Shed Residue and the Residue fields. |

Style wakes are not continuous ribbons, tubes or a texture attached to the head. Each mote has
its own world-space position and aging; the shader animates its drift and fade between bounded
mesh updates. Its small circular billboard faces the view, while the cloud of mote positions
occupies 3D space. Individual motes are sprites, not volumetric geometry.

Pollen wakes meander softly, dust disperses in warm drifting streams, and ash rises with a
spark-like character. Layer/profile settings change the result without adding another component.
Trail Fade Duration controls retirement near/outside coverage; Trail Lifetime controls normal
wake persistence. Residue has its own lifetime and end tint.

Trail Amount reallocates the same native budget toward the fourth layer; it is not an extra
particle allowance. A larger fourth-layer allocation produces more heads/residue, while Trail
Ratio controls which heads leave motes. At high density, detail per sample is automatically
limited to the shared 2200-mote ceiling to retain continuous tracks. Long, wide, overlapping wakes
still increase transparent rendering cost.

## Wind and wind wisps

Directional wind is additive to natural drift, vertical motion and turbulence. Enable Wind,
set an angle in degrees and adjust Wind Strength; 0° points along world +Z and 90° along +X.
A directional WindZone can drive heading and strength. There is no fluid simulation or obstacle
avoidance; spherical WindZones and gust/pulse settings are not interpreted.

Wind Visuals adds small wind wisps only with active wind and positive strength. These are the
only native ribbon trails; their material and settings are independent of style wakes.
Turning Wind Visuals off leaves wind movement active. Turning Wind off preserves natural motion.
Global Brightness and the wind layer control wisp brightness; profile Brightness does not.

## Brightness, opacity and materials

Style brightness = Global Brightness × profile Brightness × layer Brightness.
Wind brightness = Global Brightness × wind-layer Brightness.
Colour/alpha combine profile colour, layer tint, lifetime fading and component Opacity.
Wake motes have an additional bright-core emphasis.

Brightness adjusts colour intensity; Opacity adjusts transparency. Neither creates scene lighting.
Bloom is optional and depends on your pipeline/post-processing setup; there are no required lights.

| Shader / property | Meaning |
|---|---|
| AmbientParticle.shader: Form | Procedural silhouette: Mote, Grain, Spore, Halo, Ember or Haze. |
| AmbientParticle.shader: Brightness | Material brightness when rendered independently; the controller overrides renderer brightness at runtime. |
| AmbientTrail.shader: Brightness | Wind-ribbon brightness; likewise driven by the controller. |
| MetricGrid.shader: Base Color | Demo grid-line colour. |
| MetricGrid.shader: Floor Color | Demo ground colour. |
| MetricGrid.shader: Horizon Color | Demo distance/horizon blend colour. |

Camera Fade, Wake Mode and Wake Time are hidden runtime controls, not profile authoring knobs.
Hidden legacy Style/Trail Scale properties on the wind shader are not supported appearance controls.
Particle Form belongs to the layer material, not the component Inspector.

Leave Particle Material empty to preserve each layer's silhouette. Its optional override applies
to multiple particle renderers, including wind. A custom material lacking the wake properties
cannot replace the wake shader; the controller retains a supported wake material instead.
Duplicate shipped materials before changing their Form or colour. Merely editing material
Brightness may appear ineffective because the controller supplies its own multiplier.

Shaders share AmbientVisualCore.hlsl. URP is the base edition; the sibling conversion packages
replace pipeline wrappers and their demo assets while preserving GUIDs. Do not mix wrapper
files from different editions. See [Installing](Integration.md#installing).

Textures/DustSoftParticle.asset and two legacy materials remain for compatibility; current
default effects use procedural silhouettes, not that texture. They are not additional styles.

## Make a custom atmosphere

1. Duplicate a supplied profile and any materials you intend to change.
2. Keep four layers in the same order so the Inspector/API toggles stay predictable.
3. Set palette, natural drift, size and lifetime in the profile; refine each layer's tint,
   brightness, size, noise and motion.
4. Tune heads, wake persistence and residue separately. Reduce overlapping halos before
   increasing the total budget.
5. Assign the new profile, then apply component overrides such as observer, wind and brightness.
6. Test moving forward, sideways and turning, at both low and high density. Profile the target
   device at its actual output resolution.

Shared profile edits affect every consumer. To notify consumers after scripted layer edits, call
RefreshSettings(). To apply changed profile defaults to component density/budget/radius, reselect
the profile or use ResetToProfile(). These operations can refill particles; they are not smooth
transitions between presets.

Uniform host scale is recommended: coverage, sizes and motion scale together, while lifetimes
stay in seconds. A nonuniform transform uses its largest absolute axis, not independent XYZ
stretching of round particles.

## Demo elements and scope

The demo uses a procedural metric-grid floor that recenters under the observer, giving an open
ground impression; it is not an infinite physics surface. Grid spacing is shader-defined rather
than an exposed per-component parameter. Camera movement and UI are example code, independent
of the game's required component.

The promotional scene, cinematic, video recorder, screenshot tools, previous trail experiments
and their instructions are outside the customer package. They are not runtime dependencies.

