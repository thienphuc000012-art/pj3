using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class CombatCameraDirector : MonoBehaviour
{
    public const OutputChannels CombatChannel = OutputChannels.Channel08;
    const int LivePriority = 100;
    const float MaxPitch = 40f;

    [SerializeField] CombatCameraSettings settings;
    [SerializeField] bool drawFramingGizmos = true;

    readonly List<Vector3> framePoints = new List<Vector3>(64);
    readonly CombatCameraTransitionController transition = new CombatCameraTransitionController();
    CinemachineBrain brain;
    Camera output;
    CinemachineCamera cameraRig;
    CombatCameraTargetResolver targets;
    CombatCameraSettings runtimeSettings;
    CombatManager combat;
    OutputChannels previousChannelMask;
    CinemachineBrain.UpdateMethods previousUpdateMethod;
    CinemachineBrain.BrainUpdateMethods previousBlendUpdateMethod;
    CinemachineBlendDefinition previousBrainBlend;
    CinemachineBlenderSettings previousCustomBlends;
    SkillCinematicController skill;
    ImpactCameraController impact;
    BattleUnit actor;
    BattleUnit focus;
    bool includeTeams;
    float ultimateViewportHeight;
    CombatCameraState baseState;
    Vector3 currentPosition;
    Quaternion currentRotation;
    float currentFov;
    Vector3 lastAim;
    bool hasShot;

    CombatCameraSettings Settings => settings != null ? settings : runtimeSettings;
    public CombatCameraState BaseState => baseState;
    public CombatCameraState State => skill != null && skill.IsPlaying ? CombatCameraState.SkillCinematic :
        impact != null && impact.IsActive ? CombatCameraState.Impact : baseState;
    public CinemachineCamera CameraRig => cameraRig;
    public float IdleBlendDuration
    {
        get
        {
            var shot = Settings != null ? Settings.battleIdle : null;
            return shot == null ? .45f : shot.easing == CombatCameraEase.Cut ? 0 : shot.blendDuration;
        }
    }

    public bool Initialize(CinemachineBrain outputBrain, CombatManager manager)
    {
        if (!isActiveAndEnabled || outputBrain == null || manager == null) return false;
        if (cameraRig != null) return brain == outputBrain && combat == manager;
        brain = outputBrain;
        output = brain.GetComponent<Camera>();
        if (output == null) return false;
        combat = manager;
        targets = new CombatCameraTargetResolver(combat);
        targets.RegisterParty();

        if (settings == null)
        {
            runtimeSettings = ScriptableObject.CreateInstance<CombatCameraSettings>();
            runtimeSettings.hideFlags = HideFlags.DontSave;
        }

        previousChannelMask = brain.ChannelMask;
        previousUpdateMethod = brain.UpdateMethod;
        previousBlendUpdateMethod = brain.BlendUpdateMethod;
        previousBrainBlend = brain.DefaultBlend;
        previousCustomBlends = brain.CustomBlends;
        brain.ChannelMask = CombatChannel;
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
        brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
        brain.CustomBlends = null;

        var rig = new GameObject("CM_CombatDynamic");
        rig.transform.SetParent(transform, false);
        rig.transform.SetPositionAndRotation(output.transform.position, output.transform.rotation);
        cameraRig = rig.AddComponent<CinemachineCamera>();
        cameraRig.OutputChannel = CombatChannel;
        cameraRig.Priority = LivePriority;
        cameraRig.Lens = LensSettings.FromCamera(output);
        cameraRig.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;

        skill = GetComponent<SkillCinematicController>();
        if (skill == null) skill = gameObject.AddComponent<SkillCinematicController>();
        skill.Stopped += BlendFromTimeline;
        impact = GetComponent<ImpactCameraController>();
        if (impact == null) impact = gameObject.AddComponent<ImpactCameraController>();
        impact.Attach(cameraRig);
        currentPosition = rig.transform.position;
        currentRotation = rig.transform.rotation;
        currentFov = cameraRig.Lens.FieldOfView;
        return true;
    }

    public void ShowIdle(BattleUnit active, bool cut = false)
    {
        var player = active != null && active.isPlayer ? active : targets.Closest(combat.playerParty, active);
        Show(CombatCameraState.BattleIdle, player, targets.Closest(combat.enemyParty, player, true), false, cut);
    }

    public void ShowPlayerMenu(BattleUnit active) =>
        Show(CombatCameraState.PlayerMenu, active, targets.Closest(combat.enemyParty, active, true));

    public void Select(BattleUnit active, BattleUnit selected)
    {
        float duration = -1;
        if (hasShot && baseState == CombatCameraState.TargetSelection && focus != null && focus != selected)
        {
            float distance = Vector3.Distance(targets.BattlePosition(focus), targets.BattlePosition(selected));
            float extra = Mathf.Min(Settings.targetBlendExtraLimit, distance * Settings.targetBlendSecondsPerMeter);
            duration = Settings.targetSelection.blendDuration + extra;
        }
        Show(CombatCameraState.TargetSelection, active, selected, duration: duration);
    }

    public void ShowIntro() =>
        Show(CombatCameraState.BattleIntro, targets.Closest(combat.playerParty, null),
            targets.Closest(combat.enemyParty, null, true), true, true);

    public void ShowBoss(BattleUnit boss) =>
        Show(CombatCameraState.BossWide, boss, targets.Closest(combat.playerParty, boss), true);

    public void ShowStatus(BattleUnit unit) =>
        Show(CombatCameraState.StatusFocus, targets.Closest(unit.isPlayer ? combat.enemyParty : combat.playerParty, unit), unit);

    public void Anticipate(BattleUnit enemy, BattleUnit target, bool wide)
    {
        bool wideBossAttack = wide && enemy != null && enemy.isBoss && target != null && target.isPlayer;
        Show(wideBossAttack ? CombatCameraState.BossWide : CombatCameraState.EnemyAnticipation,
            enemy, target, wideBossAttack);
    }

    public void BeginAction(BattleUnit attacker, BattleUnit target, ActionData action, float ultimateBlendDuration)
    {
        if (action.isUltimate)
        {
            var letterbox = combat.GetComponent<UltimateCinematic>();
            float barHeight = letterbox != null ? Mathf.Clamp(letterbox.barHeight, .05f, .25f) : .12f;
            ultimateViewportHeight = 1f - 2f * barHeight;
            Show(CombatCameraState.Ultimate, attacker, target, duration: ultimateBlendDuration);
            return;
        }

        var state = attacker.isPlayer ? CombatCameraState.PlayerAction : CombatCameraState.EnemyExecution;
        Show(state, attacker, target);
    }

    public void PlaySkill(BattleUnit attacker, BattleUnit target, ActionData action)
    {
        if (action.skillCinematic != null)
        {
            // Keep a dynamic shot under the Timeline override. It is ready if
            // the authored Timeline ends before the action or is interrupted.
            skill.Play(action.skillCinematic, brain, attacker.transform, target.transform,
                targets.BattlePosition(attacker), targets.BattlePosition(target), impact);
        }
    }

    public void PlayImpact(ActionData action, BattleUnit attacker, BattleUnit target,
        bool critical, bool parried, bool allowLocalHitStop)
    {
        impact.Play(action, attacker, target, critical, parried, allowLocalHitStop && !skill.IsPlaying);
    }

    public void Recover(BattleUnit attacker, BattleUnit target)
    {
        if (attacker == null || attacker.isPlayer || actor != attacker ||
            (baseState != CombatCameraState.EnemyExecution && baseState != CombatCameraState.BossWide)) return;
        Show(CombatCameraState.EnemyRecovery, attacker, target, includeTeams, preserveSkill: true);
    }

    public void StopSkill() { if (skill != null) skill.Stop(); }

    void BlendFromTimeline()
    {
        if (!hasShot || cameraRig == null || output == null) return;
        currentPosition = output.transform.position;
        currentRotation = output.transform.rotation;
        currentFov = output.fieldOfView;
        var shot = Settings.Shot(baseState);
        transition.Begin(currentPosition, currentRotation, currentFov, shot, shot.blendDuration);
        UpdateShot(0, shot.easing == CombatCameraEase.Cut || shot.blendDuration <= 0);
    }

    public void ResetPresentation()
    {
        StopSkill();
        if (impact != null) impact.ResetFeedback();
    }

    void Show(CombatCameraState state, BattleUnit primary, BattleUnit secondary, bool wide = false,
        bool cut = false, float duration = -1, bool preserveSkill = false)
    {
        if (!preserveSkill) StopSkill();
        if (hasShot && !cut && duration < 0 && baseState == state && actor == primary &&
            focus == secondary && includeTeams == wide) return;

        bool avoidSubject = state == CombatCameraState.TargetSelection ||
                            (hasShot && baseState == CombatCameraState.TargetSelection);
        actor = primary;
        focus = secondary;
        baseState = state;
        includeTeams = wide;
        CombatCameraShot shot = Settings.Shot(state);
        bool snap = cut || !hasShot || shot.easing == CombatCameraEase.Cut ||
                    (duration >= 0 ? duration : shot.blendDuration) <= 0;
        hasShot = true;
        transition.Begin(currentPosition, currentRotation, currentFov, shot,
            snap ? 0 : duration >= 0 ? duration : shot.blendDuration, avoidSubject);
        if (snap) UpdateShot(0, true);
    }

    void LateUpdate()
    {
        if (skill != null && skill.IsPlaying &&
            (combat == null || combat.state != CombatState.Executing || actor == null || actor.IsDead))
            StopSkill();
        if (hasShot && cameraRig != null) UpdateShot(Time.unscaledDeltaTime, false);
    }

    void UpdateShot(float deltaTime, bool snap)
    {
        if (actor == null && focus == null) return;
        CombatCameraShot shot = Settings.Shot(baseState);
        bool ultimate = baseState == CombatCameraState.Ultimate;
        bool attacking = baseState == CombatCameraState.EnemyExecution || baseState == CombatCameraState.EnemyRecovery ||
                         baseState == CombatCameraState.PlayerAction || baseState == CombatCameraState.BossWide || ultimate;
        float actorMotion = attacking || baseState == CombatCameraState.BattleIntro ? 1f : 0f;
        float targetMotion = ultimate ? 1f : attacking ? .75f : 0f;
        Vector3 actorAim = actor != null ? Aim(actor, actorMotion, attacking) : Aim(focus, targetMotion, false);
        Vector3 targetAim = focus != null ? Aim(focus, targetMotion, false) : actorAim;
        if (ultimate && focus != null && focus != actor && focus.isBoss)
            targetAim = targets.SelectionAim(focus, targetAim);
        Vector3 arenaPlayer = targets.TeamCenter(combat.playerParty, actorAim, false, includeDefeated: true);
        Vector3 arenaEnemy = targets.TeamCenter(combat.enemyParty, targetAim, false, includeDefeated: true);
        Vector3 axis = Vector3.ProjectOnPlane(arenaEnemy - arenaPlayer, Vector3.up);
        if (axis.sqrMagnitude < .01f)
            axis = Vector3.ProjectOnPlane(actor != null ? actor.transform.forward : Vector3.forward, Vector3.up);
        if (axis.sqrMagnitude < .01f) axis = Vector3.forward;
        axis.Normalize();

        if (actor != null && focus != null && actor.isPlayer != focus.isPlayer)
        {
            Vector3 pair = targets.BattlePosition(focus) - targets.BattlePosition(actor);
            if (!actor.isPlayer) pair = -pair;
            pair.y = 0;
            if (pair.sqrMagnitude > .01f)
            {
                float yaw = Vector3.SignedAngle(axis, pair, Vector3.up);
                axis = Quaternion.AngleAxis(Mathf.Clamp(yaw, -Settings.targetYawLimit, Settings.targetYawLimit), Vector3.up) * axis;
            }
        }
        Quaternion basis = Quaternion.LookRotation(axis, Vector3.up);
        if (!includeTeams && !ultimate)
        {
            UpdateFocusedShot(shot, axis, basis, actorAim, targetAim, actorMotion, targetMotion, deltaTime, snap);
            return;
        }

        Vector3 aim = Vector3.Lerp(actorAim, targetAim, shot.targetWeight) + basis * shot.lookAtOffset;
        bool walkingIn = baseState == CombatCameraState.BattleIntro;
        if (includeTeams)
        {
            var playerCenter = targets.TeamCenter(combat.playerParty, arenaPlayer, walkingIn);
            var enemyCenter = targets.TeamCenter(combat.enemyParty, arenaEnemy, false);
            Vector3 teamAim = (playerCenter + enemyCenter) * .5f + Vector3.up * 1.5f;
            aim = Vector3.Lerp(aim, teamAim, .7f);
        }

        Vector3 back = Quaternion.AngleAxis(shot.orbitAngle, Vector3.up) * -axis;
        float distance = Mathf.Max(1f, shot.cameraDistance);
        Vector3 offset = back * distance + basis * shot.followOffset;
        Vector3 planar = Vector3.ProjectOnPlane(offset, Vector3.up);
        if (planar.sqrMagnitude < .01f) planar = back;
        float maxHeight = planar.magnitude * Mathf.Tan(MaxPitch * Mathf.Deg2Rad);
        Vector3 direction = (planar + Vector3.up * Mathf.Clamp(offset.y, -maxHeight, maxHeight)).normalized;
        Quaternion orientation = Quaternion.LookRotation(-direction, Vector3.up);

        framePoints.Clear();
        if (actor != null) AddPoints(actor, actorMotion);
        if (focus != null && focus != actor)
        {
            if (!ultimate || !focus.isBoss || !targets.TryAddSelectionFramePoints(framePoints, focus, targetAim))
                AddPoints(focus, targetMotion);
        }
        if (includeTeams)
        {
            AddOtherTeamMembers(combat.playerParty, walkingIn ? 1f : 0f);
            AddOtherTeamMembers(combat.enemyParty, 0f);
        }

        float fov = Mathf.Clamp(shot.fieldOfView, 24, 75);
        float tanHalfFov = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        float fit = Mathf.Max(.1f, Settings.viewportFit);
        // Black bars reduce vertical room only. Preserve horizontal coverage for the two subjects.
        float tanVertical = tanHalfFov * (ultimate ? Mathf.Min(fit, ultimateViewportHeight - .04f) : fit);
        float tanHorizontal = tanHalfFov * fit * Mathf.Max(.1f, output.aspect);
        Vector3 right = orientation * Vector3.right;
        Vector3 up = orientation * Vector3.up;
        distance = Mathf.Max(distance, offset.magnitude);
        foreach (Vector3 point in framePoints)
            distance = Mathf.Max(distance, FitDistance(point - aim, direction, right, up, tanHorizontal, tanVertical));
        if (shot.maximumDistance > 0)
            distance = Mathf.Min(distance, Mathf.Max(1f, shot.maximumDistance));

        Vector3 desiredPosition = aim + direction * distance;
        ApplyShot(shot, aim, desiredPosition, Quaternion.LookRotation(aim - desiredPosition, Vector3.up), fov, deltaTime, snap);
    }

    void UpdateFocusedShot(CombatCameraShot shot, Vector3 axis, Quaternion basis,
        Vector3 actorAim, Vector3 targetAim, float actorMotion, float targetMotion, float deltaTime, bool snap)
    {
        bool defense = baseState == CombatCameraState.EnemyAnticipation ||
                       baseState == CombatCameraState.EnemyExecution || baseState == CombatCameraState.EnemyRecovery;
        bool targeting = baseState == CombatCameraState.TargetSelection;
        bool focusTarget = focus != null && (targeting || baseState == CombatCameraState.StatusFocus ||
                                             (defense && focus.isPlayer) || actor == null);
        BattleUnit primary = focusTarget ? focus : actor;
        BattleUnit secondary = focusTarget ? actor : focus;
        if (primary == null) return;
        Vector3 pivot = focusTarget ? targetAim : actorAim;
        if (targeting)
        {
            // Forward belongs to the selected model. The team axis is useful
            // for action shots, but puts allies in profile when choosing a target.
            Vector3 front = Vector3.ProjectOnPlane(primary.transform.forward, Vector3.up);
            if (front.sqrMagnitude > .001f)
                axis = -front.normalized;
            else if (primary.isPlayer)
                axis = -axis;
            basis = Quaternion.LookRotation(axis, Vector3.up);
            pivot = targets.SelectionAim(primary, pivot);
        }
        Vector3 secondaryAim = focusTarget ? actorAim : targetAim;
        float primaryMotion = focusTarget ? targetMotion : actorMotion;
        bool pairAction = defense || baseState == CombatCameraState.PlayerAction;
        float pairWeight = pairAction && secondary != null && secondary != primary
            ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, 5.5f, Vector3.Distance(pivot, secondaryAim)))
            : 0f;

        // Action shots keep the battle axis stable during a melee crossover.
        float orbit = targeting ? Mathf.Clamp(shot.orbitAngle, -20f, 20f) : shot.orbitAngle;
        float baseDistance = Mathf.Max(1f, shot.cameraDistance);
        Vector3 offset = Quaternion.AngleAxis(orbit, Vector3.up) * -axis * baseDistance + basis * shot.followOffset;
        Vector3 planar = Vector3.ProjectOnPlane(offset, Vector3.up);
        if (planar.sqrMagnitude < .01f) planar = -axis;
        float maxHeight = planar.magnitude * Mathf.Tan(MaxPitch * Mathf.Deg2Rad);
        Vector3 direction = (planar + Vector3.up * Mathf.Clamp(offset.y, -maxHeight, maxHeight)).normalized;
        Quaternion orientation = Quaternion.LookRotation(-direction, Vector3.up);
        Vector3 right = orientation * Vector3.right;
        Vector3 up = orientation * Vector3.up;

        Vector3 look = pivot + basis * shot.lookAtOffset +
            Vector3.ClampMagnitude(secondaryAim - pivot, 2f) * (shot.targetWeight * pairWeight);
        float fov = Mathf.Clamp(shot.fieldOfView, 24f, 75f);
        float vertical = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        float horizontal = vertical * Mathf.Max(.1f, output.aspect);
        float fit = Mathf.Max(.1f, Settings.viewportFit);
        framePoints.Clear();
        if (!targeting || !targets.TryAddSelectionFramePoints(framePoints, primary, pivot))
            AddPoints(primary, primaryMotion);
        float distance = Mathf.Max(baseDistance, offset.magnitude);
        foreach (Vector3 point in framePoints)
            distance = Mathf.Max(distance, FitDistance(point - pivot, direction, right, up, horizontal * fit, vertical * fit));

        if (pairWeight > 0f)
        {
            framePoints.Add(secondaryAim);
            float paired = FitDistance(secondaryAim - pivot, direction, right, up, horizontal * fit, vertical * fit);
            distance = Mathf.Lerp(distance, Mathf.Max(distance, paired), pairWeight);
        }
        if (shot.maximumDistance > 0f) distance = Mathf.Min(distance, Mathf.Max(1f, shot.maximumDistance));

        Vector3 position = pivot + direction * distance;
        if (baseState == CombatCameraState.BattleIdle)
        {
            float time = Time.unscaledTime * Settings.idleDriftSpeed;
            position += (right * Mathf.Sin(time) + up * (.4f * Mathf.Sin(time * 1.37f))) * Settings.idleDrift;
        }
        Vector2 screen = shot.primaryScreenPosition;
        Vector3 viewRay = new Vector3((Mathf.Clamp01(screen.x) - .5f) * 2f * horizontal,
            (Mathf.Clamp01(screen.y) - .5f) * 2f * vertical, 1f);
        Quaternion rotation = Quaternion.LookRotation(look - position, Vector3.up) *
            Quaternion.Inverse(Quaternion.LookRotation(viewRay, Vector3.up));
        ApplyShot(shot, look, position, rotation, fov, deltaTime, snap);
    }

    static float FitDistance(Vector3 relative, Vector3 back, Vector3 right, Vector3 up, float horizontal, float vertical) =>
        Vector3.Dot(relative, back) + Mathf.Max(Mathf.Abs(Vector3.Dot(relative, right)) / horizontal,
            Mathf.Abs(Vector3.Dot(relative, up)) / vertical) + .15f;

    void ApplyShot(CombatCameraShot shot, Vector3 aim, Vector3 desiredPosition, Quaternion desiredRotation,
        float fov, float deltaTime, bool snap)
    {
        if (snap)
        {
            currentPosition = desiredPosition;
            currentRotation = desiredRotation;
            currentFov = fov;
        }
        else
        {
            transition.Update(ref currentPosition, ref currentRotation, ref currentFov,
                desiredPosition, desiredRotation, fov, shot.damping, deltaTime, aim);
        }

        lastAim = aim;
        Vector3 safePosition = AvoidObstacle(aim, currentPosition);
        Quaternion safeRotation = currentRotation;
        if (safePosition != currentPosition && (aim - safePosition).sqrMagnitude > .0001f)
            safeRotation = Quaternion.FromToRotation(aim - currentPosition, aim - safePosition) * safeRotation;
        cameraRig.transform.SetPositionAndRotation(safePosition, safeRotation);
        LensSettings lens = cameraRig.Lens;
        lens.FieldOfView = currentFov;
        cameraRig.Lens = lens;
    }

    void AddOtherTeamMembers(List<BattleUnit> team, float liveWeight)
    {
        foreach (var unit in team)
            if (unit != null && !unit.IsDead && unit != actor && unit != focus) AddPoints(unit, liveWeight);
    }

    Vector3 Aim(BattleUnit unit, float liveWeight, bool attacking) =>
        targets.Aim(unit, liveWeight, attacking, unit.isBoss && !includeTeams);

    void AddPoints(BattleUnit unit, float liveWeight) =>
        targets.AddFramePoints(framePoints, unit, liveWeight, unit.isBoss && includeTeams ? Settings.bossPadding : 1,
            unit.isBoss && !includeTeams);

    Vector3 AvoidObstacle(Vector3 aim, Vector3 position)
    {
        Vector3 ray = position - aim;
        float distance = ray.magnitude;
        if (Settings.obstructionLayers.value == 0 || distance < .001f) return position;
        Vector3 direction = ray / distance;
        if (!Physics.SphereCast(aim, Settings.obstructionRadius, direction, out RaycastHit hit, distance,
                Settings.obstructionLayers, QueryTriggerInteraction.Ignore)) return position;

        float clearance = Mathf.Min(Settings.minimumClearDistance, distance);
        float surface = Mathf.Max(.05f, hit.distance - .05f);
        if (surface < clearance)
        {
            // Try a raised angle when the normal route is blocked close to the subject.
            Vector3 raised = (direction + Vector3.up * .35f).normalized;
            if (!Physics.SphereCast(aim, Settings.obstructionRadius, raised, out _, distance,
                    Settings.obstructionLayers, QueryTriggerInteraction.Ignore)) return aim + raised * distance;
        }
        return aim + direction * surface;
    }

    void OnDisable()
    {
        if (skill != null) skill.Stopped -= BlendFromTimeline;
        ResetPresentation();
        if (cameraRig != null)
        {
            cameraRig.gameObject.SetActive(false);
            Destroy(cameraRig.gameObject);
        }
        if (brain != null && cameraRig != null)
        {
            brain.ChannelMask = previousChannelMask;
            brain.UpdateMethod = previousUpdateMethod;
            brain.BlendUpdateMethod = previousBlendUpdateMethod;
            brain.DefaultBlend = previousBrainBlend;
            brain.CustomBlends = previousCustomBlends;
        }
        if (runtimeSettings != null) Destroy(runtimeSettings);
        cameraRig = null;
        hasShot = false;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawFramingGizmos || !hasShot || cameraRig == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(lastAim, .2f);
        Gizmos.DrawLine(lastAim, cameraRig.transform.position);
        Gizmos.color = Color.yellow;
        foreach (Vector3 point in framePoints) Gizmos.DrawWireSphere(point, .07f);
    }
}
