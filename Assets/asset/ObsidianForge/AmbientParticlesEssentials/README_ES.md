# Ambient Particles Essentials

English version: [README.md](README.md)

Añade partículas ambientales sutiles con un solo componente: Pollen & Spores, Floating Dust y
Ambient Ash. El volumen de emisión sigue a tu cámara o jugador mientras las partículas se mueven
en espacio mundial. Cada estilo tiene cuatro capas desactivables, estelas de motes y viento opcional.

- Unity 6 · URP, Built-in y HDRP.
- Integración con un solo script o con el prefab incluido.
- Valores iniciales: Pollen & Spores, Density 0.8, Total Particle Budget 800, Spawn Radius 20 y Trail Amount 3.
- Siluetas procedurales: los efectos actuales no necesitan texturas ni bloom.

## Empieza en 30 segundos

1. En Built-in o HDRP, importa primero la conversión correspondiente en Assets/ObsidianForge/.
   URP no necesita conversión. Consulta [Integración](Documentation/Integration_ES.md#instalación).
2. Abre Demo/AmbientParticlesDemo.unity y pulsa Play.
3. WASD mueve, Shift acelera, Q/E gira y F1 oculta la UI. El mouse solo cambia parámetros.
4. En tu escena, añade Ambient Biome Particles a un objeto vacío, cámara o jugador.
   Asigna la cámara a Follow Target; déjalo vacío para usar la cámara etiquetada MainCamera.
   Añadir el componente desde el Editor selecciona automáticamente Pollen_Default.

Si añades el componente por código en un build, asigna un perfil con SetProfile().
No necesitas bosque, fuego, luces, colliders ni emisores distribuidos por todo el nivel.

## Los tres estilos

| Estilo | Apariencia |
|---|---|
| Pollen & Spores | Polen dorado, esporas redondas verdes, halos rosados y meandros luminosos suaves. |
| Floating Dust | Polvo cálido fino, motes redondos, luces desenfocadas y pequeñas estelas doradas. |
| Ambient Ash | Ceniza fría flotante, brasas naranjas, halos suaves y estelas de chispas ascendentes. |

Cambia Profile para alternar estilos. Duplica el perfil antes de personalizarlo: sus valores se
comparten entre todos los componentes que lo usan. El desplegable del Inspector edita ese perfil,
no una copia privada.

## Controles principales

| Control | Uso |
|---|---|
| Global Brightness | Brillo del efecto completo; no modifica la opacidad. |
| Density / Particle Count | Fracción y techo del presupuesto compartido, no cantidades por capa. |
| Spawn Radius | Cobertura que sigue a la cámara; un radio mayor distribuye las partículas más lejos. |
| Fine / Detail / Accent / Trail Layer | Activa o desactiva cada capa independientemente. |
| Trail Amount / Trail Fade Duration | Reparte más del mismo presupuesto a estelas y regula su fade de retirada. |
| Wind Enabled / Wind Angle / Wind Strength | Añade viento direccional conservando deriva natural y turbulencia. |
| Wind Visuals | Oculta solo las estelas de viento, sin detener su movimiento. |

Todos los campos del componente, perfil y capas están en [Scripts](Documentation/Scripts_ES.md).
La API, instalación por pipeline y soluciones de problemas están en [Integración](Documentation/Integration_ES.md).

## Contenido

AmbientParticlesEssentials/

- README.md / README_ES.md — presentación e inicio rápido.
- CHANGELOG.md / CHANGELOG_ES.md — historial comercial de lanzamientos.
- Runtime/ — controlador, perfiles y assembly Demo independiente.
- Editor/ — Inspector del componente y herramientas de autoría.
- Profiles/ — tres estilos predeterminados editables.
- Prefabs/ — prefab AmbientBiomeParticles.
- Materials/ — materiales de capas, viento y demo.
- Shaders/ — shaders de partículas/viento y núcleo visual compartido.
- Demo/ — escena interactiva y shader/malla de rejilla métrica procedural.
- Textures/ — asset antiguo de compatibilidad; los presets actuales no lo usan.
- Documentation/ — Integration, Scripts, Elements y ThirdParty en ambos idiomas.

Las conversiones Built-in y HDRP están junto a esta carpeta, en Assets/ObsidianForge/.
Marketing, grabación, pruebas e historial de desarrollo no forman parte del asset del cliente.

## Rendimiento

Density × Particle Count limita los sistemas de partículas nativos en conjunto; el máximo es 1200.
La malla de motes de estela tiene su propio techo de 2200. Las capas apagadas ahorran su cuota en
vez de repartirla a otras. Estelas más largas/anchas y transparencias superpuestas tienen coste de
renderizado. Mide en tu hardware objetivo; esos límites no garantizan FPS universales.

## Documentación

- [Integración](Documentation/Integration_ES.md) — instalación, demo, uso, API y problemas frecuentes.
- [Scripts](Documentation/Scripts_ES.md) — cada parámetro del Inspector y comportamiento de Reset.
- [Elementos](Documentation/Elements_ES.md) — estilos, capas, estelas, materiales y personalización.
- [Avisos de terceros](Documentation/ThirdParty_ES.md).
- [Changelog de lanzamientos](CHANGELOG_ES.md).
