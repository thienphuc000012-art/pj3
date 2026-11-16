using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One persistent audio owner. Gains are applied per bus, never through AudioListener.volume.</summary>
public sealed class GameAudio : MonoBehaviour
{
    sealed class Voice
    {
        public AudioSource source;
        public GameAudioBus bus;
        public float gain, started;
        public bool sceneBound;
    }
    static GameAudio instance;
    static AudioClip fallbackClick;
    static GameAudioSettings cachedSettings;
    public static event Action VolumesChanged;
    public static GameAudioSettings Settings
    {
        get
        {
            if (instance != null) return instance.settings;
            if (cachedSettings == null) cachedSettings = Resources.Load<GameAudioSettings>("Adventure/Audio/GameAudioSettings");
            if (cachedSettings == null) cachedSettings = ScriptableObject.CreateInstance<GameAudioSettings>();
            return cachedSettings;
        }
    }
    public static GameAudio Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
            {
                var host = new GameObject("Game Audio");
                instance = host.AddComponent<GameAudio>();
            }
            return instance;
        }
    }
    GameAudioSettings settings;
    readonly float[] volumes = new float[5];
    readonly List<Voice> voices = new List<Voice>();
    readonly HashSet<AudioSource> managed = new HashSet<AudioSource>();
    readonly List<WorldInteraction> areas = new List<WorldInteraction>();
    AudioSource musicA, musicB;
    AudioSource incoming, outgoing;
    AudioClip desiredClip;
    float desiredGain, fadeAge, incomingStart, outgoingStart, incomingGain, outgoingGain;
    float nextDiscovery, nextBindings;
    CombatManager battleOwner;
    BattleEncounterSetup encounterSetup;
    AudioListener listener;
    Camera listenerCamera;
    bool preferencesDirty;
    // Disabled only by isolated Edit Mode validation; production always persists user choices.
    bool persistPreferences = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; fallbackClick = null; cachedSettings = null; VolumesChanged = null; WorldInteraction.All.Clear(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() { _ = Instance; }
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
        settings = Resources.Load<GameAudioSettings>("Adventure/Audio/GameAudioSettings");
        if (settings == null) settings = ScriptableObject.CreateInstance<GameAudioSettings>();
        for (int i = 0; i < volumes.Length; i++) volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(PreferenceKey((GameAudioBus)i), settings.DefaultVolume((GameAudioBus)i)));
        musicA = NewSource("Music A"); musicB = NewSource("Music B");
        musicA.loop = musicB.loop = true;
        incoming = musicA; outgoing = musicB;
        var listenerObject = new GameObject("Audio Listener"); listenerObject.transform.SetParent(transform, false);
        listener = listenerObject.AddComponent<AudioListener>();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDestroy()
    {
        if (instance != this) return;
        SavePreferences();
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }
    void OnApplicationPause(bool paused) { if (paused) SavePreferences(); }
    void OnApplicationQuit() => SavePreferences();
    static string PreferenceKey(GameAudioBus bus) => "Adventure.Audio." + bus;
    public static float Volume(GameAudioBus bus) => Instance != null ? Instance.volumes[(int)bus] : 1f;
    public static void SetVolume(GameAudioBus bus, float value)
    {
        var audio = Instance; if (audio == null) return;
        value = float.IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        if (Mathf.Approximately(audio.volumes[(int)bus], value)) return;
        audio.volumes[(int)bus] = value;
        if (audio.persistPreferences) { PlayerPrefs.SetFloat(PreferenceKey(bus), value); audio.preferencesDirty = true; }
        audio.ApplyVolumes(); VolumesChanged?.Invoke();
    }
    public static void FlushPreferences() { if (instance != null) instance.SavePreferences(); }
    void SavePreferences() { if (!preferencesDirty) return; PlayerPrefs.Save(); preferencesDirty = false; }
    float Gain(GameAudioBus bus) => volumes[0] * (bus == GameAudioBus.Master ? 1f : volumes[(int)bus]);
    AudioSource NewSource(string label)
    {
        var host = new GameObject(label); host.transform.SetParent(transform, false);
        var source = host.AddComponent<AudioSource>(); source.playOnAwake = false;
        source.ignoreListenerPause = true; managed.Add(source); return source;
    }
    public static void Play(GameSound sound, Vector3 position, GameAudioBus bus = GameAudioBus.SpecialEffects)
    {
        if (sound == null || !sound.HasClip || Instance == null) return;
        Instance.PlayClip(sound.Pick(), position, sound.volume, bus, sound.spatialBlend,
            UnityEngine.Random.Range(Mathf.Max(.1f, sound.pitch.x), Mathf.Max(.1f, sound.pitch.y)), sound.maxDistance);
    }
    public static void PlayClipAt(AudioClip clip, Vector3 position, float gain = 1f,
        GameAudioBus bus = GameAudioBus.SpecialEffects, float spatialBlend = 1f)
    { if (clip != null && Instance != null) Instance.PlayClip(clip, position, gain, bus, spatialBlend, 1f, 24f); }
    void PlayClip(AudioClip clip, Vector3 position, float gain, GameAudioBus bus, float blend, float pitch, float distance)
    {
        Voice voice = voices.Find(v => !v.source.isPlaying);
        if (voice == null && voices.Count < Mathf.Max(8, settings.maxOneShots))
        { voice = new Voice { source = NewSource("One Shot " + voices.Count) }; voices.Add(voice); }
        if (voice == null)
        {
            // Preserve UI/voice feedback when a skill spawns many simultaneous effects.
            foreach (var candidate in voices)
                if (candidate.bus == bus && (voice == null || candidate.started < voice.started)) voice = candidate;
            if (voice == null && (bus == GameAudioBus.UserInterface || bus == GameAudioBus.Voice))
                foreach (var candidate in voices)
                    if (candidate.bus == GameAudioBus.SpecialEffects && (voice == null || candidate.started < voice.started)) voice = candidate;
            if (voice == null) return;
        }
        voice.source.Stop(); voice.bus = bus; voice.gain = Mathf.Clamp01(gain); voice.started = Time.unscaledTime;
        voice.sceneBound = bus != GameAudioBus.UserInterface;
        voice.source.transform.position = position; voice.source.clip = clip;
        voice.source.spatialBlend = blend; voice.source.pitch = pitch;
        voice.source.minDistance = 1.5f; voice.source.maxDistance = Mathf.Max(1.5f, distance);
        voice.source.rolloffMode = AudioRolloffMode.Linear;
        voice.source.volume = voice.gain * Gain(bus); if (Application.isPlaying) voice.source.Play();
    }
    public static void Button(bool actionMenu = false)
    {
        var audio = Instance; if (audio == null) return;
        var sound = actionMenu ? audio.settings.actionMenuButton : audio.settings.commonButton;
        if (sound.HasClip) { Play(sound, Vector3.zero, GameAudioBus.UserInterface); return; }
        var clip = actionMenu ? GameUITheme.CombatButtonPressSound : null;
        if (clip == null)
        {
            if (fallbackClick == null)
            {
                const int rate = 44100; var samples = new float[2205];
                for (int i = 0; i < samples.Length; i++) { float t = (float)i / rate; samples[i] = Mathf.Sin(t * 2f * Mathf.PI * 1100f) * Mathf.Exp(-t * 145f) * .3f; }
                fallbackClick = AudioClip.Create("UI Soft Click", samples.Length, 1, rate, false); fallbackClick.SetData(samples, 0);
            }
            clip = fallbackClick;
        }
        PlayClipAt(clip, Vector3.zero, sound.volume, GameAudioBus.UserInterface, 0f);
    }
    public static void BeginBattle(CombatManager owner, BattleEncounterSetup setup)
    { if (Instance != null) { Instance.battleOwner = owner; Instance.encounterSetup = setup; Instance.RefreshMusic(); } }
    void OnActiveSceneChanged(Scene old, Scene current)
    {
        foreach (var voice in voices) if (voice.sceneBound) voice.source.Stop();
        areas.Clear(); nextDiscovery = nextBindings = 0; RefreshMusic();
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        nextDiscovery = nextBindings = 0; RefreshListener();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true)) GameButtonSound.Ensure(button);
            GameAudioSourceBus.Bind(root);
        }
    }
    void RefreshListener()
    {
        // One listener also covers the intro, whose cinematic cameras have no listener.
        foreach (var candidate in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (candidate != listener && candidate.enabled) candidate.enabled = false;
        listenerCamera = Camera.main;
        if (listenerCamera == null || !listenerCamera.isActiveAndEnabled)
        {
            listenerCamera = null;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (camera.isActiveAndEnabled && camera.cameraType == CameraType.Game && camera.targetTexture == null &&
                    (listenerCamera == null || camera.depth > listenerCamera.depth)) listenerCamera = camera;
        }
    }
    void LateUpdate()
    {
        var main = Camera.main;
        if (main != null && main.isActiveAndEnabled) listenerCamera = main;
        if (listener != null && listenerCamera != null)
            listener.transform.SetPositionAndRotation(listenerCamera.transform.position, listenerCamera.transform.rotation);
    }
    void Update()
    {
        if (Time.unscaledTime >= nextDiscovery)
        {
            nextDiscovery = Time.unscaledTime + .25f;
            areas.Clear();
            foreach (var point in WorldInteraction.All)
                if (point != null && point.isActiveAndEnabled && point.kind == WorldInteractionKind.Discovery && point.areaMusic.clip != null) areas.Add(point);
            RefreshMusic();
        }
        if (Time.unscaledTime >= nextBindings)
        {
            nextBindings = Time.unscaledTime + .5f;
            RefreshListener();
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)) GameButtonSound.Ensure(button);
            foreach (var player in FindObjectsByType<PlayerScript>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) ActorAudio.For(player.gameObject);
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!managed.Contains(source) && source.GetComponent<GameAudioSourceBus>() == null) source.gameObject.AddComponent<GameAudioSourceBus>();
        }
        fadeAge += Time.unscaledDeltaTime;
        float t = settings.musicFadeSeconds <= 0 ? 1f : Mathf.Clamp01(fadeAge / settings.musicFadeSeconds);
        incomingGain = Mathf.Lerp(incomingStart, desiredGain, t); outgoingGain = Mathf.Lerp(outgoingStart, 0, t);
        ApplyVolumes();
        if (t >= 1f && outgoing.isPlaying) outgoing.Stop();
    }
    void ApplyVolumes()
    {
        if (incoming != null) incoming.volume = incomingGain * Gain(GameAudioBus.Music);
        if (outgoing != null) outgoing.volume = outgoingGain * Gain(GameAudioBus.Music);
        foreach (var voice in voices) voice.source.volume = voice.gain * Gain(voice.bus);
    }
    void RefreshMusic()
    {
        var scene = SceneManager.GetActiveScene(); var session = CampaignSession.Instance;
        GameMusic track = null;
        if (scene.name == settings.titleScene) track = settings.title;
        else if (scene.name == settings.introScene) track = settings.intro;
        else if (battleOwner != null && battleOwner.isActiveAndEnabled && battleOwner.gameObject.scene == scene)
            track = encounterSetup != null && encounterSetup.battleMusic.clip != null ? encounterSetup.battleMusic : settings.battle;
        else if (session != null && session.OnMap && session.Menu == AdventureMenu.Rest)
            track = session.RestPoint != null && session.RestPoint.restMusic.clip != null ? session.RestPoint.restMusic : settings.campfire;
        else if (session != null && session.OnMap)
        {
            track = scene.name == "m" ? settings.m : settings.mapgame;
            if (session.MapPlayer != null)
            {
                WorldInteraction chosen = null; float best = float.MaxValue;
                foreach (var area in areas)
                {
                    float sqr = (area.transform.position - session.MapPlayer.transform.position).sqrMagnitude;
                    if (sqr > area.range * area.range || sqr >= best) continue;
                    best = sqr; chosen = area;
                }
                if (chosen != null) track = chosen.areaMusic;
            }
        }
        ChangeMusic(track);
    }
    public void ChangeMusic(GameMusic track)
    {
        var clip = track?.clip; float gain = clip != null ? Mathf.Clamp01(track.volume) : 0f;
        if (desiredClip == clip && Mathf.Approximately(desiredGain, gain)) return;
        if (desiredClip != clip)
        {
            // Reuse the louder source during rapid zone changes, fading the other one in.
            if (outgoingGain > incomingGain)
            { var swap = incoming; incoming = outgoing; outgoing = swap; float g = incomingGain; incomingGain = outgoingGain; outgoingGain = g; }
            var old = outgoing; outgoing = incoming; incoming = old;
            outgoingGain = incomingGain; incomingGain = 0f;
            incoming.Stop(); incoming.clip = clip; if (clip != null && Application.isPlaying) incoming.Play();
        }
        desiredClip = clip; desiredGain = gain; incomingStart = incomingGain; outgoingStart = outgoingGain; fadeAge = 0;
    }
}
