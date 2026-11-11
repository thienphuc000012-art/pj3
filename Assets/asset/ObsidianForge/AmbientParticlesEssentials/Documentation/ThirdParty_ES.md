# Avisos de terceros

English version: [ThirdParty.md](ThirdParty.md)

Ambient Particles Essentials contiene código C#, shaders HLSL personalizados, siluetas de
partículas procedurales, geometría de demo generada, perfiles, materiales y una escena creados
por el publisher. No incluye código, modelos, audio ni texturas de origen externo de terceros.

El asset heredado DustSoftParticle contiene datos de textura generados, no una imagen de terceros.
Los presets actuales no lo usan.

## Dependencias de Unity

Unity 6 proporciona Particle System, las APIs de malla/renderizado e IMGUI utilizados.
Usa un proyecto configurado para URP, HDRP o Built-in y la edición correspondiente del paquete.
URP/HDRP requieren los paquetes de renderizado de Unity en tu proyecto; no son contenido de
terceros incrustado en este asset. Consulta [Integración](Integration_ES.md).

El runtime no requiere Input System, Shader Graph, VFX Graph, Unity Recorder, framework de
pruebas ni plugins de terceros. La navegación/UI de demo están en un assembly Demo separado.

Recorder, pruebas y herramientas promocionales del publisher quedan fuera del paquete del
cliente. No son necesarios para usar el efecto. Para las condiciones de Unity, sus paquetes
y tu adquisición en Asset Store consulta sus licencias respectivas; este aviso no las reemplaza.

