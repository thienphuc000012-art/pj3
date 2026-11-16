using UnityEngine;

/// <summary>Routes existing prefab/Timeline AudioSources through Options without replacing their playback.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
public sealed class GameAudioSourceBus : MonoBehaviour
{
    public GameAudioBus bus = GameAudioBus.SpecialEffects;
    AudioSource source;
    float authoredVolume, appliedVolume;
    bool initialized;
    void Awake() { Initialize(); Apply(); }
    void OnEnable() { GameAudio.VolumesChanged += Apply; Apply(); }
    public static void Bind(GameObject root)
    {
        if (root == null || !Application.isPlaying) return;
        foreach (var audio in root.GetComponentsInChildren<AudioSource>(true))
        {
            var route = audio.GetComponent<GameAudioSourceBus>();
            if (route == null) route = audio.gameObject.AddComponent<GameAudioSourceBus>();
            route.Apply();
        }
    }
    void Initialize()
    {
        if (initialized) return; initialized = true;
        source = GetComponent<AudioSource>(); authoredVolume = source.volume; appliedVolume = source.volume;
    }
    void LateUpdate() => Apply();
    void Apply()
    {
        Initialize();
        // Timeline or an effect may animate its own gain; retain that authored value.
        if (!Mathf.Approximately(source.volume, appliedVolume)) authoredVolume = source.volume;
        appliedVolume = authoredVolume * GameAudio.Volume(GameAudioBus.Master) * (bus == GameAudioBus.Master ? 1f : GameAudio.Volume(bus));
        source.volume = appliedVolume;
    }
    void OnDisable()
    {
        GameAudio.VolumesChanged -= Apply;
        if (initialized && source != null) { source.volume = authoredVolume; appliedVolume = authoredVolume; }
    }
}
