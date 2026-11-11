using System.Collections.Generic;
using UnityEngine;

namespace ObsidianForge.AmbientParticlesEssentials.Demo
{
    /// <summary>Lightweight in-game controls used only by the shipping demo scene.</summary>
    [DisallowMultipleComponent]
    public sealed class AmbientParticlesDemo : MonoBehaviour
    {
        [SerializeField] private AmbientBiomeParticles ambientParticles;
        [SerializeField] private List<AmbientParticleProfile> profiles = new List<AmbientParticleProfile>();
        [SerializeField] private bool showUi = true;

        private int profileIndex;
        private float density;
        private int particleCount;
        private float trailAmount;
        private float particleSize;
        private float movementSpeed;
        private float turbulence;
        private float windStrength;
        private float windAngle;
        private float brightness;
        private float opacity;
        private float spawnRadius;
        private Vector2 scroll;

        private void Awake()
        {
            if (ambientParticles == null)
                ambientParticles = GetComponent<AmbientBiomeParticles>();

            if (ambientParticles != null && ambientParticles.Profile != null && !profiles.Contains(ambientParticles.Profile))
                profiles.Add(ambientParticles.Profile);

            profileIndex = Mathf.Max(0, profiles.IndexOf(ambientParticles == null ? null : ambientParticles.Profile));
            SyncFromEffect();
        }

        private void OnGUI()
        {
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.F1)
            {
                showUi = !showUi;
                currentEvent.Use();
            }

            if (!showUi || ambientParticles == null || ambientParticles.Profile == null)
                return;

            // The component is the authority, including edits made in the Inspector during Play.
            SyncFromEffect();

            const float width = 340f;
            GUILayout.BeginArea(new Rect(18f, 18f, width, Mathf.Min(760f, Screen.height - 36f)), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("AMBIENT PARTICLES ESSENTIALS", GUI.skin.label);
            GUILayout.Label("WASD: move · Q/E: turn · F1: UI");
            GUILayout.Space(8f);

            DrawProfileSelector();
            GUILayout.Space(8f);
            GUILayout.Label("Current Effect: " + ambientParticles.Profile.DisplayName);
            GUILayout.Label(ambientParticles.Profile.Description, GUI.skin.label);
            GUILayout.Space(10f);

            GUILayout.Label("Layers — shared particle budget");
            for (var i = 0; i < ambientParticles.LayerCount; i++)
            {
                var enabled = ambientParticles.IsLayerEnabled(i);
                var next = GUILayout.Toggle(enabled, ambientParticles.GetLayerName(i));
                if (next != enabled) ambientParticles.SetLayerEnabled(i,next);
            }

            brightness = DrawSlider("Global Brightness", brightness, 0f, 8f);
            density = DrawSlider("Density", density, 0.01f, 1f);
            particleCount = DrawIntSlider("Total Particle Budget", particleCount, 16, AmbientBiomeParticles.MaximumParticles);
            trailAmount = DrawSlider("Trail Amount (x)", trailAmount, 0f, 5f);
            particleSize = DrawSlider("Particle Size", particleSize, 0.1f, 4f);
            movementSpeed = DrawSlider("Movement Speed", movementSpeed, 0f, 4f);
            turbulence = DrawSlider("Turbulence", turbulence, 0f, 4f);
            opacity = DrawSlider("Opacity", opacity, 0f, 1f);
            spawnRadius = DrawSlider("Spawn Radius", spawnRadius, 2f, 32f);
            GUILayout.Space(8f);
            var wind = GUILayout.Toggle(ambientParticles.WindEnabled,"Directional wind (adds to natural drift)");
            if (wind != ambientParticles.WindEnabled) ambientParticles.SetWindEnabled(wind);
            var wisps = GUILayout.Toggle(ambientParticles.WindVisuals,"Wind wisps");
            if (wisps != ambientParticles.WindVisuals) ambientParticles.SetWindVisuals(wisps);
            windAngle = DrawSlider("Wind Direction (degrees)", windAngle, 0f, 360f);
            windStrength = DrawSlider("Wind Strength", windStrength, 0f, 1.5f);
            if (ambientParticles.WindSource != null) GUILayout.Label("Direction / strength are driven by the WindZone.");

            ApplyControls();
            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset"))
            {
                ambientParticles.ResetToProfile();
                SyncFromEffect();
            }

            if (GUILayout.Button("Hide UI (F1)"))
                showUi = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public void SetProfiles(IList<AmbientParticleProfile> sourceProfiles)
        {
            profiles.Clear();
            if (sourceProfiles != null)
                profiles.AddRange(sourceProfiles);
            profileIndex = Mathf.Max(0, profiles.IndexOf(ambientParticles == null ? null : ambientParticles.Profile));
        }

        private void DrawProfileSelector()
        {
            GUILayout.Label("Effect");
            GUILayout.BeginHorizontal();
            for (var index = 0; index < profiles.Count; index++)
            {
                var candidate = profiles[index];
                if (candidate == null)
                    continue;

                if (GUILayout.Toggle(profileIndex == index, candidate.DisplayName, "Button"))
                {
                    if (profileIndex != index)
                    {
                        profileIndex = index;
                        ambientParticles.SetProfile(candidate);
                        SyncFromEffect();
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        private void ApplyControls()
        {
            if (!Mathf.Approximately(brightness,ambientParticles.GlobalBrightness))
                ambientParticles.SetGlobalBrightness(brightness);
            if (!Mathf.Approximately(density, ambientParticles.Density))
                ambientParticles.SetDensity(density);
            if (particleCount != ambientParticles.ParticleCount)
                ambientParticles.SetParticleCount(particleCount);
            if (!Mathf.Approximately(trailAmount, ambientParticles.TrailAmount))
                ambientParticles.SetTrailAmount(trailAmount);
            if (!Mathf.Approximately(particleSize, ambientParticles.ParticleSizeMultiplier))
                ambientParticles.SetParticleSize(particleSize);
            if (!Mathf.Approximately(movementSpeed, ambientParticles.MovementSpeedMultiplier))
                ambientParticles.SetMovementSpeed(movementSpeed);
            if (!Mathf.Approximately(turbulence, ambientParticles.TurbulenceMultiplier))
                ambientParticles.SetTurbulence(turbulence);
            if (!Mathf.Approximately(windStrength, ambientParticles.WindStrength) || !Mathf.Approximately(windAngle,ambientParticles.WindAngle))
                ambientParticles.SetWind(windAngle, windStrength);
            if (!Mathf.Approximately(opacity, ambientParticles.OpacityMultiplier))
                ambientParticles.SetOpacity(opacity);
            if (!Mathf.Approximately(spawnRadius, ambientParticles.SpawnRadius))
                ambientParticles.SetSpawnRadius(spawnRadius);
        }

        private void SyncFromEffect()
        {
            if (ambientParticles == null || ambientParticles.Profile == null)
                return;

            density = ambientParticles.Density;
            profileIndex = Mathf.Max(0,profiles.IndexOf(ambientParticles.Profile));
            particleCount = ambientParticles.ParticleCount;
            trailAmount = ambientParticles.TrailAmount;
            particleSize = ambientParticles.ParticleSizeMultiplier;
            movementSpeed = ambientParticles.MovementSpeedMultiplier;
            turbulence = ambientParticles.TurbulenceMultiplier;
            windStrength = ambientParticles.WindStrength;
            windAngle = ambientParticles.WindAngle;
            brightness = ambientParticles.GlobalBrightness;
            opacity = ambientParticles.OpacityMultiplier;
            spawnRadius = ambientParticles.SpawnRadius;
        }

        private static float DrawSlider(string label, float value, float min, float max)
        {
            GUILayout.Label($"{label}: {value:0.00}");
            return GUILayout.HorizontalSlider(value, min, max);
        }

        private static int DrawIntSlider(string label, int value, int min, int max)
        {
            GUILayout.Label($"{label}: {value}");
            return Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
        }
    }
}
