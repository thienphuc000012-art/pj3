using UnityEngine;

namespace ObsidianForge.AmbientParticlesEssentials
{
    /// <summary>
    /// Reusable, asset-backed settings for an ambient particle biome.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AmbientParticleProfile",
        menuName = "Obsidian Forge/Ambient Particles Essentials/Particle Profile")]
    public sealed class AmbientParticleProfile : ScriptableObject
    {
        [Header("Style brightness")]
        [Range(0f, 8f)] [SerializeField] private float brightness = 1f;
        public float Brightness => brightness;
        public void SetBrightness(float value) { brightness = Mathf.Clamp(value,0f,8f); Revision++; }
        // Inspector edits are picked up once, not by comparing/allocating settings every frame.
        public int Revision { get; private set; }
        [Header("Identity")]
        [SerializeField] private string displayName = "Floating Dust";
        [TextArea(2, 3)]
        [SerializeField] private string description =
            "Subtle airborne dust designed for caves, ruins, dungeons and dry environments.";

        [Header("Volume")]
        [Range(0.05f, 1f)] [SerializeField] private float density = 0.8f;
        [Range(16, AmbientBiomeParticles.MaximumParticles)] [SerializeField] private int maxParticles = AmbientBiomeParticles.DefaultParticleBudget;
        [Min(2f)] [SerializeField] private float spawnRadius = AmbientBiomeParticles.DefaultSpawnRadius;
        [SerializeField] private Vector2 verticalRange = new Vector2(-1.5f, 4f);
        [Min(0f)] [SerializeField] private float minimumCameraDistance = 1.25f;
        [Min(0.5f)] [SerializeField] private float lifetime = 26f;

        [Header("Appearance")]
        [SerializeField] private Vector2 particleSize = new Vector2(0.03f, 0.085f);
        [ColorUsage(true, false)] [SerializeField] private Color color = new Color(1f, 0.82f, 0.55f, 0.26f);

        [Header("Motion")]
        [SerializeField] private Vector2 movementSpeed = new Vector2(0.015f, 0.08f);
        [InspectorName("Natural Drift Direction")]
        [Tooltip("Ambient drift retained even when the component's directional wind is disabled.")]
        [SerializeField] private Vector3 windDirection = new Vector3(1f, 0.08f, 0.25f);
        [InspectorName("Natural Drift Strength")]
        [Min(0f)] [SerializeField] private float windStrength = 0.12f;
        [Min(0f)] [SerializeField] private float turbulence = 0.08f;
        [SerializeField] private float gravityModifier;

        [Header("Layers (shared particle budget)")]
        [SerializeField] private AmbientParticleLayer[] layers;
        public int LayerCount => layers == null ? 0 : Mathf.Min(4, layers.Length);
        public AmbientParticleLayer GetLayer(int index) => layers[index];

        public string DisplayName => displayName;
        public string Description => description;
        public float Density => density;
        public int MaxParticles => maxParticles;
        public float SpawnRadius => spawnRadius;
        public Vector2 VerticalRange => verticalRange;
        public float MinimumCameraDistance => minimumCameraDistance;
        public float Lifetime => lifetime;
        public Vector2 ParticleSize => particleSize;
        public Color Color => color;
        public Vector2 MovementSpeed => movementSpeed;
        public Vector3 WindDirection => windDirection;
        public float WindStrength => windStrength;
        public float Turbulence => turbulence;
        public float GravityModifier => gravityModifier;

        /// <summary>Applies the shipping baseline for the first effect: Floating Dust.</summary>
        public void ConfigureFloatingDustDefaults()
        {
            displayName = "Floating Dust";
            description = "Subtle airborne dust designed for caves, ruins, dungeons and dry environments.";
            density = 0.8f;
            maxParticles = AmbientBiomeParticles.DefaultParticleBudget;
            spawnRadius = AmbientBiomeParticles.DefaultSpawnRadius;
            verticalRange = new Vector2(-1.5f, 4f);
            minimumCameraDistance = 1.25f;
            lifetime = 26f;
            particleSize = new Vector2(0.03f, 0.085f);
            color = new Color(1f, 0.82f, 0.55f, 0.26f);
            movementSpeed = new Vector2(0.015f, 0.08f);
            windDirection = new Vector3(1f, 0.08f, 0.25f);
            windStrength = 0.12f;
            turbulence = 0.08f;
            gravityModifier = 0f;
            brightness = 1.15f;
            layers = new[]
            {
                new AmbientParticleLayer("Fine golden dust", .64f, 1f, .35f, .45f, new Vector2(-.015f,.015f), Vector2.zero, Vector2.one, Color.white),
                new AmbientParticleLayer("Soft round motes", .25f, 2.3f, .65f, 1f, new Vector2(-.04f,.02f), Vector2.zero, Vector2.one, new Color(1f,.94f,.82f,.8f)),
                new AmbientParticleLayer("Defocused dust", .08f, 5.8f, .4f, .7f, new Vector2(-.025f,.01f), Vector2.zero, Vector2.one, new Color(1f,.9f,.7f,.28f)),
                new AmbientParticleLayer("Dust streamlets", .03f, 1.1f, 8f, .65f, new Vector2(-.02f,.05f), Vector2.zero, Vector2.one, new Color(1f,.94f,.75f,1.3f))
                    { trails = true, trailLifetime = 4.2f, trailWidth = .95f, trailOpacity = .85f, brightness = 2.8f, noiseFrequency = .2f, noiseScroll = .08f,
                        shedResidue = true, residueLifetime = 1.6f, residueSpread = .42f, residueDrift = .12f, residueSize = 1.7f,
                        residueEndTint = new Color(.72f,.6f,.44f,0f) }
            };
        }

        /// <summary>Applies the shipping baseline for airborne organic particles.</summary>
        public void ConfigurePollenAndSporesDefaults()
        {
            displayName = "Pollen & Spores";
            description = "Soft organic motes for forests, gardens, wetlands and magical biomes.";
            density = 0.8f;
            maxParticles = AmbientBiomeParticles.DefaultParticleBudget;
            spawnRadius = AmbientBiomeParticles.DefaultSpawnRadius;
            verticalRange = new Vector2(-1f, 4.8f);
            minimumCameraDistance = 1.25f;
            lifetime = 30f;
            particleSize = new Vector2(0.05f, 0.11f);
            color = new Color(0.88f, 0.94f, 0.48f, 0.4f);
            movementSpeed = new Vector2(0.03f, 0.16f);
            windDirection = new Vector3(0.7f, 0.22f, 0.4f);
            windStrength = 0.18f;
            turbulence = 0.2f;
            gravityModifier = 0f;
            brightness = 1.3f;
            color = new Color(1f,.92f,.65f,.45f);
            layers = new[]
            {
                new AmbientParticleLayer("Golden pollen", .55f, 1f, .8f, .65f, new Vector2(-.025f,.08f), Vector2.zero, Vector2.one, Color.white),
                new AmbientParticleLayer("Round luminous spores", .30f, 2.2f, .5f, 1.2f, new Vector2(.025f,.12f), Vector2.zero, Vector2.one, new Color(.82f,1f,.82f,.85f)) { noiseFrequency = .35f, noiseScroll = .12f },
                new AmbientParticleLayer("Rose spore halos", .10f, 4.5f, .4f, .7f, new Vector2(-.015f,.065f), Vector2.zero, Vector2.one, new Color(1f,.48f,.85f,.5f)),
                new AmbientParticleLayer("Spore meanders", .05f, 1.4f, 4.5f, .65f, new Vector2(.08f,.2f), Vector2.zero, Vector2.one, new Color(1f,.85f,.6f,1.2f))
                    { trails = true, trailLifetime = 4.5f, trailWidth = 1.15f, trailOpacity = .75f, brightness = 2f, noiseFrequency = .25f, noiseScroll = .1f,
                        shedResidue = true, residueLifetime = 2.2f, residueSpread = .8f, residueDrift = .16f, residueSize = 2.8f,
                        residueEndTint = new Color(.9f,.62f,.85f,0f) }
            };
        }

        /// <summary>Applies the shipping baseline for fine suspended ash and debris.</summary>
        public void ConfigureAmbientAshDefaults()
        {
            displayName = "Ambient Ash";
            description = "Fine drifting ash for volcanic, burned, industrial and post-apocalyptic biomes.";
            density = 0.8f;
            maxParticles = AmbientBiomeParticles.DefaultParticleBudget;
            spawnRadius = AmbientBiomeParticles.DefaultSpawnRadius;
            verticalRange = new Vector2(-1.2f, 4.5f);
            minimumCameraDistance = 1.25f;
            lifetime = 28f;
            particleSize = new Vector2(0.035f, 0.09f);
            color = new Color(0.48f, 0.47f, 0.46f, 0.5f);
            movementSpeed = new Vector2(0.02f, 0.11f);
            windDirection = new Vector3(0.85f, 0.03f, 0.35f);
            windStrength = 0.22f;
            turbulence = 0.14f;
            gravityModifier = 0f;
            brightness = 1.5f;
            color = new Color(1f,.82f,.66f,.5f);
            layers = new[]
            {
                new AmbientParticleLayer("Cool drifting ash", .52f, 1f, .5f, .6f, new Vector2(-.18f,-.06f), Vector2.zero, Vector2.one, new Color(.52f,.65f,.8f,.75f)),
                new AmbientParticleLayer("Round glowing embers", .30f, 2f, .6f, 1.5f, new Vector2(-.05f,.12f), Vector2.zero, Vector2.one, new Color(1f,.35f,.055f,1f)) { noiseFrequency = .4f, noiseScroll = .16f, brightness = 1.6f },
                new AmbientParticleLayer("Soft ember halos", .12f, 5f, .4f, .9f, new Vector2(-.09f,.015f), Vector2.zero, Vector2.one, new Color(1f,.23f,.04f,.45f)),
                new AmbientParticleLayer("Rising ember trails", .06f, 1.1f, 5.5f, .8f, new Vector2(.18f,.45f), Vector2.zero, Vector2.one, new Color(1f,.25f,.035f,1.3f))
                    { trails = true, trailLifetime = 3f, trailWidth = .65f, trailOpacity = .9f, noiseFrequency = .32f, noiseScroll = .12f, brightness = 2.8f,
                        shedResidue = true, residueLifetime = .95f, residueSpread = .35f, residueDrift = .22f, residueSize = 1.4f,
                        residueEndTint = new Color(.5f,.08f,.015f,0f) }
            };
        }

        private void OnValidate()
        {
            Revision++;
            brightness = Mathf.Clamp(brightness,0f,8f);
            maxParticles = Mathf.Clamp(maxParticles, 16, 1200);
            spawnRadius = Mathf.Max(2f, spawnRadius);
            minimumCameraDistance = Mathf.Clamp(minimumCameraDistance, 0f, spawnRadius - 0.1f);
            lifetime = Mathf.Max(0.5f, lifetime);
            particleSize.x = Mathf.Max(0.001f, particleSize.x);
            particleSize.y = Mathf.Max(particleSize.x, particleSize.y);
            movementSpeed.x = Mathf.Max(0f, movementSpeed.x);
            movementSpeed.y = Mathf.Max(movementSpeed.x, movementSpeed.y);
            verticalRange.y = Mathf.Max(verticalRange.x + 0.1f, verticalRange.y);
        }
    }
}
