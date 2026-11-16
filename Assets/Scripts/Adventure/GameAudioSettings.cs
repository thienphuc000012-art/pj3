using System;
using UnityEngine;

public enum GameAudioBus { Master, Music, Voice, UserInterface, SpecialEffects }

[Serializable]
public sealed class GameSound
{
    [Tooltip("Một hoặc nhiều clip; mỗi lần phát chọn ngẫu nhiên một clip. Để trống = không phát.")]
    public AudioClip[] clips = Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float volume = .8f;
    public Vector2 pitch = Vector2.one;
    [Range(0f, 1f)] public float spatialBlend = 1f;
    [Min(1f)] public float maxDistance = 24f;
    public bool HasClip { get { if (clips != null) foreach (var clip in clips) if (clip != null) return true; return false; } }
    public AudioClip Pick()
    {
        if (!HasClip) return null;
        int start = UnityEngine.Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++) if (clips[(start + i) % clips.Length] != null) return clips[(start + i) % clips.Length];
        return null;
    }
}

[Serializable]
public sealed class GameMusic
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = .65f;
}

[CreateAssetMenu(menuName = "Adventure/Audio/Game Audio Settings", fileName = "GameAudioSettings")]
public sealed class GameAudioSettings : ScriptableObject
{
    [Header("Âm lượng mặc định (Options lưu lựa chọn riêng của người chơi)")]
    [Range(0, 1)] public float master = .8f;
    [Range(0, 1)] public float music = .8f;
    [Range(0, 1)] public float voice = 1f;
    [Range(0, 1)] public float userInterface = 1f;
    [Range(0, 1)] public float specialEffects = 1f;
    [Header("Nhạc nền mặc định")]
    public string titleScene = "Title", introScene = "MangaIntro";
    public GameMusic title = new GameMusic();
    public GameMusic intro = new GameMusic();
    public GameMusic mapgame = new GameMusic();
    public GameMusic m = new GameMusic();
    public GameMusic battle = new GameMusic();
    public GameMusic campfire = new GameMusic();
    [Min(0f)] public float musicFadeSeconds = 1.2f;
    [Header("Nhân vật mặc định (có thể ghi đè trên Player / BattleUnit / Enemy Encounter)")]
    public ActorAudioProfile player;
    public ActorAudioProfile enemy;
    [Header("Tương tác và UI")]
    public GameSound pickup = new GameSound();
    public GameSound chestOpen = new GameSound();
    public GameSound scan = new GameSound();
    public GameSound craft = new GameSound();
    public GameSound commonButton = new GameSound { spatialBlend = 0f, volume = .28f };
    public GameSound actionMenuButton = new GameSound { spatialBlend = 0f, volume = .28f };
    [Header("Giới hạn hiệu ứng phát đồng thời")]
    [Range(8, 96)] public int maxOneShots = 32;
    public float DefaultVolume(GameAudioBus bus) => bus switch
    {
        GameAudioBus.Master => master, GameAudioBus.Music => music, GameAudioBus.Voice => voice,
        GameAudioBus.UserInterface => userInterface, _ => specialEffects
    };
}
