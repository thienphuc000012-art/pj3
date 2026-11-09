using UnityEngine;

public enum CombatCameraState
{
    BattleIdle, TargetSelection, EnemyAnticipation, EnemyExecution, EnemyRecovery,
    PlayerAction, BossWide, BattleIntro, StatusFocus, Impact, SkillCinematic, PlayerMenu, Ultimate
}

public enum CombatCameraEase { Cut, Smooth, EaseIn, EaseOut, Custom }

[System.Serializable]
public sealed class CombatCameraShot
{
    [Tooltip("Relative to the horizontal battle axis: X side, Y up, Z toward the opposing side.")]
    public Vector3 followOffset;
    [Tooltip("Offset from the weighted framing center, in battle space.")]
    public Vector3 lookAtOffset;
    [Range(24, 75)] public float fieldOfView;
    [Min(1)] public float cameraDistance;
    [Min(0), Tooltip("Maximum distance from the aim point after automatic framing. Zero allows unlimited pullback.")]
    public float maximumDistance;
    [Range(-70, 70)] public float orbitAngle;
    [Tooltip("How much the camera looks toward a nearby second combatant during action, or the secondary team in a wide shot.")]
    [Range(0, 1)] public float targetWeight;
    [Tooltip("Desired position of the main subject in the Game View (0,0 = bottom left; 1,1 = top right).")]
    public Vector2 primaryScreenPosition = new Vector2(.5f, .5f);
    [Min(0)] public float damping = .16f;
    [Min(0)] public float blendDuration;
    public CombatCameraEase easing = CombatCameraEase.Smooth;
    public AnimationCurve customBlend = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public CombatCameraShot(float angle, float height, float distance, float fov, float weight, float blend,
        float maxDistance, float screenX, float screenY)
    {
        orbitAngle = angle;
        followOffset = new Vector3(0, height, 0);
        cameraDistance = distance;
        maximumDistance = maxDistance;
        fieldOfView = fov;
        targetWeight = weight;
        primaryScreenPosition = new Vector2(screenX, screenY);
        blendDuration = blend;
    }
}

[CreateAssetMenu(menuName = "Combat/Cinematic Camera Settings", fileName = "CombatCameraSettings")]
public sealed class CombatCameraSettings : ScriptableObject
{
    public CombatCameraShot battleIdle = new CombatCameraShot(-24, 0.35f, 3.35f, 42, 0f, 0.38f, 4.45f, 0.29f, 0.48f);
    public CombatCameraShot playerMenu = new CombatCameraShot(-20, 0.5f, 2.5f, 43, 0f, 0.26f, 3f, 0.29f, 0.4f);
    public CombatCameraShot targetSelection = new CombatCameraShot(0, .16f, 3.15f, 44, 0f, .32f, 7.8f, .5f, .5f);
    public CombatCameraShot enemyAnticipation = new CombatCameraShot(-31, 0.65f, 4.15f, 45, 0.22f, 0.34f, 5.7f, 0.32f, 0.46f);
    public CombatCameraShot enemyExecution = new CombatCameraShot(-35, 0.5f, 3.85f, 46, 0.3f, 0.21f, 5.8f, 0.36f, 0.45f);
    public CombatCameraShot enemyRecovery = new CombatCameraShot(-32, 0.7f, 4.1f, 45, 0.22f, 0.27f, 5.7f, 0.34f, 0.47f);
    public CombatCameraShot playerAction = new CombatCameraShot(-34, 0.65f, 3.6f, 45, 0.3f, 0.26f, 6.6f, 0.34f, 0.49f);
    public CombatCameraShot ultimate = new CombatCameraShot(-28, .55f, 3.15f, 44f, .48f, .35f, 0f, .5f, .5f);
    public CombatCameraShot bossWide = new CombatCameraShot(-43, 2.2f, 7f, 47, 0.5f, 0.38f, 14f, 0.5f, 0.5f);
    public CombatCameraShot battleIntro = new CombatCameraShot(-52, 2.8f, 7.5f, 49, 0.5f, 0f, 14.5f, 0.5f, 0.5f);
    public CombatCameraShot statusFocus = new CombatCameraShot(-31, 0.5f, 3.7f, 44, 0f, 0.3f, 7.3f, 0.5f, 0.5f);

    [Header("Framing")]
    [Range(.5f, .95f)] public float viewportFit = .81f;
    [Range(0f, 45f)] public float targetYawLimit = 18f;
    [Min(0f)] public float targetBlendSecondsPerMeter = .018f;
    [Min(0f)] public float targetBlendExtraLimit = .16f;
    [Range(1f, 1.75f)] public float bossPadding = 1.13f;
    [Min(0)] public float idleDrift = .12f;
    [Min(0)] public float idleDriftSpeed = .65f;
    [Header("Obstacles")]
    public LayerMask obstructionLayers;
    [Min(.02f)] public float obstructionRadius = .23f;
    [Min(.15f)] public float minimumClearDistance = 1f;

    public CombatCameraShot Shot(CombatCameraState state)
    {
        switch (state)
        {
            case CombatCameraState.TargetSelection: return targetSelection;
            case CombatCameraState.PlayerMenu: return playerMenu;
            case CombatCameraState.EnemyAnticipation: return enemyAnticipation;
            case CombatCameraState.EnemyExecution: return enemyExecution;
            case CombatCameraState.EnemyRecovery: return enemyRecovery;
            case CombatCameraState.PlayerAction: return playerAction;
            case CombatCameraState.Ultimate: return ultimate;
            case CombatCameraState.BossWide: return bossWide;
            case CombatCameraState.BattleIntro: return battleIntro;
            case CombatCameraState.StatusFocus: return statusFocus;
            default: return battleIdle;
        }
    }
}
