# Elementos

English version: [Elements.md](Elements.md)

Cómo se combinan las piezas visibles y cómo personalizarlas. Referencia de parámetros:
[Scripts](Scripts_ES.md). Instalación y pipelines: [Integración](Integration_ES.md).

## Estilos y capas

Cada perfil incluido tiene cuatro capas activas. Componen una sola atmósfera, no cuatro efectos
que debas instalar por separado. Las etiquetas de la demo proceden del Name de cada capa.

| Capa | Pollen & Spores | Floating Dust | Ambient Ash |
|---|---|---|---|
| Fine (0) | Polen dorado | Polvo fino cálido | Ceniza fina fría |
| Detail (1) | Esporas verdes redondas | Motes redondos suaves | Brasas naranjas |
| Accent (2) | Halos rosados suaves | Destellos dorados desenfocados | Halos de brasa difusos |
| Trail (3) | Recorridos luminosos orgánicos | Corrientes doradas de polvo | Estelas ascendentes de chispas |

Las siluetas redondas tienen bordes difuminados y centros brillantes. Los halos de Accent son
intencionalmente más grandes y difusos. Son ambientes sutiles, no volúmenes de humo, llamas ni
clima denso.

Polen es el estilo inicial. Seleccionar otro perfil cambia paleta, materiales y movimiento, y
restaura los valores del componente ligados al perfil. Consulta el comportamiento de Reset en
[Scripts](Scripts_ES.md).

## Cobertura que sigue a la cámara

El componente sigue al observador sin mover el GameObject donde está instalado. Las partículas
nuevas ocupan un volumen con sesgo frontal alrededor del objetivo. Las existentes permanecen en
espacio mundial: al mover la cámara no arrastras toda una nube. Fuera de la cobertura retenida
se reciclan; las partículas nativas aparecen gradualmente al comienzo de su vida.

Asigna la cámara de visualización en Follow Target para orientar la cobertura con la vista.
También sirve el transform del jugador, pero su dirección puede diferir de una cámara que gira
independientemente. Es una atmósfera local para un observador, no simulación persistente de todo
el nivel ni emisión exclusivamente dentro del frustum. No hay predicción del movimiento.

Spawn Radius aumenta la región, no la cantidad. Un volumen mayor con el mismo presupuesto se
ve menos denso. Minimum Camera Distance y el fade del shader por proximidad evitan partículas
grandes atravesando la vista. No son colisiones físicas; no hay respuesta contra colliders.

## Tres elementos de estela diferentes

| Elemento | Función | Controles |
|---|---|---|
| Cabezas | Partículas móviles de la cuarta capa que originan estelas. | Trail Layer, Trail Amount y tamaño/movimiento de la capa. |
| Motes de estela | Pequeños motes brillantes, redondos y suaves depositados sobre la trayectoria mundial. | Trails, Trail Ratio, Lifetime, Width, Vertex Distance, Opacity, Motes Per Sample. |
| Residuos independientes | Partículas mayores desprendidas por las cabezas; derivan y desaparecen después de nacer. | Shed Residue y campos Residue. |

Las estelas de estilo no son cintas continuas, tubos ni una textura pegada a la cabeza. Cada
mote tiene posición mundial y envejecimiento propios; el shader anima deriva y fade entre
actualizaciones acotadas de la malla. Su pequeño billboard circular mira a la cámara, mientras
la nube de posiciones ocupa espacio 3D. Los motes individuales son sprites, no geometría volumétrica.

El polen serpentea suavemente, el polvo se dispersa en corrientes cálidas y la ceniza asciende
con carácter de chispas. Los ajustes de capa/perfil modifican el resultado sin otro componente.
Trail Fade Duration controla el retiro por proximidad/fuera de cobertura; Trail Lifetime la
persistencia normal. El residuo tiene duración y color final propios.

Trail Amount redistribuye el mismo presupuesto nativo hacia la cuarta capa; no agrega otro
presupuesto. Más cuota produce más cabezas/residuos; Trail Ratio decide qué cabezas dejan motes.
Con densidad alta se limita automáticamente el detalle por muestra al máximo compartido de
2200 motes para conservar trayectorias continuas. Estelas largas, anchas y superpuestas siguen
aumentando el coste de renderizado transparente.

## Viento y estelas de viento

El viento direccional se suma a deriva natural, movimiento vertical y turbulencia. Activa Wind,
define un ángulo en grados y ajusta Wind Strength; 0° apunta a +Z mundial y 90° a +X.
Un WindZone direccional puede proporcionar dirección y fuerza. No hay simulación de fluidos ni
evasión de obstáculos; no se interpretan WindZones esféricos ni sus parámetros de ráfagas/pulsos.

Wind Visuals agrega pequeñas estelas de viento solo con viento activo y fuerza positiva.
Son las únicas cintas nativas; su material y ajustes son independientes de las estelas de estilo.
Apagar Wind Visuals mantiene el movimiento del viento. Apagar Wind conserva el movimiento natural.
Global Brightness y la capa de viento controlan su brillo; Brightness del perfil no lo afecta.

## Brillo, opacidad y materiales

Brillo de estilo = Global Brightness × Brightness del perfil × Brightness de la capa.
Brillo de viento = Global Brightness × Brightness de la capa de viento.
Color/alfa combinan color del perfil, tinte de capa, fade de vida y Opacity del componente.
Los motes de estela tienen además énfasis de brillo en el centro.

Brightness cambia intensidad de color; Opacity cambia transparencia. Ninguno ilumina la escena.
Bloom es opcional y depende del pipeline/postprocesado; no hacen falta luces.

| Shader / propiedad | Significado |
|---|---|
| AmbientParticle.shader: Form | Silueta procedural: Mote, Grain, Spore, Halo, Ember o Haze. |
| AmbientParticle.shader: Brightness | Brillo del material si se renderiza solo; el controlador lo reemplaza por renderer en ejecución. |
| AmbientTrail.shader: Brightness | Brillo de las cintas de viento; también lo controla el componente. |
| MetricGrid.shader: Base Color | Color de líneas de la rejilla de demo. |
| MetricGrid.shader: Floor Color | Color del suelo. |
| MetricGrid.shader: Horizon Color | Color de mezcla hacia el horizonte. |

Camera Fade, Wake Mode y Wake Time son controles internos ocultos, no ajustes para crear perfiles.
Las propiedades ocultas heredadas Style/Trail Scale del shader de viento no son controles de
apariencia soportados. Particle Form pertenece al material de capa, no al Inspector del componente.

Deja Particle Material vacío para conservar las siluetas de cada capa. Su reemplazo opcional
afecta varios renderers de partículas, incluido el viento. Un material personalizado sin
propiedades de estela no reemplaza ese shader: el controlador conserva un material compatible.
Duplica los materiales antes de cambiar Form o color. Editar solo Brightness del material puede
parecer inefectivo porque el componente suministra su multiplicador.

Los shaders comparten AmbientVisualCore.hlsl. URP es la edición base; los paquetes de conversión
adyacentes reemplazan envoltorios de pipeline y assets de demo conservando GUIDs. No mezcles
archivos de distintas ediciones. Consulta [Integración](Integration_ES.md).

Textures/DustSoftParticle.asset y dos materiales heredados permanecen por compatibilidad; los
efectos actuales usan siluetas procedurales, no esa textura. No son estilos adicionales.

## Crear otra atmósfera

1. Duplica un perfil incluido y los materiales que quieras modificar.
2. Conserva cuatro capas en el mismo orden para mantener predecibles los controles Inspector/API.
3. Define paleta, deriva natural, tamaño y duración del perfil; refina tinte, brillo, tamaño,
   ruido y movimiento por capa.
4. Ajusta por separado cabezas, persistencia de motes y residuos. Reduce halos superpuestos
   antes de aumentar el presupuesto.
5. Asigna el perfil y después los ajustes de componente: observador, viento y brillo.
6. Prueba avanzando, desplazándote lateralmente y girando, con densidad baja y alta. Mide el
   dispositivo objetivo a la resolución de salida real.

Editar un perfil compartido afecta a todos sus consumidores. Después de editar capas por código,
llama RefreshSettings() en los consumidores. Para aplicar cambios en defaults de densidad,
presupuesto/radio, reselecciona el perfil o llama ResetToProfile(). Estas operaciones pueden
repoblar partículas: no son transiciones suaves entre presets.

Se recomienda escala uniforme: cobertura, tamaños y movimiento escalan juntos; las duraciones
siguen en segundos. Con escala no uniforme se toma el mayor eje absoluto, sin estirar XYZ
independientemente las partículas redondas.

## Elementos de demo y alcance

La demo usa un suelo de rejilla métrica procedural que se recentra bajo el observador, dando
sensación de terreno abierto; no es una superficie física infinita. El espaciado de rejilla está
definido en el shader, no como parámetro del componente. Cámara y UI son ejemplos independientes
del componente necesario en tu juego.

La escena promocional, cinemática, grabador de video, herramientas de capturas, experimentos
anteriores de estela y sus instrucciones están fuera del paquete del cliente. No son dependencias.

