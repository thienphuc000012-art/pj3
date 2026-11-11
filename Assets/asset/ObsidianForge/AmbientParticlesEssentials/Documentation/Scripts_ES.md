# Scripts

English version: [Scripts.md](Scripts.md)

Referencia de todos los parámetros visibles. Los nombres de campo se conservan como aparecen en
Unity; los nombres entre paréntesis identifican el código/serialización. API: [Integración](Integration_ES.md#api-runtime).

## Archivos y responsabilidades

| Archivo | Responsabilidad |
|---|---|
| Runtime/Components/AmbientBiomeParticles.cs | Único componente necesario en juego; volumen, capas, viento y estelas. |
| Runtime/Profiles/AmbientParticleProfile.cs | Asset compartido de estilo y valores predeterminados. |
| Runtime/Profiles/AmbientParticleLayer.cs | Definición serializable de cada capa, no un componente que debas añadir. |
| Editor/AmbientBiomeParticlesEditor.cs | Inspector y edición inline del perfil; no se compila en el juego. |
| Editor/Demo/AmbientParticlesDemoSceneCreator.cs | Genera/restaura demo y presets; herramienta de Editor. |
| Runtime/Demo/AmbientParticlesDemo.cs | UI de la demo; no necesaria para tu juego. |
| Runtime/Demo/DemoOrbitCamera.cs | Navegación con teclado de la demo. |
| Runtime/Demo/DemoEndlessGround.cs | Seguimiento de suelo de la demo. |
| Runtime/AssemblyMarker.cs | Identidad del assembly, sin controles ni función visual. |

## AmbientBiomeParticles: Inspector del componente

| Field / code name | Range / default | Función y cuándo usarlo |
|---|---|---|
| Global Brightness (globalBrightness) | 0–8; default 1 | Multiplica brillo de estilo/capa sin cambiar alfa; el viento usa global × brillo de su capa. |
| Profile (profile) | Pollen_Default | Asset de apariencia/movimiento. Seleccionar antes de ajustes; null detiene todas las capas. |
| Particle Material (particleMaterial) | Optional | Sobrescribe materiales de partículas. Vacío conserva los de cada estilo; shaders propios deben admitir sus propiedades. |
| Follow Target (followTarget) | Optional | Transform de cámara/jugador a rodear; una cámara alinea la cobertura frontal con la vista. |
| Use Main Camera When Target Missing (useMainCameraWhenTargetMissing) | On | Usa MainCamera activa como respaldo. Apagado y sin referencia usa el Transform del host. |
| Recenter Radius Fraction (recenterRadiusFraction) | 0.1–1; default 0.25 | Distancia recorrida como fracción del radio antes de reciclar partículas lejanas; menor reacciona antes. |
| Forward Bias (forwardBias) | 0–0.8; default 0.4 | Desplaza emisión hacia el frente horizontal conservando periferia; no predice movimientos. |
| Retained Volume Padding (retainedVolumePadding) | 1–2; default 1.15 | Margen para conservar partículas existentes; mayor reduce reciclaje pero mantiene partículas más lejanas. |
| Fine Layer (fineLayer) | On | Capa 0. Apagar la detiene/limpia y ahorra su cuota. |
| Detail Layer (detailLayer) | On | Capa 1. Activa detalles por separado. |
| Accent Layer (accentLayer) | On | Capa 2. Activa halos/acentos por separado. |
| Trail Layer (trailLayer) | On | Capa 3; activa cabezas, estelas de motes y residuos juntos. |
| Density (density) | 0.01–1; default 0.8 | Fracción utilizada de Particle Count. No son partículas por unidad cúbica. |
| Particle Count / Total Particle Budget (particleCount) | 16–1200; default 800 | Techo compartido por sistemas nativos incluidos residuos/viento; se multiplica por Density. |
| Trail Amount (trailAmount) | 0–5; default 3 | Multiplica el peso de la cuarta capa antes de normalizar; más estelas dejan menos cuota a otras. Cero elimina cabezas/residuos/estelas. |
| Trail Fade Duration (trailFadeDuration) | 0.2–2 s; default 0.85 | Tiempo de retirada de cabezas/estelas cerca del observador o fuera de cobertura; independiente de vida normal y fade de residuos. |
| Particle Size Multiplier (particleSizeMultiplier) | 0.1–4; default 1 | Multiplica tamaños del perfil/capa. |
| Movement Speed Multiplier (movementSpeedMultiplier) | 0–4; default 1 | Escala velocidad base, deriva natural, vertical, giro y deriva de residuos. Viento direccional tiene fuerza propia; cero no apaga ruido/viento. |
| Turbulence Multiplier (turbulenceMultiplier) | 0–4; default 1 | Escala fuerza del ruido nativo; cero elimina esa turbulencia. |
| Wind Enabled (windEnabled) | Off | Activa viento horizontal adicional; el movimiento natural sigue funcionando. |
| Wind Angle (windAngle) | 0–360°; default 65° | Yaw mundial: 0 = +Z, 90 = +X, 180 = −Z, 270 = −X. Cero es una dirección válida. |
| Wind Strength (windStrength) | 0–1.5; default 0.12 | Control de fuerza/velocidad direccional. Cero oculta estelas de viento; no detiene deriva natural. |
| Wind Source (windSource) | Optional WindZone | Zona direccional aporta yaw frontal y windMain. Inactiva apaga viento; se ignoran zonas esféricas/pulsos. |
| Wind Visuals (windVisuals) | On, armed | Permite estelas solo con viento activo y fuerza positiva. Apagar oculta visuales, no el movimiento. |
| Wind Wisp Count (windWispCount) | 0–12; default 8 | Techo deseado de partículas de viento, limitado además al 5% del presupuesto compartido. |
| Wind Wisps (windWisps) | Layer settings | Bloque independiente de apariencia/movimiento; Budget Share se ignora porque Wisp Count controla su cuota. |
| Opacity (opacity) | 0–1; default 1 | Multiplica transparencia. Cero oculta partículas sin apagar la simulación. |
| Spawn Radius (spawnRadius) | 2–32; default 20 | Cobertura a escala 1 en unidades mundiales. Emisión en caja adelantada, no esfera exacta; mayor reparte el mismo presupuesto. |

Fine/Detail/Accent/Trail son índices 0/1/2/3. Se crean sistemas reutilizables; no añadas scripts a sus
hijos. Apagar capa limpia partículas/estela y ahorra su cuota, no aumenta la de otras.
La escala uniforme del host afecta cobertura, tamaño y movimiento; los tiempos no se escalan.

## AmbientParticleProfile: Inspector del estilo

Crea perfiles desde Assets > Create > Obsidian Forge > Ambient Particles Essentials > Particle Profile,
o duplica un preset de Profiles/. El desplegable Style brightness and independent layers del
componente edita exactamente este asset compartido.

| Field / code name | Range / default | Función y cuándo usarlo |
|---|---|---|
| Brightness (brightness) | 0–8 | Brillo solo del estilo; siguen multiplicadores global/capa. No afecta a estelas de viento. |
| Display Name (displayName) | Text | Nombre en el selector de la demo. |
| Description (description) | Text | Descripción mostrada en demo. |
| Density (density) | 0.05–1; default 0.8 | Densidad del componente al seleccionar/restaurar perfil. |
| Max Particles (maxParticles) | 16–1200; default 800 | Presupuesto total predeterminado del componente. |
| Spawn Radius (spawnRadius) | Default 20 | Cobertura inicial; el componente la limita a 2–32 al validar/usar sus controles. |
| Vertical Range (verticalRange) | World units; Y > X | Alturas mínima/máxima relativas al observador, escaladas con el host. |
| Minimum Camera Distance (minimumCameraDistance) | 0 to below profile radius | Distancia de exclusión próxima. Partículas sin estela se eliminan dentro; cabezas se retiran gradualmente. Cero desactiva exclusión/fade. |
| Lifetime (lifetime) | Seconds; ≥0.5 | Vida base multiplicada por capa, con variación por partícula. |
| Particle Size (particleSize) | Min/max world size | Rango aleatorio de tamaño. X/Y uniforme conserva siluetas redondas. |
| Color (color) | RGBA | Color/alfa base multiplicado por tint de capa y opacidad del componente. |
| Movement Speed (movementSpeed) | Min/max speed | Velocidad inicial aleatoria; se aplican multiplicadores de capa/componente. |
| Natural Drift Direction (windDirection) | Vector3 | Dirección normalizada de deriva base, admite vertical; permanece con viento direccional apagado/activo. |
| Natural Drift Strength (windStrength) | ≥0 | Magnitud de deriva base afectada por Movement Speed Multiplier; separada de Wind Strength direccional. |
| Turbulence (turbulence) | ≥0 | Fuerza base de ruido combinada con multiplicadores del componente/capa. |
| Gravity Modifier (gravityModifier) | Unity gravity multiplier | Aporte de gravedad; positivo normalmente cae y negativo invierte gravedad. Cero conserva deriva/ruido. |
| Layers (layers) | Four shipped layers | Definiciones ordenadas independientes; consulta abajo. Conserva cuatro slots para controles previsibles. |

## AmbientParticleLayer: controles por capa

Los siguientes campos están en Layers del perfil o en Wind Wisps del componente. Controles de
residuos solo funcionan en la cuarta capa del estilo. Viento usa un presupuesto propio reservado
dentro del total: su Budget Share y sus campos de residuos/motes no se usan.

| Field / code name | Range / default | Función y cuándo usarlo |
|---|---|---|
| Name (name) | Text | Nombre de capa en Inspector/demo. |
| Budget Share (budgetShare) | 0–1 | Peso relativo normalizado con otras capas y Trail Amount; no cantidad absoluta. Cuotas apagadas no se redistribuyen. |
| Material (material) | Optional | Material de partículas de la capa; los de serie contienen la silueta procedural. |
| Brightness (brightness) | 0–8 | Brillo independiente multiplicado por perfil/global (viento no usa brillo del perfil). |
| Size Multiplier (sizeMultiplier) | ≥0.01 | Escala tamaño base. |
| Lifetime Multiplier (lifetimeMultiplier) | 0.1–4 | Escala vida base; no multiplica la vida independiente de residuos. |
| Speed Multiplier (speedMultiplier) | ≥0 | Escala velocidad inicial aleatoria, no viento direccional ni el rango Vertical Speed. |
| Noise Multiplier (noiseMultiplier) | ≥0 | Escala turbulencia; cero elimina ruido nativo de esta capa. |
| Noise Frequency (noiseFrequency) | ≥0.01 | Frecuencia del ruido; mayor produce variaciones de menor escala. |
| Noise Scroll (noiseScroll) | ≥0 | Velocidad de evolución del ruido; cero mantiene su patrón quieto. |
| Vertical Speed (verticalSpeed) | Min/max world speed | Velocidad Y adicional; negativa cae, positiva sube. Se aplican escala y multiplicador de movimiento. |
| Angular Speed (angularSpeed) | Min/max radians/s | Rango de giro del billboard afectado por multiplicador de movimiento. |
| Brightness Variation (brightnessVariation) | 0–1 | Variación aleatoria de brillo/color; mayor amplía el rango oscuro-claro. |
| Tint (tint) | RGBA | Multiplica color/alfa del perfil; alfa controla esta capa por separado. |
| Trails (trails) | Toggle | Activa estelas de motes en cuarta capa; cintas nativas solo en viento. Otras capas no generan estelas. |
| Trail Material (trailMaterial) | Optional | Solo material de cinta de viento. Estelas de estilo usan material redondo de cuarta capa. |
| Trail Ratio (trailRatio) | 0–1 | Fracción de cabezas con estelas de motes/cintas; no apaga residuos. |
| Trail Lifetime (trailLifetime) | 0.1–5 s; wind ≤3 s | Duración de motes depositados; máxima duración de cinta de viento. Mayor aumenta persistencia/coste. |
| Trail Width (trailWidth) | 0.01–1.5; wind ≤0.5 | Dispersión espacial de estela de estilo o ancho de cinta de viento a escala 1. |
| Trail Vertex Distance (trailVertexDistance) | 0.04–0.3 | Distancia entre muestras; menor añade detalle/coste. Cabezas lentas siguen depositando periódicamente. |
| Trail Opacity (trailOpacity) | 0–1 | Opacidad de estela de motes/cinta de viento, separada de cabeza. |
| Trail Motes Per Sample (trailMotesPerSample) | 1–4; default 3 | Motes deseados por muestra. Se reduce detalle para respetar techo de geometría; no se usa en viento nativo. |
| Shed Residue (shedResidue) | Fourth layer only | Activa partículas independientes mayores detrás de cabezas, separado de Trails. |
| Residue Budget Share (residueBudgetShare) | 0.5–0.9; default 0.75 | Reserva parte de cuota de cuarta capa para residuos, dejando una cabeza cuando el cupo lo permite. |
| Residue Lifetime (residueLifetime) | 0.2–3 s | Vida independiente de residuos con variación. |
| Residue Rate (residueRate) | 1–12 | Emisiones deseadas por cabeza/segundo; limitadas por cuota/vida disponibles. |
| Residue Size (residueSize) | 0.1–3.5 | Tamaño relativo a cabeza de origen, con variación y expansión. |
| Residue Spread (residueSpread) | 0.01–1.2 | Dispersión mundial alrededor de cabeza, escalada con host. |
| Residue Drift (residueDrift) | 0–0.5 | Deriva aleatoria independiente; se aplica multiplicador de movimiento. Viento sigue sumándose. |
| Residue Opacity (residueOpacity) | 0–1 | Transparencia relativa a cabeza de origen. |
| Residue End Tint (residueEndTint) | RGBA colour | Color de enfriamiento/dispersión; también influye en color de estelas envejecidas. Fade controla opacidad final. |

Aspect (aspect) es un dato antiguo oculto de compatibilidad; el runtime usa tamaño uniforme X/Y.
No permite deformar los billboards redondos actuales.

## Reset, perfiles y edición durante Play

- ResetToProfile y el botón Reset restauran Density, Particle Count y Spawn Radius del perfil;
  tamaño, velocidad, turbulencia y opacidad a 1; Trail Amount a 3; y activan las cuatro capas.
- SetProfile hace lo mismo. Mantiene Global Brightness, viento, seguimiento, materiales y Trail Fade Duration.
  Cambiar perfil no restaura edits hechos dentro del propio asset de perfil.
- Editar un perfil en el Inspector afecta a todos sus consumidores y puede persistir como edición
  del asset al salir de Play. No uses un perfil compartido como copia temporal.
- Cambios por código en capas requieren RefreshSettings en cada consumidor. SetBrightness del perfil
  notifica su revisión; configurar presets por código requiere notificar/reseleccionar.
- Cambios de presupuesto, radio, tamaño, velocidad, opacidad o perfil pueden repoblar el volumen.
  No los uses como animación continua si necesitas conservar exactamente las partículas existentes.

## Scripts y UI de la demo

AmbientParticlesDemo:

- Ambient Particles (ambientParticles): controlador al que se aplica el panel.
- Profiles (profiles): lista y orden de estilos; el incluido empieza en polen.
- Show Ui (showUi): panel inicialmente visible; F1 lo alterna.
- Controles UI: estilo, cuatro capas, brillo global, densidad, presupuesto, Trail Amount, tamaño,
  velocidad, turbulencia, opacidad, radio, viento activo, estelas de viento, dirección y fuerza.
  Son los mismos valores del componente, no una configuración duplicada.

DemoOrbitCamera:

- Walk Speed (walkSpeed): unidades/segundo, inicial 5.
- Sprint Multiplier (sprintMultiplier): factor al mantener Shift, inicial 2.
- Turn Speed (turnSpeed): grados/segundo con Q/E, inicial 105.
- No hay giro con mouse ni bloqueo del cursor; da foco a Game para usar el teclado.

DemoEndlessGround:

- Observer (observer): Transform de seguimiento; vacío usa MainCamera.
- Snap Distance (snapDistance): paso mundial de recolocación de suelo, inicial 10, mínimo efectivo 1.
  No controla el volumen de partículas.

El generador Create Ambient Particles Demo restaura perfiles/materiales de serie y prefab/escena.
Respalda personalizaciones antes de usarlo. Está pensado para edición URP inicial; en Built-in/HDRP
usa las demos convertidas incluidas.
