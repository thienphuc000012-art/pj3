using UnityEngine;

[CreateAssetMenu(menuName = "Adventure/Audio/Actor Audio Profile", fileName = "ActorAudioProfile")]
public sealed class ActorAudioProfile : ScriptableObject
{
    [Header("Di chuyển (SFX)")]
    public GameSound walk = new GameSound();
    public GameSound run = new GameSound();
    public GameSound jump = new GameSound();
    [Min(.1f)] public float walkStepDistance = 1.25f, runStepDistance = 1.65f;
    [Min(.1f)] public float runSpeedThreshold = 3f;
    [Tooltip("Bật nếu animation đã có event Footstep/PlayFootstepSound. Tắt để phát theo quãng đường thực sự di chuyển.")]
    public bool footstepsByAnimationEvent;
    [Tooltip("Bật nếu Walk/Run là cả đoạn tiếng bước chân lặp dài. Tắt để dùng từng tiếng bước chân theo quãng đường hoặc animation event.")]
    public bool loopMovementAudio;
    [Header("Chiến đấu (SFX)")]
    public GameSound skill = new GameSound();
    public GameSound parry = new GameSound();
    [Header("Giọng nhân vật (Voice)")]
    public GameSound hurt = new GameSound();
    public GameSound die = new GameSound();
    public GameSound growl = new GameSound();
    [Tooltip("Thời gian ngẫu nhiên giữa các tiếng gầm khi enemy tuần tra; không phát khi đang menu/cutscene/combat.")]
    public Vector2 growlInterval = new Vector2(7f, 15f);
}
