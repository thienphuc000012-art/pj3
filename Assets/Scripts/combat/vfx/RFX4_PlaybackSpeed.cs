using UnityEngine;

public class RFX4_PlaybackSpeed : MonoBehaviour
{
    [Header("Global VFX Playback Speed")]
    [Min(0.01f)]
    public float playbackSpeed = 1f;

    [Header("Optional")]
    public bool speedUpLightCurves = true;
    public bool speedUpFadeout = true;
    public bool speedUpAudio = false;

    private ParticleSystem[] particleSystems;
    private RFX4_LightCurves[] lightCurves;
    private AudioSource[] audioSources;
    private RFX4_EffectSettings effectSettings;

    private float[] originalParticleSpeeds;
    private float[] originalLightTimes;
    private float[] originalAudioPitch;

    private float originalFadeoutTime;

    private bool initialized = false;

    private void Awake()
    {
        CacheOriginalValues();
        ApplyPlaybackSpeed();
    }

    private void OnEnable()
    {
        if (!initialized)
            CacheOriginalValues();

        ApplyPlaybackSpeed();
    }

    private void CacheOriginalValues()
    {
        // ==========================================
        // Particle Systems
        // ==========================================
        particleSystems =
            GetComponentsInChildren<ParticleSystem>(true);

        originalParticleSpeeds =
            new float[particleSystems.Length];

        for (int i = 0; i < particleSystems.Length; i++)
        {
            var main = particleSystems[i].main;

            originalParticleSpeeds[i] =
                main.simulationSpeed;
        }

        // ==========================================
        // Light Curves
        // ==========================================
        lightCurves =
            GetComponentsInChildren<RFX4_LightCurves>(true);

        originalLightTimes =
            new float[lightCurves.Length];

        for (int i = 0; i < lightCurves.Length; i++)
        {
            originalLightTimes[i] =
                lightCurves[i].GraphTimeMultiplier;
        }

        // ==========================================
        // Audio
        // ==========================================
        audioSources =
            GetComponentsInChildren<AudioSource>(true);

        originalAudioPitch =
            new float[audioSources.Length];

        for (int i = 0; i < audioSources.Length; i++)
        {
            originalAudioPitch[i] =
                audioSources[i].pitch;
        }

        // ==========================================
        // RFX4 Effect Settings
        // ==========================================
        effectSettings =
            GetComponent<RFX4_EffectSettings>();

        if (effectSettings != null)
        {
            originalFadeoutTime =
                effectSettings.FadeoutTime;
        }

        initialized = true;
    }

    public void ApplyPlaybackSpeed()
    {
        if (!initialized)
            CacheOriginalValues();

        float speed = Mathf.Max(0.01f, playbackSpeed);

        // ==========================================
        // PARTICLES
        // ==========================================
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
                continue;

            var main = particleSystems[i].main;

            main.simulationSpeed =
                originalParticleSpeeds[i] * speed;
        }

        // ==========================================
        // LIGHT CURVES
        // ==========================================
        if (speedUpLightCurves)
        {
            for (int i = 0; i < lightCurves.Length; i++)
            {
                if (lightCurves[i] == null)
                    continue;

                lightCurves[i].GraphTimeMultiplier =
                    originalLightTimes[i] / speed;
            }
        }

        // ==========================================
        // FADE OUT
        // ==========================================
        if (speedUpFadeout &&
            effectSettings != null)
        {
            effectSettings.FadeoutTime =
                originalFadeoutTime / speed;
        }

        // ==========================================
        // AUDIO
        // ==========================================
        if (speedUpAudio)
        {
            for (int i = 0; i < audioSources.Length; i++)
            {
                if (audioSources[i] == null)
                    continue;

                audioSources[i].pitch =
                    originalAudioPitch[i] * speed;
            }
        }
    }

    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = Mathf.Max(0.01f, speed);
        ApplyPlaybackSpeed();
    }
}