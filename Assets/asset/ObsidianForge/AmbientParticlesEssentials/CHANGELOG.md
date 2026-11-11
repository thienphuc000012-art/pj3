1.0 — Initial Release

- Three atmosphere styles — Pollen & Spores, Floating Dust and Ambient Ash — with distinct palettes, natural motion and organic mote wakes.
- One main component, AmbientBiomeParticles, plus a ready-to-use prefab and three customizable profile assets.
- Camera-following emission volume with forward-biased coverage, world-space particles and configurable spawn radius.
- Four independently switchable layers per style, enabled by default, with individual tint, brightness, lifetime and motion controls.
- Soft circular trail motes deposited along world-space paths, with smooth drift, spread and fading; detached residue dissipates independently.
- Directional wind with degree-based controls, configurable strength, optional wind wisps and directional WindZone binding.
- Runtime tuning and scripting API for profiles, layers, density, particle budget, size, speed, turbulence, opacity, brightness and trails.
- Reused Particle Systems, a shared native-particle budget and bounded mote-trail geometry.
- Transform scale compensation for volume, particle size and movement; uniform scale recommended.
- Three custom HLSL shaders for particles, wind trails and the demo grid.
- Interactive demo with live controls, free movement, an open metric-grid floor and hideable UI.
- Separate Runtime, Editor and Demo assemblies; no Input System dependency in the runtime.
- English and Spanish documentation.
- Unity 6 support for URP, HDRP and Built-in, with included pipeline conversion packages.
