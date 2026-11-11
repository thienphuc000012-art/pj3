using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace ObsidianForge.AmbientParticlesEssentials
{
    /// <summary>Reusable world-space layers sharing one bounded particle budget.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Obsidian Forge/Ambient Particles Essentials/Ambient Biome Particles")]
    public sealed class AmbientBiomeParticles : MonoBehaviour
    {
        public const int MaximumParticles = 1200;
        public const int MaximumWakeMotes = 2200;
        public const int DefaultParticleBudget = 800;
        public const float DefaultSpawnRadius = 20f;
        [Header("Global brightness")]
        [Range(0f, 8f)] [SerializeField] private float globalBrightness = 1f;
        [Header("Profile")]
        [SerializeField] private AmbientParticleProfile profile;
        [Tooltip("Optional override for all layers. Leave empty for the profile materials.")]
        [SerializeField] private Material particleMaterial;
        [Header("Follow")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private bool useMainCameraWhenTargetMissing = true;
        [Range(.1f, 1f)] [SerializeField] private float recenterRadiusFraction = .25f;
        [Header("View-Weighted Emission")]
        [Range(0f, .8f)] [SerializeField] private float forwardBias = .4f;
        [Range(1f, 2f)] [SerializeField] private float retainedVolumePadding = 1.15f;
        [Header("Layers")]
        [SerializeField] private bool fineLayer = true;
        [SerializeField] private bool detailLayer = true;
        [SerializeField] private bool accentLayer = true;
        [SerializeField] private bool trailLayer = true;
        [Header("Runtime Controls")]
        [Range(.01f, 1f)] [SerializeField] private float density = .8f;
        [Tooltip("Total budget across ALL layers. Disabled layers save their share.")]
        [Range(16, MaximumParticles)] [SerializeField] private int particleCount = DefaultParticleBudget;
        [Tooltip("Relative amount of trail particles and their residue. 1 uses the profile balance; 0 removes them. Shares the total budget with other layers.")]
        [Range(0f, 5f)] [SerializeField] private float trailAmount = 3f;
        [Tooltip("Seconds for a trail head and its 3D wake to fade when they leave the volume or approach the observer. Detached residue has its own lifetime.")]
        [Range(.2f, 2f)] [SerializeField] private float trailFadeDuration = .85f;
        [Range(.1f, 4f)] [SerializeField] private float particleSizeMultiplier = 1f;
        [Range(0f, 4f)] [SerializeField] private float movementSpeedMultiplier = 1f;
        [Range(0f, 4f)] [SerializeField] private float turbulenceMultiplier = 1f;
        [Header("Directional wind — added to natural motion")]
        [SerializeField] private bool windEnabled;
        [Tooltip("World-space yaw: 0 = +Z, 90 = +X. Zero degrees is a valid direction.")]
        [Range(0f, 360f)] [SerializeField] private float windAngle = 65f;
        [Range(0f, 1.5f)] [SerializeField] private float windStrength = .12f;
        [Tooltip("Optional directional WindZone. Uses forward yaw and windMain; spherical zones are ignored.")]
        [SerializeField] private WindZone windSource;
        [SerializeField] private bool windVisuals = true;
        [Range(0, 12)] [SerializeField] private int windWispCount = 8;
        [SerializeField] private AmbientParticleLayer windWisps = new AmbientParticleLayer(
            "Wind wisps", 0f, .2f, 1f, .35f, Vector2.zero, Vector2.zero, Vector2.one, new Color(.7f,.8f,.9f,.22f))
            { trails = true, trailLifetime = 1.2f, trailWidth = .06f, trailOpacity = .7f, noiseFrequency = .35f };
        [Header("Volume and transparency")]
        [Range(0f, 1f)] [SerializeField] private float opacity = 1f;
        [Range(2f, 32f)] [SerializeField] private float spawnRadius = DefaultSpawnRadius;

        private sealed class LayerRuntime
        {
            internal ParticleSystem system;
            internal ParticleSystemRenderer renderer;
            internal ParticleSystem.Particle[] buffer;
            internal int budget;
            internal readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            internal readonly Dictionary<uint, Retirement> retiring = new Dictionary<uint, Retirement>();
            internal readonly HashSet<uint> liveSeeds = new HashSet<uint>();
            internal readonly List<uint> staleSeeds = new List<uint>();
        }
        private struct Retirement
        {
            internal float started;
            internal Color32 originalColor;
        }
        private struct WakePoint
        {
            internal Vector3 position;
            internal float time;
            internal uint id;
        }
        private sealed class WakeTrack
        {
            internal readonly List<WakePoint> points = new List<WakePoint>(48);
            internal Color32 color;
            internal bool seen;
            internal int motesPerSample;
        }
        private readonly LayerRuntime[] runtime = new LayerRuntime[6];
        private readonly Dictionary<uint,WakeTrack> wakeTracks = new Dictionary<uint,WakeTrack>();
        private readonly List<uint> expiredWakeSeeds = new List<uint>();
        private readonly List<Vector3> wakeVertices = new List<Vector3>(4096);
        private readonly List<Vector2> wakeUvs = new List<Vector2>(4096);
        private List<Vector4> wakeMotion = new List<Vector4>(4096);
        private List<Vector2> wakeTimes = new List<Vector2>(4096);
        private readonly List<Color32> wakeColors = new List<Color32>(4096);
        private readonly List<int> wakeTriangles = new List<int>(8192);
        private MaterialPropertyBlock wakeProperties;
        private Mesh wakeMesh;
        private MeshRenderer wakeRenderer;
        private float nextWakeUpdate;
        private uint wakePointSequence;
        private const int MoteVertices = 4;
        private const float WakeMeshRefreshSeconds = .09f;
        private const float WakeBudgetSampleSeconds = .13f;
        private const float WakeMaximumQuietSeconds = .18f;
        private const float WakeTrackTurnoverReserve = 1.4f;
        private static readonly Vector2[] MoteCorners = {
            new Vector2(0f,0f),new Vector2(0f,1f),new Vector2(1f,0f),new Vector2(1f,1f)
        };
        private static readonly int[] MoteFaces = { 0,1,2, 2,1,3 };
        private static readonly int WakeModeId = Shader.PropertyToID("_WakeMode");
        private static readonly int WakeTimeId = Shader.PropertyToID("_WakeTime");
        private SphereCollider exclusion;
        private Material fallbackMaterial;
        private Material fallbackTrailMaterial;
        private Camera cachedCamera;
        private bool dirty = true, repopulate = true, placed;
        private Vector3 lastPosition, lastForward;
        private float nextMaintenance, nextStationaryMaintenance, appliedScale;
        private int appliedRevision;
        private float nextResidueEmission;
        private float nextRetirementUpdate;
        private uint residueSequence;
        private int residueParentCursor;
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int TrailScaleId = Shader.PropertyToID("_TrailScale");
        private static readonly int CameraFadeId = Shader.PropertyToID("_CameraFade");
        private static readonly AnimationCurve TrailWidth = AnimationCurve.Linear(0f,1f,1f,0f);
        private static readonly AnimationCurve ResidueSize = AnimationCurve.EaseInOut(0f,.6f,1f,1.25f);
        private Gradient residueFade = new Gradient();
        private GradientColorKey[] residueColors = new GradientColorKey[3];
        private static readonly GradientAlphaKey[] ResidueAlphas = {
            new GradientAlphaKey(0f,0f),new GradientAlphaKey(1f,.12f),new GradientAlphaKey(.5f,.5f),new GradientAlphaKey(0f,1f) };
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("AmbientParticles.Update");
        private readonly Gradient fade = CreateFade();
        private static readonly AmbientParticleLayer LegacyLayer = new AmbientParticleLayer(
            "Ambient particles",1f,1f,1f,1f,Vector2.zero,Vector2.zero,Vector2.one,Color.white);

        public AmbientParticleProfile Profile => profile;
        /// <summary>The fine layer; inspect other layers with GetLayerSystem.</summary>
        public ParticleSystem ParticleSystem => runtime[0]?.system;
        public int LayerCount => profile == null ? 0 : Mathf.Max(1,profile.LayerCount);
        public float Density => density;
        public int ParticleCount => particleCount;
        public float TrailAmount => trailAmount;
        public float TrailFadeDuration => trailFadeDuration;
        public float ParticleSizeMultiplier => particleSizeMultiplier;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;
        public float TurbulenceMultiplier => turbulenceMultiplier;
        public float OpacityMultiplier => opacity;
        public float SpawnRadius => spawnRadius;
        public float WindStrength => windStrength;
        public float GlobalBrightness => globalBrightness;
        public float WindAngle => windAngle;
        public bool WindEnabled => windEnabled;
        public bool WindVisuals => windVisuals;
        public Vector3 WindDirection => Quaternion.Euler(0f,windAngle,0f)*Vector3.forward;
        public ParticleSystem WindParticleSystem => runtime[4]?.system;
        /// <summary>Detached particles shed by the fourth layer; shares its budget and toggle.</summary>
        public ParticleSystem TrailResidueSystem => runtime[5]?.system;
        public WindZone WindSource { get => windSource; set => windSource = value; }
        public Material WindTrailMaterial { get => windWisps.trailMaterial; set { windWisps.trailMaterial = value; RequestSettings(false); } }
        public Transform FollowTarget { get => followTarget; set { followTarget = value; placed = false; } }
        public Material ParticleMaterial { get => particleMaterial; set { particleMaterial = value; RequestSettings(false); } }
        public ParticleSystem GetLayerSystem(int index) => index >= 0 && index < 4 ? runtime[index]?.system : null;
        public string GetLayerName(int index) => Definition(index).name;
        public bool IsLayerEnabled(int index) => index == 0 ? fineLayer : index == 1 ? detailLayer : index == 2 ? accentLayer : index == 3 && trailLayer;
        public void SetLayerEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= 4 || IsLayerEnabled(index) == enabled) return;
            if (index == 0) fineLayer = enabled;
            else if (index == 1) detailLayer = enabled;
            else if (index == 2) accentLayer = enabled;
            else trailLayer = enabled;
            RequestSettings(false);
        }

        // One conservative factor applies to every authored distance, including scaled parents.
        private float WorldScale => Mathf.Max(.001f,Mathf.Max(Mathf.Abs(transform.lossyScale.x),
            Mathf.Max(Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z))));
        private AmbientParticleLayer Definition(int index) => index == 4 ? windWisps : profile != null && index >= 0 && (index == 5 ? 3 : index) < profile.LayerCount
            ? profile.GetLayer(index == 5 ? 3 : index) ?? LegacyLayer : LegacyLayer;
        private void Awake() => InitializeParticleSystem();
#if UNITY_EDITOR
        // Assign the shipped default when a buyer adds the component from the Inspector.
        // Builds use the serialized profile in the supplied prefab or an explicit SetProfile call.
        private void Reset()
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath("5362317a36d6347208924a11828c9535");
            profile = UnityEditor.AssetDatabase.LoadAssetAtPath<AmbientParticleProfile>(path);
            if (profile != null) ResetToProfile();
        }
#endif
        private void OnEnable() { dirty = true; repopulate = true; placed = false; }
        private void OnDisable()
        {
            ClearWakes();
            for (var i = 0; i < runtime.Length; i++)
                if (runtime[i]?.system != null)
                {
                    runtime[i].system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    runtime[i].retiring.Clear();
                }
        }
        private void OnDestroy()
        {
            if (wakeMesh != null)
            {
                if (Application.isPlaying) Destroy(wakeMesh);
                else DestroyImmediate(wakeMesh);
            }
            if (Application.isPlaying) { Destroy(fallbackMaterial); Destroy(fallbackTrailMaterial); }
            else { DestroyImmediate(fallbackMaterial); DestroyImmediate(fallbackTrailMaterial); }
        }
        private void OnValidate()
        {
            particleCount = Mathf.Clamp(particleCount,16,MaximumParticles);
            density = Mathf.Clamp(density,.01f,1f);
            trailAmount = Mathf.Clamp(trailAmount,0f,5f);
            trailFadeDuration = Mathf.Clamp(trailFadeDuration,.2f,2f);
            spawnRadius = Mathf.Clamp(spawnRadius,2f,32f);
            globalBrightness = Mathf.Clamp(globalBrightness,0f,8f);
            windStrength = Mathf.Clamp(windStrength,0f,1.5f);
            windAngle = Mathf.Repeat(windAngle,360f);
            dirty = true; repopulate = true;
        }
        private void LateUpdate()
        {
            using (UpdateMarker.Auto()) Tick(Time.unscaledTime);
        }
        internal void Tick(float now)
        {
            if (windSource != null && windSource.mode == WindZoneMode.Directional)
            {
                var direction = windSource.transform.forward;
                SetWind(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,
                    windSource.gameObject.activeInHierarchy ? windSource.windMain : 0f);
                SetWindEnabled(windSource.gameObject.activeInHierarchy);
            }
            if (profile != null && appliedRevision != profile.Revision) { dirty = true; repopulate = true; }
            if (!Mathf.Approximately(appliedScale,WorldScale)) { dirty = true; repopulate = true; }
            if (dirty) ApplySettings();
            if (profile == null) return;
            var target = ResolveTarget();
            var position = target == null ? transform.position : target.position;
            var forward = PlanarForward(target == null ? transform : target);
            // Only generated children move. Attaching to a camera/player never moves that host.
            exclusion.transform.SetPositionAndRotation(position,Quaternion.identity);
            ConfigureVolumes(position,forward);
            var distance = Mathf.Max(.5f,spawnRadius*WorldScale*recenterRadiusFraction);
            if (!placed || now >= nextStationaryMaintenance || ((position-lastPosition).sqrMagnitude >= distance*distance
                || Vector3.Dot(forward,lastForward) < .87f) && now >= nextMaintenance)
            {
                // A teleport must discard old trail history, not connect distant volumes.
                if (placed && (position-lastPosition).sqrMagnitude > 4f*spawnRadius*spawnRadius*appliedScale*appliedScale)
                {
                    ClearWakes();
                    for (var i = 0; i < runtime.Length; i++)
                    {
                        runtime[i].system.Clear(false);
                        runtime[i].retiring.Clear();
                    }
                }
                for (var i = 0; i < 5; i++)
                    if (runtime[i].budget > 0) Refill(runtime[i],position,forward,!placed,now);
                lastPosition = position; lastForward = forward; placed = true;
                nextMaintenance = now+.15f;
                nextStationaryMaintenance = now+2f;
            }
            UpdateTrailRetirements(position,now);
            EmitResidue(now);
            UpdateWakes(now);
        }
        public void SetProfile(AmbientParticleProfile value)
        {
            profile = value;
            if (profile == null) { RequestSettings(true); return; }
            ResetToProfile();
        }
        public void ResetToProfile()
        {
            if (profile == null) return;
            density = profile.Density; particleCount = profile.MaxParticles; spawnRadius = profile.SpawnRadius;
            particleSizeMultiplier = movementSpeedMultiplier = turbulenceMultiplier = opacity = 1f;
            trailAmount = 3f;
            fineLayer = detailLayer = accentLayer = trailLayer = true;
            RequestSettings(true);
        }
        public void SetDensity(float v) { density = Mathf.Clamp(v,.01f,1f); RequestSettings(true); }
        public void SetParticleCount(int v) { particleCount = Mathf.Clamp(v,16,MaximumParticles); RequestSettings(true); }
        public void SetTrailAmount(float value)
        {
            value = Mathf.Clamp(value,0f,5f);
            if (Mathf.Approximately(value,trailAmount)) return;
            trailAmount = value; RequestSettings(true);
        }
        public void SetTrailFadeDuration(float value) { trailFadeDuration = Mathf.Clamp(value,.2f,2f); }
        public void SetParticleSize(float v) { particleSizeMultiplier = Mathf.Clamp(v,.1f,4f); RequestSettings(true); }
        public void SetMovementSpeed(float v) { movementSpeedMultiplier = Mathf.Clamp(v,0f,4f); RequestSettings(true); }
        public void SetTurbulence(float v) { turbulenceMultiplier = Mathf.Clamp(v,0f,4f); RequestSettings(false); }
        public void SetWind(Vector3 direction, float strength)
        {
            SetWind(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,strength);
            SetWindEnabled(new Vector2(direction.x,direction.z).sqrMagnitude > .0001f);
        }
        /// <summary>World yaw, 0 = +Z, 90 = +X. Enable separately with SetWindEnabled.</summary>
        public void SetWind(float degrees, float strength)
        {
            degrees = Mathf.Repeat(degrees,360f); strength = Mathf.Clamp(strength,0f,1.5f);
            if (Mathf.Approximately(degrees,windAngle) && Mathf.Approximately(strength,windStrength)) return;
            windAngle = degrees; windStrength = strength; RequestSettings(false);
        }
        public void SetWindEnabled(bool value) { if (windEnabled == value) return; windEnabled = value; RequestSettings(false); }
        public void SetWindVisuals(bool value) { if (windVisuals == value) return; windVisuals = value; RequestSettings(false); }
        public void SetGlobalBrightness(float value) { globalBrightness = Mathf.Clamp(value,0f,8f); RequestSettings(false); }
        /// <summary>Call after changing profile layer settings from a script.</summary>
        public void RefreshSettings() => RequestSettings(true);
        public void SetOpacity(float v) { opacity = Mathf.Clamp01(v); RequestSettings(true); }
        public void SetSpawnRadius(float v) { spawnRadius = Mathf.Clamp(v,2f,32f); RequestSettings(true); }
        private void RequestSettings(bool reset)
        {
            dirty = true; repopulate |= reset;
            if (!Application.isPlaying && runtime[0] != null) ApplySettings();
        }
        internal void InitializeParticleSystem() { EnsureSystems(); ApplySettings(); }
        private void EnsureSystems()
        {
            // New non-serialized fields can be null on an existing scene component after
            // Unity hot-reloads the assembly without reconstructing that component.
            if (wakeMotion == null) wakeMotion = new List<Vector4>(4096);
            if (wakeTimes == null) wakeTimes = new List<Vector2>(4096);
            if (wakeProperties == null) wakeProperties = new MaterialPropertyBlock();
            for (var i = 0; i < runtime.Length; i++)
            {
                if (runtime[i] != null) continue;
                var child = new GameObject("Ambient Layer "+(i+1));
                child.transform.SetParent(transform,false);
                var system = child.AddComponent<ParticleSystem>();
                system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var renderer = child.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sortMode = ParticleSystemSortMode.None;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                runtime[i] = new LayerRuntime { system = system, renderer = renderer };
            }
            if (exclusion != null) return;
            var wakes = new GameObject("World-space wake motes");
            wakes.transform.SetParent(transform,false);
            var filter = wakes.AddComponent<MeshFilter>();
            wakeRenderer = wakes.AddComponent<MeshRenderer>();
            wakeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            wakeRenderer.receiveShadows = false;
            wakeRenderer.lightProbeUsage = LightProbeUsage.Off;
            wakeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            wakeMesh = new Mesh { name = "Ambient wake motes", indexFormat = IndexFormat.UInt16, hideFlags = HideFlags.DontSave };
            wakeMesh.MarkDynamic();
            filter.sharedMesh = wakeMesh;
            var sphere = new GameObject("Camera Exclusion Zone");
            sphere.transform.SetParent(transform,false);
            exclusion = sphere.AddComponent<SphereCollider>();
            exclusion.isTrigger = true;
        }
        private float LayerWeight(int index) => Mathf.Max(0f,Definition(index).budgetShare)*(index == 3 ? trailAmount : 1f);
        private void ApplySettings()
        {
            EnsureSystems(); dirty = false; appliedScale = WorldScale;
            var rebuildWakes = repopulate;
            appliedRevision = profile == null ? 0 : profile.Revision;
            var sum = 0f;
            for (var i = 0; i < LayerCount; i++) sum += LayerWeight(i);
            var totalBudget = Mathf.FloorToInt(particleCount*density);
            var windBudget = profile != null && windEnabled && windVisuals && windStrength > .001f
                ? Mathf.Min(Mathf.Clamp(windWispCount,0,12),totalBudget/20) : 0;
            var wakeBudget = LayerCount > 3 && trailLayer && sum > 0f
                ? Mathf.FloorToInt((totalBudget-windBudget)*LayerWeight(3)/sum) : 0;
            var residueBudget = Definition(3).shedResidue && wakeBudget >= 2
                ? Mathf.Min(wakeBudget-1,Mathf.FloorToInt(wakeBudget*Mathf.Clamp(Definition(3).residueBudgetShare,.5f,.9f))) : 0;
            for (var i = 0; i < runtime.Length; i++)
            {
                var layer = runtime[i];
                var wasEmpty = layer.budget == 0;
                // No redistribution on disable: a disabled layer really saves its whole share.
                layer.budget = i == 5 ? residueBudget : i == 4 ? windBudget : i == 3 ? wakeBudget-residueBudget : i < LayerCount && IsLayerEnabled(i) && sum > 0f
                    ? Mathf.FloorToInt((totalBudget-windBudget)*LayerWeight(i)/sum) : 0;
                layer.renderer.enabled = layer.budget > 0;
                if (layer.budget == 0)
                {
                    layer.retiring.Clear();
                    layer.system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var stoppedMain = layer.system.main; stoppedMain.maxParticles = 0;
                    continue;
                }
                if (layer.buffer == null || layer.buffer.Length < particleCount)
                    layer.buffer = new ParticleSystem.Particle[particleCount];
                ConfigureLayer(layer,Definition(i),i == 4);
                if (i == 5) ConfigureResidue(layer,Definition(3));
                if (repopulate || wasEmpty)
                {
                    layer.retiring.Clear();
                    layer.system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    layer.system.Play(false); placed = false; nextResidueEmission = 0f;
                }
            }
            repopulate = false;
            if (wakeRenderer != null)
            {
                wakeRenderer.enabled = runtime[3].budget > 0 && Definition(3).trails;
                if (wakeRenderer.enabled)
                    wakeRenderer.sharedMaterial = particleMaterial != null && particleMaterial.HasProperty(WakeModeId)
                        ? particleMaterial : Definition(3).material != null && Definition(3).material.HasProperty(WakeModeId)
                            ? Definition(3).material : FallbackMaterial();
                wakeProperties.SetFloat(BrightnessId,runtime[3].properties.GetFloat(BrightnessId)*1.8f);
                wakeProperties.SetVector(CameraFadeId,runtime[3].properties.GetVector(CameraFadeId));
                wakeProperties.SetFloat(WakeModeId,1f);
                wakeRenderer.SetPropertyBlock(wakeProperties);
                if (rebuildWakes) ClearWakes();
            }
        }
        private void ConfigureLayer(LayerRuntime layer, AmbientParticleLayer definition, bool isWind)
        {
            var system = layer.system; system.name = definition.name;
            var main = system.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = layer.budget;
            var lifetime = (isWind ? 4f : profile.Lifetime)*Mathf.Clamp(definition.lifetimeMultiplier,.1f,4f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime*.7f,lifetime*1.25f);
            main.startSpeed = isWind ? new ParticleSystem.MinMaxCurve(0f) : new ParticleSystem.MinMaxCurve(profile.MovementSpeed.x*movementSpeedMultiplier*definition.speedMultiplier*appliedScale,
                profile.MovementSpeed.y*movementSpeedMultiplier*definition.speedMultiplier*appliedScale);
            var size = profile.ParticleSize*(particleSizeMultiplier*definition.sizeMultiplier*appliedScale);
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x,size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
            var color = (isWind ? Color.white : profile.Color)*definition.tint; color.a *= opacity;
            var dim = color*(1f-definition.brightnessVariation); dim.a = color.a*.8f;
            main.startColor = new ParticleSystem.MinMaxGradient(dim,color);
            main.gravityModifier = profile.GravityModifier*appliedScale*movementSpeedMultiplier;
            var emission = system.emission; emission.enabled = true;
            emission.rateOverTime = layer.budget/Mathf.Max(.5f,lifetime*.975f);
            var velocity = system.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            var wind = (windEnabled ? WindDirection*windStrength : Vector3.zero)*(isWind ? 4f : 1f);
            // The style's original ambient drift survives with wind both on and off.
            if (!isWind) wind += profile.WindDirection.normalized*profile.WindStrength*movementSpeedMultiplier;
            wind *= appliedScale;
            // Unity requires the same curve mode on all three velocity axes.
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x,wind.x);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z,wind.z);
            velocity.y = new ParticleSystem.MinMaxCurve(wind.y+definition.verticalSpeed.x*appliedScale*movementSpeedMultiplier,
                wind.y+definition.verticalSpeed.y*appliedScale*movementSpeedMultiplier);
            var noise = system.noise;
            noise.enabled = profile.Turbulence*turbulenceMultiplier*definition.noiseMultiplier > .001f;
            noise.strength = profile.Turbulence*turbulenceMultiplier*definition.noiseMultiplier*appliedScale;
            noise.frequency = definition.noiseFrequency/appliedScale;
            noise.scrollSpeed = definition.noiseScroll*movementSpeedMultiplier;
            noise.damping = true; noise.quality = ParticleSystemNoiseQuality.Low;
            var rotation = system.rotationOverLifetime; rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(definition.angularSpeed.x*movementSpeedMultiplier,definition.angularSpeed.y*movementSpeedMultiplier);
            var colorOverLife = system.colorOverLifetime; colorOverLife.enabled = true; colorOverLife.color = fade;
            // Trails retire over time; killing their source here would erase the whole wake.
            var trigger = system.trigger; trigger.enabled = profile.MinimumCameraDistance > 0f && !definition.trails;
            if (trigger.enabled) { trigger.inside = ParticleSystemOverlapAction.Kill; trigger.SetCollider(0,exclusion); }
            exclusion.radius = profile.MinimumCameraDistance;
            layer.renderer.sharedMaterial = particleMaterial != null ? particleMaterial : definition.material != null ? definition.material : FallbackMaterial();
            layer.properties.SetFloat(BrightnessId,globalBrightness*(isWind ? 1f : profile.Brightness)*Mathf.Clamp(definition.brightness,0f,8f));
            layer.properties.SetFloat(TrailScaleId,appliedScale);
            var nearDistance = profile.MinimumCameraDistance*appliedScale;
            // Soften all layers before the exclusion zone, not only trail heads.
            layer.properties.SetVector(CameraFadeId,nearDistance > 0f
                ? new Vector4(nearDistance*.5f,nearDistance+appliedScale,1f,0f) : Vector4.zero);
            layer.renderer.SetPropertyBlock(layer.properties);
            var trails = system.trails;
            // Style wakes deposit small 3D motes; only wind wisps use native ribbons.
            trails.enabled = definition.trails && isWind;
            if (trails.enabled)
            {
                trails.mode = ParticleSystemTrailMode.PerParticle;
                trails.ratio = Mathf.Clamp01(definition.trailRatio);
                // Unity expresses this as a fraction of particle lifetime, not seconds.
                // Divide by the longest particle life to bound every trail by the authored seconds.
                trails.lifetime = Mathf.Clamp(definition.trailLifetime,.1f,isWind ? 3f : 5f)/(lifetime*1.25f);
                trails.minVertexDistance = Mathf.Clamp(definition.trailVertexDistance,.04f,.3f)*appliedScale;
                trails.worldSpace = true; trails.dieWithParticles = false;
                trails.sizeAffectsWidth = false; trails.sizeAffectsLifetime = false;
                trails.inheritParticleColor = true;
                trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(Mathf.Clamp(definition.trailWidth,.01f,.5f)*appliedScale,TrailWidth);
                var trailFade = CreateFade();
                var keys = trailFade.alphaKeys;
                for (var key = 0; key < keys.Length; key++) keys[key].alpha *= Mathf.Clamp01(definition.trailOpacity);
                trailFade.alphaKeys = keys;
                trails.colorOverLifetime = trailFade;
                layer.renderer.trailMaterial = definition.trailMaterial != null ? definition.trailMaterial : FallbackTrailMaterial();
            }
        }
        private void ConfigureResidue(LayerRuntime layer, AmbientParticleLayer definition)
        {
            // Unity can restore this MonoBehaviour across a domain reload without its
            // non-serialized scratch array; recreate it before configuring the module.
            if (residueColors == null || residueColors.Length != 3) residueColors = new GradientColorKey[3];
            if (residueFade == null) residueFade = new Gradient();
            var system = layer.system; system.name = definition.name+" — detached wake";
            var main = system.main;
            var lifetime = Mathf.Clamp(definition.residueLifetime,.2f,3f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime*.65f,lifetime);
            main.startSpeed = 0f; main.gravityModifier = 0f;
            var emission = system.emission; emission.enabled = false;
            var trails = system.trails; trails.enabled = false;
            var velocity = system.velocityOverLifetime;
            var wind = (windEnabled ? WindDirection*windStrength : Vector3.zero)*appliedScale;
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x,wind.x);
            velocity.y = new ParticleSystem.MinMaxCurve(0f,0f);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z,wind.z);
            var noise = system.noise;
            noise.strength = profile.Turbulence*turbulenceMultiplier*.4f*appliedScale;
            var size = system.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f,ResidueSize);
            residueColors[0] = new GradientColorKey(Color.white,0f);
            residueColors[1] = new GradientColorKey(Color.Lerp(Color.white,definition.residueEndTint,.35f),.45f);
            residueColors[2] = new GradientColorKey(definition.residueEndTint,1f);
            residueFade.SetKeys(residueColors,ResidueAlphas);
            var color = system.colorOverLifetime; color.color = residueFade;
        }

        private void ClearWakes()
        {
            wakeTracks.Clear();
            if (wakeMesh != null) wakeMesh.Clear();
            nextWakeUpdate = 0f;
        }

        // Each head deposits independent soft billboards at 3D world-space positions.
        // The shader moves and fades them every rendered frame; the CPU only refreshes
        // the bounded mesh of birth data at its capped sampling frequency.
        private void UpdateWakes(float now)
        {
            if (wakeRenderer == null || !wakeRenderer.enabled) return;
            wakeProperties.SetFloat(WakeTimeId,now);
            wakeRenderer.SetPropertyBlock(wakeProperties);
            if (now < nextWakeUpdate) return;
            nextWakeUpdate = now+WakeMeshRefreshSeconds;
            var definition = Definition(3);
            var lifetime = Mathf.Clamp(definition.trailLifetime,.1f,5f);
            var vertexDistance = Mathf.Clamp(definition.trailVertexDistance,.04f,.3f)*appliedScale;
            var source = runtime[3];
            // Keep every spatial sample. At high populations, reduce motes per sample
            // instead of skipping whole sample intervals (which made some tracks empty).
            // Allow head turnover while reserving the same bounded mesh budget.
            var plannedTracks = Mathf.Max(1,Mathf.CeilToInt(source.budget*WakeTrackTurnoverReserve));
            var plannedSamplesPerTrack = Mathf.CeilToInt(lifetime/WakeBudgetSampleSeconds)+1;
            var motesPerSample = Mathf.Min(Mathf.Clamp(definition.trailMotesPerSample,1,4),
                Mathf.Max(1,MaximumWakeMotes/(plannedTracks*plannedSamplesPerTrack)));
            var count = source.system.GetParticles(source.buffer);
            foreach (var track in wakeTracks.Values) track.seen = false;
            for (var i = 0; i < count; i++)
            {
                var particle = source.buffer[i];
                if (particle.remainingLifetime <= 0f) continue;
                if (particle.randomSeed%1000u >= Mathf.RoundToInt(Mathf.Clamp01(definition.trailRatio)*1000f)) continue;
                if (!wakeTracks.TryGetValue(particle.randomSeed,out var track))
                {
                    track = new WakeTrack { motesPerSample = motesPerSample };
                    wakeTracks.Add(particle.randomSeed,track);
                }
                track.seen = true;
                track.color = particle.startColor;
                var points = track.points;
                // A recycled random seed or externally teleported particle must not
                // scatter motes between two unrelated volumes.
                if (points.Count > 0 && (particle.position-points[points.Count-1].position).sqrMagnitude > 4f*appliedScale*appliedScale)
                    points.Clear();
                if (points.Count == 0 || (particle.position-points[points.Count-1].position).sqrMagnitude > vertexDistance*vertexDistance
                    || now-points[points.Count-1].time >= WakeMaximumQuietSeconds)
                    points.Add(new WakePoint { position = particle.position, time = now, id = ++wakePointSequence });
            }
            wakeVertices.Clear(); wakeUvs.Clear(); wakeMotion.Clear(); wakeTimes.Clear();
            wakeColors.Clear(); wakeTriangles.Clear();
            expiredWakeSeeds.Clear();
            var worldToLocal = wakeRenderer.transform.worldToLocalMatrix;
            var naturalDrift = profile.WindDirection.normalized*profile.WindStrength*movementSpeedMultiplier;
            var directionalDrift = windEnabled ? WindDirection*windStrength : Vector3.zero;
            var moteCount = 0;
            var trackQuota = Mathf.Max(1,MaximumWakeMotes/Mathf.Max(plannedTracks,wakeTracks.Count));
            foreach (var pair in wakeTracks)
            {
                var track = pair.Value;
                var points = track.points;
                var expired = 0;
                while (expired < points.Count && now-points[expired].time >= lifetime) expired++;
                if (expired > 0) points.RemoveRange(0,expired);
                if (points.Count == 0)
                {
                    if (!track.seen) expiredWakeSeeds.Add(pair.Key);
                    continue;
                }
                var trackMotes = 0;
                // The newest/brightest motes win if an extreme motion setting reaches
                // the quota; older points are already partway through their fade.
                for (var p = points.Count-1; p >= 0 && trackMotes < trackQuota; p--)
                {
                    var point = points[p];
                    if (moteCount >= MaximumWakeMotes) break;
                    var age = Mathf.Clamp01((now-points[p].time)/lifetime);
                    var spread = Mathf.Clamp(definition.trailWidth,.01f,1.5f)*appliedScale;
                    for (var mote = 0; mote < track.motesPerSample && moteCount < MaximumWakeMotes && trackMotes < trackQuota; mote++)
                    {
                        var seed = pair.Key ^ point.id*747796405u ^ (uint)(mote+1)*2891336453u;
                        var offset = new Vector3(SignedNoise(seed),SignedNoise(seed+1u),SignedNoise(seed+2u));
                        if (offset.sqrMagnitude < .001f) offset = Vector3.up;
                        offset.Normalize();
                        var radial = .2f+.8f*Mathf.Abs(SignedNoise(seed+3u));
                        var center = point.position+offset*(spread*radial*.025f);
                        var velocity = (naturalDrift*.12f+directionalDrift*.25f)*appliedScale
                            +offset*(spread*radial*.475f/lifetime);
                        var size = Mathf.Clamp(profile.ParticleSize.x*definition.sizeMultiplier*particleSizeMultiplier*appliedScale
                            *(.16f+.24f*Mathf.Abs(SignedNoise(seed+4u))),.004f*appliedScale,.12f*appliedScale);
                        var color = Color.Lerp((Color)track.color,definition.residueEndTint,age*.45f);
                        color.a = ((Color)track.color).a*Mathf.Clamp01(definition.trailOpacity)
                            *(.5f+.5f*Mathf.Abs(SignedNoise(seed+5u)));
                        var vertexColor = (Color32)color;
                        var first = wakeVertices.Count;
                        var localCenter = worldToLocal.MultiplyPoint3x4(center);
                        var motion = new Vector4(velocity.x,velocity.y,velocity.z,size);
                        var times = new Vector2(point.time,lifetime);
                        for (var v = 0; v < MoteVertices; v++)
                        {
                            wakeVertices.Add(localCenter);
                            wakeUvs.Add(MoteCorners[v]);
                            wakeMotion.Add(motion);
                            wakeTimes.Add(times);
                            wakeColors.Add(vertexColor);
                        }
                        for (var face = 0; face < MoteFaces.Length; face++) wakeTriangles.Add(first+MoteFaces[face]);
                        moteCount++;
                        trackMotes++;
                    }
                }
            }
            for (var i = 0; i < expiredWakeSeeds.Count; i++) wakeTracks.Remove(expiredWakeSeeds[i]);
            wakeMesh.Clear(false);
            wakeMesh.SetVertices(wakeVertices);
            wakeMesh.SetColors(wakeColors);
            wakeMesh.SetUVs(0,wakeUvs);
            wakeMesh.SetUVs(1,wakeMotion);
            wakeMesh.SetUVs(2,wakeTimes);
            wakeMesh.SetTriangles(wakeTriangles,0,false);
            wakeMesh.RecalculateBounds();
            var bounds = wakeMesh.bounds;
            var drift = (naturalDrift*.12f+directionalDrift*.25f).magnitude*appliedScale*lifetime;
            var scale = wakeRenderer.transform.lossyScale;
            var smallestScale = Mathf.Max(.01f,Mathf.Min(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            var maxSpread = Mathf.Clamp(definition.trailWidth,.01f,1.5f)*appliedScale*.5f;
            bounds.Expand((maxSpread+drift+.25f*appliedScale)*2f/smallestScale);
            wakeMesh.bounds = bounds;
        }

        private void EmitResidue(float now)
        {
            var wake = runtime[5];
            if (wake.budget == 0 || now < nextResidueEmission) return;
            var source = runtime[3];
            var count = source.system.GetParticles(source.buffer);
            var definition = Definition(3);
            // Reserve enough lifetime slots per head; no catch-up bursts after a stalled frame.
            nextResidueEmission = now+Mathf.Max(1f/Mathf.Clamp(definition.residueRate,1f,12f),
                count*Mathf.Clamp(definition.residueLifetime,.2f,3f)*.85f/Mathf.Max(1,wake.budget));
            var available = wake.budget-wake.system.particleCount;
            for (var n = 0; n < count && available > 0; n++)
            {
                var index = (residueParentCursor+n)%count;
                var particle = source.buffer[index];
                if (particle.remainingLifetime <= 0f || source.retiring.ContainsKey(particle.randomSeed)) continue;
                var seed = particle.randomSeed+(++residueSequence)*747796405u;
                var jitter = new Vector3(SignedNoise(seed),SignedNoise(seed+1),SignedNoise(seed+2));
                var color = (Color)particle.startColor; color.a *= Mathf.Clamp01(definition.residueOpacity);
                var emit = new ParticleSystem.EmitParams {
                    position = particle.position-particle.totalVelocity*.06f+jitter*(Mathf.Clamp(definition.residueSpread,.01f,1.2f)*appliedScale),
                    velocity = jitter*(Mathf.Clamp(definition.residueDrift,0f,.5f)*appliedScale*movementSpeedMultiplier),
                    startSize = particle.startSize*Mathf.Clamp(definition.residueSize,.1f,3.5f)*(.9f+.45f*SignedNoise(seed+3)),
                    startColor = color, randomSeed = seed == 0 ? 1u : seed,
                    applyShapeToPosition = false
                };
                wake.system.Emit(emit,1); available--;
            }
            residueParentCursor++;
        }
        private static float SignedNoise(uint seed)
        {
            seed ^= seed >> 16; seed *= 2246822519u; seed ^= seed >> 13;
            return (seed & 65535u)/32767.5f-1f;
        }
        private static void QueueRetirement(LayerRuntime layer, ParticleSystem.Particle particle, float now)
        {
            if (!layer.retiring.ContainsKey(particle.randomSeed))
                layer.retiring.Add(particle.randomSeed,new Retirement { started = now, originalColor = particle.startColor });
        }
        private void UpdateTrailRetirements(Vector3 observer, float now)
        {
            if (now < nextRetirementUpdate) return;
            nextRetirementUpdate = now+.05f;
            var radiusSquared = profile.MinimumCameraDistance*appliedScale;
            radiusSquared *= radiusSquared;
            // Only the few trail heads; keep native lifetime and slot history unchanged.
            for (var layerIndex = 3; layerIndex <= 4; layerIndex++)
            {
                var layer = runtime[layerIndex];
                if (layer.budget == 0) continue;
                var count = layer.system.GetParticles(layer.buffer);
                var changed = false;
                layer.liveSeeds.Clear();
                for (var i = 0; i < count; i++)
                {
                    var particle = layer.buffer[i];
                    layer.liveSeeds.Add(particle.randomSeed);
                    if (radiusSquared > 0f && (particle.position-observer).sqrMagnitude < radiusSquared)
                        QueueRetirement(layer,particle,now);
                    if (!layer.retiring.TryGetValue(particle.randomSeed,out var retirement)) continue;
                    var progress = Mathf.Clamp01((now-retirement.started)/trailFadeDuration);
                    var color = retirement.originalColor;
                    color.a = (byte)Mathf.RoundToInt(color.a*(1f-Mathf.SmoothStep(0f,1f,progress)));
                    particle.startColor = color;
                    if (progress >= 1f) particle.remainingLifetime = -1f;
                    layer.buffer[i] = particle;
                    changed = true;
                }
                if (changed) layer.system.SetParticles(layer.buffer,count);
                layer.staleSeeds.Clear();
                foreach (var item in layer.retiring)
                    if (!layer.liveSeeds.Contains(item.Key)) layer.staleSeeds.Add(item.Key);
                for (var i = 0; i < layer.staleSeeds.Count; i++) layer.retiring.Remove(layer.staleSeeds[i]);
            }
        }
        private void ConfigureVolumes(Vector3 position, Vector3 forward)
        {
            var radius = spawnRadius*appliedScale;
            var center = position+forward*(radius*forwardBias)+Vector3.up*((profile.VerticalRange.x+profile.VerticalRange.y)*.5f*appliedScale);
            for (var i = 0; i < runtime.Length; i++)
            {
                var system = runtime[i].system;
                system.transform.SetPositionAndRotation(center,Quaternion.LookRotation(forward,Vector3.up));
                var inherited = system.transform.lossyScale;
                var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
                shape.randomDirectionAmount = 1f; shape.position = Vector3.zero; shape.rotation = Vector3.zero;
                shape.scale = new Vector3(radius*1.7f/Mathf.Max(.001f,Mathf.Abs(inherited.x)),
                    (profile.VerticalRange.y-profile.VerticalRange.x)*appliedScale/Mathf.Max(.001f,Mathf.Abs(inherited.y)),
                    radius*2.2f/Mathf.Max(.001f,Mathf.Abs(inherited.z)));
            }
        }
        private void Refill(LayerRuntime layer, Vector3 position, Vector3 forward, bool initial, float now)
        {
            var total = layer.system.GetParticles(layer.buffer);
            var radius = spawnRadius*appliedScale;
            var right = Vector3.Cross(Vector3.up,forward);
            var kept = 0;
            var hasTrails = layer.system.trails.enabled || layer == runtime[3] && Definition(3).trails;
            for (var i = 0; i < total; i++)
            {
                var delta = layer.buffer[i].position-position;
                var z = Vector3.Dot(delta,forward);
                if (layer.buffer[i].remainingLifetime > 0f && Mathf.Abs(Vector3.Dot(delta,right)) <= radius*retainedVolumePadding
                    && z >= -radius*(.7f+retainedVolumePadding*.2f) && z <= radius*(1.5f+retainedVolumePadding*.2f)
                    && delta.y >= (profile.VerticalRange.x-1f)*appliedScale && delta.y <= (profile.VerticalRange.y+1f)*appliedScale)
                {
                    // Native trail history belongs to a particle slot: C# compaction would
                    // reconnect one particle's trail to another particle across the volume.
                    if (!hasTrails) layer.buffer[kept] = layer.buffer[i];
                    kept++;
                }
                else if (hasTrails && layer.buffer[i].remainingLifetime > 0f)
                {
                    QueueRetirement(layer,layer.buffer[i],now);
                    kept++; // Keep the native slot until its alpha has faded.
                }
            }
            // Compact before emitting, so dead particles cannot occupy the hard cap.
            // Preserve native trail history when maintenance has nothing to recycle.
            if (kept != total) layer.system.SetParticles(layer.buffer,hasTrails ? total : kept);
            // Let Unity kill/compact trail slots together with their history on its next step.
            if (hasTrails && kept != total) return;
            layer.system.Emit(Mathf.Max(0,layer.budget-kept));
            if (!initial) return;
            total = layer.system.GetParticles(layer.buffer);
            for (var i = 0; i < total; i++)
            {
                // Randomized initial ages avoid synchronized expiration a lifetime later.
                var fraction = .18f+(layer.buffer[i].randomSeed%1000)*.0006f;
                layer.buffer[i].remainingLifetime = layer.buffer[i].startLifetime*fraction;
            }
            layer.system.SetParticles(layer.buffer,total);
        }
        private Transform ResolveTarget()
        {
            if (followTarget != null) return followTarget;
            if (!useMainCameraWhenTargetMissing) return null;
            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
            return cachedCamera == null ? null : cachedCamera.transform;
        }
        private Material FallbackMaterial()
        {
            if (fallbackMaterial != null) return fallbackMaterial;
            var shader = Shader.Find("Obsidian Forge/Ambient Particle") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            fallbackMaterial = new Material(shader) { name = "Ambient fallback", hideFlags = HideFlags.DontSave };
            return fallbackMaterial;
        }
        private Material FallbackTrailMaterial()
        {
            if (fallbackTrailMaterial == null)
                fallbackTrailMaterial = new Material(Shader.Find("Obsidian Forge/Ambient Trail")) { hideFlags = HideFlags.HideAndDontSave };
            return fallbackTrailMaterial;
        }
        private static Vector3 PlanarForward(Transform target)
        { var f = Vector3.ProjectOnPlane(target.forward,Vector3.up); return f.sqrMagnitude < .001f ? Vector3.forward : f.normalized; }
        private static Gradient CreateFade()
        {
            var result = new Gradient();
            result.SetKeys(new[] { new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f) },
                new[] { new GradientAlphaKey(0f,0f),new GradientAlphaKey(.1f,.02f),
                    new GradientAlphaKey(.5f,.05f),new GradientAlphaKey(1f,.09f),
                    new GradientAlphaKey(.85f,.7f),new GradientAlphaKey(0f,1f) });
            return result;
        }
    }
}
