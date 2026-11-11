# Third-party notices

Versión en español: [ThirdParty_ES.md](ThirdParty_ES.md)

Ambient Particles Essentials contains publisher-authored C# code, custom HLSL shaders,
procedural particle silhouettes, generated demo geometry, profiles, materials and a demo scene.
It does not bundle third-party code, models, audio or externally sourced textures.

The retained legacy DustSoftParticle asset is generated texture data, not a third-party image.
Current presets do not use it.

## Unity dependencies

Unity 6 supplies the Particle System, mesh/rendering APIs and IMGUI used by the package.
Choose a configured URP, HDRP or Built-in project and the corresponding package edition.
URP/HDRP require Unity's matching render-pipeline packages in your project; these are not
third-party content embedded in this asset. See [Integration](Integration.md).

The runtime does not require Input System, Shader Graph, VFX Graph, Unity Recorder, a testing
framework or any third-party plugin. The demo navigation/UI are in a separate Demo assembly.

Recorder, tests and promotional tools used by the publisher remain outside the customer package.
They are not necessary for using this effect. For the terms governing Unity itself, its packages
and your Asset Store acquisition, refer to their respective licenses; this notice does not
replace them.

