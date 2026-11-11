# Integración

English version: [Integration.md](Integration.md)

## Requisitos

Unity 6 y un proyecto Built-in, URP o HDRP configurado. Runtime no depende de Input System,
Recorder, test-framework, Shader Graph ni paquetes de terceros. Bloom es opcional.

## Instalación

El asset principal está en Assets/ObsidianForge/AmbientParticlesEssentials/.
La edición inicial es URP. Antes de abrir la demo, importa la conversión junto a esa carpeta:

| Pipeline del proyecto | Importar desde Assets/ObsidianForge/ |
|---|---|
| URP | Ningún paquete adicional. |
| Built-in | AmbientParticlesEssentials_BuiltIn.unitypackage |
| HDRP | AmbientParticlesEssentials_HDRP.unitypackage |

Importa todos los reemplazos. Conservan GUIDs y sustituyen shaders/demo; no configuran el pipeline
del proyecto. HDRP instala además el perfil Volume de la demo. Respalda shaders/demos personalizados
antes de convertir. No importes ambas conversiones. Para volver a URP, reimporta la distribución
original y elimina el perfil Volume HDRP de la demo si permanece.

La demo HDRP usa exposición fija 0 y ningún tonemapping. Tus escenas no necesitan ese Volume;
el brillo unlit del efecto es independiente de la exposición HDRP. Tu postprocesado puede cambiar
la apariencia final.

## Escena demo

Abre Demo/AmbientParticlesDemo.unity y pulsa Play. Pollen & Spores aparece primero.
WASD mueve, Shift acelera, Q/E gira y F1 alterna el panel. El mouse solo modifica parámetros.
El panel se desplaza en pantallas pequeñas. Reset restaura controles dependientes del perfil y
activa las cuatro capas; conserva brillo global y controles de viento.

La cámara se mueve libremente por el suelo de rejilla abierto. Esta es la demo interactiva del
cliente, no la escena promocional interna: los botones de grabación/marketing no están incluidos.

## En tu juego

1. Añade Ambient Biome Particles desde Add Component > Obsidian Forge > Ambient Particles Essentials,
   o arrastra Prefabs/AmbientBiomeParticles.prefab.
2. Asigna Profile y Follow Target. Al añadir el componente en el Editor se selecciona Pollen_Default.
3. Usa la cámara de vista como Follow Target para cobertura orientada a su dirección. Sin asignación,
   puede usar la cámara activa etiquetada MainCamera. Sin fallback, usa el Transform de su host.
4. Pulsa Play. No necesitas una llamada explícita de inicio.

Puede vivir en un objeto separado, jugador o cámara. Nunca mueve su host.
Las partículas quedan en espacio mundial; solo el volumen de emisión generado sigue al observador.
Usa un efecto de seguimiento para una escena normal de una cámara. Varias instancias tienen
presupuestos propios y aumentan el coste; una instancia no cubre por separado cámaras distantes.

## Integración por código

Namespace: ObsidianForge.AmbientParticlesEssentials. Asigna un perfil serializado al crear el
componente en un build; la asignación predeterminada automática solo funciona al añadirlo en el Editor.

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
        effect.SetProfile(pollen); // Seleccionar antes de aplicar ajustes.
        effect.SetParticleCount(800);
        effect.SetSpawnRadius(20f);
        effect.SetDensity(0.8f);
        effect.SetWind(90f, 0.35f);
        effect.SetWindEnabled(true);
    }
}
```

## API runtime

Los cambios se aplican en LateUpdate durante Play. Algunos ajustes de apariencia/presupuesto
repueblan el volumen y borran estelas anteriores: son cambios de configuración, no una garantía de
interpolación continua. Consulta [Scripts](Scripts_ES.md) para límites y comportamiento de Reset.

| Miembro | Función |
|---|---|
| SetProfile(profile) / ResetToProfile() | Selecciona perfil o restaura sus valores. Perfil null detiene todas las capas. |
| SetLayerEnabled(index, enabled) | Activa capa 0–3; la limpia al desactivarla. |
| SetDensity(value) / SetParticleCount(count) | Fracción y techo total de partículas nativas. |
| SetSpawnRadius(radius) | Cobertura que sigue al observador. |
| SetTrailAmount(value) / SetTrailFadeDuration(seconds) | Peso del presupuesto de estelas y duración de fade de retirada. |
| SetParticleSize(value) / SetMovementSpeed(value) | Multiplicadores de tamaño y movimiento. |
| SetTurbulence(value) / SetOpacity(value) | Multiplicadores de ruido y transparencia. |
| SetGlobalBrightness(value) | Brillo general del componente. |
| SetWind(degrees, strength) / SetWindEnabled(enabled) | Configura viento horizontal y lo activa explícitamente. |
| SetWind(Vector3 direction, strength) | Usa yaw horizontal; X/Z no nulos activan el viento, X/Z cero lo desactivan. |
| SetWindVisuals(enabled) | Activa solo las estelas visuales; no detiene el viento. |
| RefreshSettings() | Reaplica ajustes tras editar perfil/capas por código. |
| FollowTarget / ParticleMaterial / WindSource / WindTrailMaterial | Asigna observador, material opcional, WindZone direccional o material de estela de viento. |
| Profile / LayerCount / GetLayerName(index) / IsLayerEnabled(index) | Consulta estilo y estado de capas. |
| GetLayerSystem(index) / ParticleSystem | Consulta capa 0–3 o el ParticleSystem de la capa fina. |
| WindParticleSystem / TrailResidueSystem | Consulta viento o residuos tras inicialización. |
| Density, ParticleCount, SpawnRadius, TrailAmount, TrailFadeDuration | Lee valores actuales del componente. |
| GlobalBrightness, ParticleSizeMultiplier, MovementSpeedMultiplier, TurbulenceMultiplier, OpacityMultiplier | Lee multiplicadores actuales. |
| WindEnabled, WindVisuals, WindAngle, WindStrength, WindDirection | Lee controles de viento; WindDirection es un vector horizontal normalizado. |
| profile.SetBrightness(value) / profile.GetLayer(index) | Edita brillo del estilo o accede a una capa; notifica con RefreshSettings tras editarla por código. |
| profile.ConfigureFloatingDustDefaults(), ConfigurePollenAndSporesDefaults(), ConfigureAmbientAshDefaults() | Sustituye ajustes/capas por el preset base; notifica consumidores y reselecciona/restaura cuando corresponda. |

El perfil expone valores de identidad, volumen, apariencia y movimiento de solo lectura, además de
Revision para detectar edición del Inspector. Los campos de capa son editables. El controlador es
dueño de los sistemas generados; cambios directos de módulos pueden sobrescribirse al reaplicar ajustes.

## Problemas frecuentes y rendimiento

| Síntoma | Qué comprobar |
|---|---|
| Partículas/suelo rosa | Conversión incorrecta o pipeline ausente. Reimporta la correcta; no conviertas automáticamente materiales unlit a Lit. |
| No se ven partículas | Perfil/observador, opacidad, brillo, capas y objeto activos. Da foco a Game para el teclado de la demo. |
| Cambiar estilo borra ajustes | Selecciona perfil antes de aplicar overrides. Duplica perfiles en vez de cambiar los compartidos. |
| No se ven estelas | Trail Layer, Trail Amount, Trails de la cuarta capa, opacidad y cuota. Shed Residue es un control separado. |
| No hay estelas de viento | Wind Enabled, Wind Visuals, fuerza positiva y Wisp Count. Un WindZone asignado controla el viento. |
| Viento inesperado | Usa WindZone direccional o quita la referencia para controlar ángulos manualmente. No se muestrean zonas esféricas/ráfagas. |
| Efecto demasiado grande | Prefiere escala uniforme. Se usa el eje absoluto mayor para cobertura, tamaño y movimiento; evita padres no uniformes rotados. |
| FPS bajos | Reduce density/count, apaga capas y baja duración/ancho/detalle de estelas y superposición de transparencias. Mide en build y hardware objetivo. |
| Brillo diferente | Revisa brillo global/perfil/capa y tu bloom/tonemapping; las partículas no requieren iluminación de escena. |

Las partículas nativas comparten máximo 1200 por componente, incluidos viento y residuos.
Las estelas tienen un techo separado de 2200 motes de geometría. Los valores 800 × 0.8 permiten
hasta 640 partículas nativas antes de redondeos/capas apagadas; es un techo, no una cantidad viva exacta.
Subir radio aumenta cobertura, no densidad: reparte el mismo presupuesto en un volumen mayor.
