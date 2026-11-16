using UnityEngine;

[DisallowMultipleComponent]
public sealed class ActorAudio : MonoBehaviour
{
    [Tooltip("Để trống dùng Player/Enemy profile trong GameAudioSettings.")]
    public ActorAudioProfile profile;
    PlayerScript player;
    BattleUnit unit;
    Vector3 previous;
    float travelled, speed;
    AudioSource movementLoop;
    GameSound loopSound;
    public bool battleEntryWalking;
    public ActorAudioProfile Profile => profile != null ? profile : player != null && player.isActiveAndEnabled && player.audioProfile != null ? player.audioProfile :
        unit != null && unit.audioProfile != null ? unit.audioProfile :
        (player != null && player.isActiveAndEnabled || unit != null && unit.isPlayer ? GameAudio.Settings.player : GameAudio.Settings.enemy);
    void Awake() { player = GetComponent<PlayerScript>(); unit = GetComponent<BattleUnit>(); previous = transform.position; }
    void OnEnable() { previous = transform.position; travelled = 0; }
    void LateUpdate()
    {
        Vector3 delta = transform.position - previous; previous = transform.position;
        float distance = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
        speed = distance / Mathf.Max(.001f, Time.deltaTime);
        var p = Profile;
        bool walking = battleEntryWalking || player != null && player.isActiveAndEnabled && player.HasGroundSupport && player.CC != null && player.CC.enabled;
        bool moving = walking && Time.timeScale > 0 && distance >= .001f && distance <= 1f;
        if (p != null && p.loopMovementAudio)
        {
            UpdateMovementLoop(moving ? (speed >= p.runSpeedThreshold && p.run.HasClip ? p.run : p.walk) : null);
            travelled = 0; return;
        }
        UpdateMovementLoop(null);
        if (p == null || p.footstepsByAnimationEvent || !walking || Time.timeScale == 0 || distance > 1f || distance < .001f)
        { travelled = 0; return; }
        travelled += distance;
        float step = speed >= p.runSpeedThreshold ? p.runStepDistance : p.walkStepDistance;
        if (travelled >= Mathf.Max(.1f, step)) { travelled %= Mathf.Max(.1f, step); PlayFootstepSound(); }
    }
    public void Footstep() => PlayFootstepSound();
    void OnDisable() { UpdateMovementLoop(null); travelled = 0; }
    void UpdateMovementLoop(GameSound sound)
    {
        if (sound == null || !sound.HasClip)
        { if (movementLoop != null) movementLoop.Stop(); loopSound = null; return; }
        if (movementLoop == null)
        {
            var host = new GameObject("Movement Audio"); host.transform.SetParent(transform, false);
            movementLoop = host.AddComponent<AudioSource>(); movementLoop.playOnAwake = false; movementLoop.loop = true;
        }
        if (loopSound == sound && movementLoop.isPlaying) return;
        loopSound = sound; movementLoop.Stop(); movementLoop.clip = sound.Pick(); movementLoop.volume = sound.volume;
        movementLoop.spatialBlend = sound.spatialBlend; movementLoop.minDistance = 1.5f; movementLoop.maxDistance = sound.maxDistance;
        movementLoop.rolloffMode = AudioRolloffMode.Linear; movementLoop.pitch = Random.Range(Mathf.Max(.1f, sound.pitch.x), Mathf.Max(.1f, sound.pitch.y));
        GameAudioSourceBus.Bind(movementLoop.gameObject); movementLoop.Play();
    }
    public void PlayFootstepSound()
    {
        var p = Profile; if (p == null || p.loopMovementAudio || Time.timeScale == 0) return;
        GameAudio.Play(speed >= p.runSpeedThreshold && p.run.HasClip ? p.run : p.walk, transform.position);
    }
    public void PlayJumpSound() => GameAudio.Play(Profile?.jump, transform.position);
    public void PlayHurtSound() => GameAudio.Play(Profile?.hurt, transform.position, GameAudioBus.Voice);
    public void PlayDeathSound() => GameAudio.Play(Profile?.die, transform.position, GameAudioBus.Voice);
    public void PlaySkillSound(ActionData action)
    { GameAudio.Play(action != null && action.useSound != null && action.useSound.HasClip ? action.useSound : Profile?.skill, transform.position); }
    public static ActorAudio For(GameObject actor)
    { var component = actor.GetComponent<ActorAudio>(); return component != null ? component : actor.AddComponent<ActorAudio>(); }
}
