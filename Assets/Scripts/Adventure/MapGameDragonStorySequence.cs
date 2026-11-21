using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

[DefaultExecutionOrder(-700)]
public sealed class MapGameDragonStorySequence : MonoBehaviour
{
    [Header("Quest and trigger")]
    public MapGameSetup mapSetup;
    [Tooltip("Scene marker at the cutscene trigger position; select it and press F to frame the trigger volume.")]
    public Transform triggerMarker;
    public string requiredQuestId = "12";
    public Vector3 triggerPosition = new Vector3(-21.197134f, 291.01001f, -23.7513275f);
    public Vector3 encounterPosition = new Vector3(-21.197134f, 291.01001f, -23.7513275f);
    public Vector3 mountainDragonStartPosition = new Vector3(-21.197134f, 299.100006f, -23.7513275f);
    public float triggerRadius = 5f;
    public float triggerHeightTolerance = 6f;
    [Header("Enemies")]
    public BattleUnit mountainDragon, dragonide;
    public ActionData mountainSpitFireBall;
    public AnimationClip mountainDragonDeath, dragonideDeath;
    [Header("Combat setup IDs")]
    [Tooltip("Must match a BattleEncounterSetup ID in the combat scene for MountainDragon.")]
    public string mountainDragonCombatSetupId = "default";
    [Tooltip("Must match a BattleEncounterSetup ID in the combat scene for Dragonide.")]
    public string dragonideCombatSetupId = "default";
    [Header("Enemy rotations")]
    [Tooltip("World rotation in degrees for MountainDragon during the cutscene and encounter.")]
    public Vector3 mountainDragonRotationEuler;
    [Tooltip("World rotation in degrees for Dragonide during its encounter.")]
    public Vector3 dragonideRotationEuler;
    [Header("Mountain Dragon cutscene")]
    public string mountainFlyDownState = "Move Layer.FlyStationary";
    public string mountainLandingState = "Move Layer.GlideToLanding";
    public string mountainFlyIdleState = "Move Layer.FlyStationary";
    public string mountainFlyAttackState = "Move Layer.FlyStationarySpitFireBall";
    [Min(0f)] public float mountainCameraMinimumDistance = 13f;
    [Range(25f, 85f)] public float mountainCameraFieldOfView = 58f;
    [Min(0f)] public float mountainCameraElevation = 3f;
    [Min(1f)] public float mountainCameraFramingPadding = 1.2f;
    [Header("Fireball camera framing")]
    [Tooltip("Camera distance while following the fireball. Lower values zoom in closer.")]
    [Min(2f)] public float mountainFireballCameraDistance = 8f;
    [Min(0f)] public float mountainFireballCameraElevation = 1.6f;
    public float mountainDescentSeconds = 4f;
    public float encounterRange = 12f;
    [Header("Player")]
    public float playerWalkSpeed = 1.6f;
    public string playerWalkState = "Base Layer.Basic Movement";
    public float playerWalkAnimValue = .5f;
    [Header("Ring and gate")]
    public GameObject ringPrefab, sceneRingObject, gatePrefab;
    public string ringItemId;
    public float ringScale = .45f;
    public Vector3 ringPositionOffset = new Vector3(0, .8f, 0);
    [Header("Ring highlight")]
    public Color ringGlowColor = new Color(1f, .72f, .2f, 1f);
    [Min(.5f)] public float ringGlowRange = 4f;
    [Min(0f)] public float ringGlowIntensity = 2.4f;
    [Header("Ring pickup")]
    public string ringPickupAnimationState = "Base Layer.pickupitem";
    [Tooltip("Maximum wait for this one-shot before returning to the player's locomotion state.")]
    [Min(.1f)] public float ringPickupAnimationSeconds = .9f;
    [Tooltip("Offset from the encounter point, adjusted to place the portal model on the floor.")]
    public Vector3 gatePositionOffset = new Vector3(1.18f, 4.62f, -.15f);
    [Tooltip("World rotation matching the portal used in scene m.")]
    public Vector3 gateRotationEuler = new Vector3(0, 34.3f, 0);
    public Vector3 gateScale = Vector3.one * 3f;
    public float mapWipeSeconds = 1.8f;
    public UnityEngine.Events.UnityEvent onOutroRequested = new UnityEngine.Events.UnityEvent();
    [Header("Dialogue audio and subtitles (clips can be added later)")]
    public GameSound playerArrivalVoice = new GameSound { spatialBlend = 0, volume = 1 };
    [TextArea] public string playerArrivalSubtitle = "Có thứ gì đó đang chờ phía trước...";
    public GameSound mountainDragonVoice = new GameSound { spatialBlend = 0, volume = 1 };
    [TextArea] public string mountainDragonSubtitle = "Kẻ xâm nhập... ngươi sẽ không rời khỏi đây.";
    public GameSound dragonideIntroVoice = new GameSound { spatialBlend = 0, volume = 1 };
    [TextArea] public string dragonideIntroSubtitle = "Cuối cùng chúng ta cũng gặp nhau.";
    public GameSound dragonideDeathVoice = new GameSound { spatialBlend = 0, volume = 1 };
    [TextArea] public string dragonideDeathSubtitle = "Sức mạnh của ngươi... thật đáng kinh ngạc.";
    public GameSound playerGateVoice = new GameSound { spatialBlend = 0, volume = 1 };
    [TextArea] public string playerGateSubtitle = "Đã đến lúc rời khỏi nơi này.";
    public float dialogueFallbackSeconds = 2.1f;

    const string MountainId = "mapgame:dragon-story:mountain-dragon";
    const string DragonideId = "mapgame:dragon-story:dragonide";
    const string MIntro = "mapgame.dragon-story.mountain-intro";
    const string MDeath = "mapgame.dragon-story.mountain-death";
    const string DIntro = "mapgame.dragon-story.dragonide-intro";
    const string DDeath = "mapgame.dragon-story.dragonide-death";
    const string RingTaken = "mapgame.dragon-story.ring";
    const string Outro = "mapgame.dragon-story.outro";
    CampaignSession session;
    CinemachineCamera storyCamera;
    float storyCameraDefaultFieldOfView;
    bool hasStoryCameraDefaultFieldOfView;
    Canvas canvas;
    Image topBar, bottomBar, subtitlePanel, wipe;
    TMP_Text subtitle;
    Material wipeMaterial;
    GameObject ring, gate;
    StoryActor mountainActor, dragonideActor;
    bool ownsPresentation;
    StoryActor mountainFramingActor;
    Bounds mountainFramingLocalBounds;
    bool hasMountainFramingBounds;
    Vector3 mountainCameraBack = Vector3.back;
    bool hasMountainCameraBack;
    PlayableGraph deathGraph;
    MapDeathDissolve dissolve;
    static readonly int ProgressId = Shader.PropertyToID("_Progress");

    sealed class StoryActor
    {
        public GameObject root, modelRoot;
        public WorldInteraction point;
        public EnemyEncounterBehaviour encounter;
        public Animator animator;
    }

    IEnumerator Start()
    {
        if (gameObject.scene.name != "mapgame") yield break;
        if (mapSetup == null) mapSetup = GetComponent<MapGameSetup>();
        while (isActiveAndEnabled && (mapSetup == null || !mapSetup.StartupReady || CampaignSession.Instance == null || CampaignSession.Instance.Data == null))
            yield return null;
        session = CampaignSession.Instance;
        if (session == null || !session.OnMap) yield break;
        if (sceneRingObject != null)
        {
            ring = sceneRingObject;
            ring.SetActive(false);
        }
        while (isActiveAndEnabled && session != null && session.OnMap)
        {
            if (session.Data == null || session.QuestProgress(requiredQuestId)?.completed != true)
            { yield return new WaitForSecondsRealtime(.25f); continue; }
            bool mountainWon = session.Data.collected.Contains(MountainId);
            bool dragonideWon = session.Data.collected.Contains(DragonideId);
            if (!mountainWon)
            {
                Vector3 p = session.MapPlayer != null ? session.MapPlayer.transform.position : Vector3.zero;
                Vector2 a = new Vector2(p.x, p.z), b = new Vector2(triggerPosition.x, triggerPosition.z);
                if (Vector2.Distance(a, b) > triggerRadius || Mathf.Abs(p.y - triggerPosition.y) > triggerHeightTolerance)
                { yield return null; continue; }
                yield return StartMountainEncounter();
                if (!session.OnMap) yield break;
                yield return null; continue;
            }
            if (!session.Data.HasStoryFlag(MDeath))
            { yield return PresentMountainDeath(); if (!session.OnMap) yield break; continue; }
            if (!dragonideWon)
            { yield return StartDragonideEncounter(); if (!session.OnMap) yield break; yield return null; continue; }
            if (!session.Data.HasStoryFlag(DDeath))
            { yield return PresentDragonideDeath(); if (!session.OnMap) yield break; continue; }
            if (!session.Data.HasStoryFlag(RingTaken))
            { yield return CollectRingAndOpenGate(); if (!session.OnMap) yield break; continue; }
            if (!session.Data.HasStoryFlag(Outro))
            { yield return WalkThroughGate(); if (!session.OnMap) yield break; }
            yield break;
        }
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && triggerMarker != null)
            triggerMarker.SetPositionAndRotation(triggerPosition, Quaternion.identity);
#endif
    }

    IEnumerator StartMountainEncounter()
    {
        if (mountainDragon == null) { Debug.LogError("[MapGame story] Assign MountainDragon.", this); yield break; }
        CacheMountainCameraBack();
        yield return AcquirePresentation();
        if (!ownsPresentation) yield break;
        bool first = !session.Data.HasStoryFlag(MIntro);
        mountainActor = EnsureActor(mountainDragon, "MountainDragon", MountainId,
            first ? mountainDragonStartPosition : encounterPosition, mountainSpitFireBall,
            mountainDragonCombatSetupId, mountainDragonRotationEuler, mountainActor);
        if (mountainActor == null) { Restore(); yield break; }
        CacheMountainActorBounds();
        mountainActor.encounter.SetEncounterAirborne(true);
        mountainActor.encounter.flyAttackState = StateLeafName(mountainFlyAttackState);
        if (first)
        {
            Vector3 toward = session.MapPlayer.transform.position - triggerPosition; toward.y = 0;
            Vector3 target = triggerPosition + (toward.sqrMagnitude > .01f ? toward.normalized : -session.MapPlayer.transform.forward) * 1.15f;
            target.y = session.MapPlayer.transform.position.y;
            yield return WalkPlayerTo(target, 3.5f);
            yield return Dialogue(playerArrivalVoice, playerArrivalSubtitle);
            PlayState(mountainActor.animator, mountainFlyDownState);
            yield return DescendMountain();
            FocusMountainActors();
            yield return Dialogue(mountainDragonVoice, mountainDragonSubtitle);
            yield return PlayFireball();
            session.Data.SetStoryFlag(MIntro); session.Save(false);
        }
        else
        {
            mountainActor.root.transform.position = encounterPosition;
            FocusMountainActors();
        }
        yield return BeginCombat(mountainActor);
    }

    IEnumerator DescendMountain()
    {
        Vector3 from = mountainActor.root.transform.position, to = encounterPosition;
        float length = Mathf.Max(.3f, mountainDescentSeconds);
        for (float elapsed = 0; elapsed < length; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / length);
            mountainActor.root.transform.position = Vector3.Lerp(from, to, t);
            FocusMountainActors();
            yield return null;
        }
        mountainActor.root.transform.position = to;
        mountainActor.point.GetComponent<EnemyCombatOrigin>()?.SetSpawnPoint(to, mountainActor.root.transform.rotation);
        PlayState(mountainActor.animator, mountainLandingState);
    }

    IEnumerator PlayFireball()
    {
        if (mountainSpitFireBall == null || session.MapPlayer == null) yield break;
        if (!PlayState(mountainActor.animator, mountainFlyAttackState))
            Debug.LogWarning($"[MapGame story] Could not play MountainDragon flight attack state '{mountainFlyAttackState}'.", this);
        Vector3 dragonPos = mountainActor.root.transform.position;
        Vector3 playerPos = session.MapPlayer.transform.position + mountainSpitFireBall.projectileTargetOffset;
        Quaternion face = Quaternion.LookRotation(playerPos - dragonPos);
        SpawnEffect(mountainSpitFireBall.castVfxPrefab, dragonPos + Vector3.up * 1.5f, face, mountainSpitFireBall.castVfxLifeTime);
        Vector3 start = mountainActor.root.transform.TransformPoint(mountainSpitFireBall.projectileStartOffset);
        GameObject projectile = mountainSpitFireBall.vfxPrefab != null
            ? Instantiate(mountainSpitFireBall.vfxPrefab, start, Quaternion.LookRotation(playerPos - start)) : null;
        if (projectile != null) ConfigureEffect(projectile, mountainSpitFireBall.vfxPlaybackSpeed);
        float duration = Mathf.Clamp(Vector3.Distance(start, playerPos) / Mathf.Max(1, mountainSpitFireBall.vfxSpeed), .2f, 3f);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            Vector3 projectilePosition = Vector3.Lerp(start, playerPos, t);
            projectilePosition.y += Mathf.Sin(t * Mathf.PI) * mountainSpitFireBall.curveHeight;
            if (projectile != null)
                projectile.transform.position = projectilePosition;
            FocusFireballPoint(projectilePosition);
            yield return null;
        }
        if (projectile != null) Destroy(projectile);
        SpawnEffect(mountainSpitFireBall.hitVfxPrefab, playerPos, Quaternion.identity, mountainSpitFireBall.hitVfxLifeTime);
        FocusFireballPoint(session.MapPlayer.transform.position + Vector3.up * 1.1f);
        PlayState(mountainActor.animator, mountainFlyIdleState);
    }

    IEnumerator PresentMountainDeath()
    {
        if (mountainDragon == null || mountainDragonDeath == null)
        { Debug.LogError("[MapGame story] Assign MountainDragon death clip.", this); yield break; }
        yield return AcquirePresentation();
        if (!ownsPresentation) yield break;
        mountainActor = EnsureActor(mountainDragon, "MountainDragon Death", MountainId, encounterPosition,
            null, mountainDragonCombatSetupId, mountainDragonRotationEuler, mountainActor);
        if (mountainActor == null) { Restore(); yield break; }
        if (dragonide != null)
            dragonideActor = EnsureActor(dragonide, "Dragonide", DragonideId, encounterPosition, null,
                dragonideCombatSetupId, dragonideRotationEuler, dragonideActor);
        else
            Debug.LogWarning("[MapGame story] Assign Dragonide so it can appear during MountainDragon's death cutscene.", this);
        FocusMountainActors();
        PlayDeath(mountainActor, mountainDragonDeath, mountainDragon.enemyDeathDissolveMaterial);
        GameAudio.Play(mountainDragon.audioProfile != null ? mountainDragon.audioProfile.die : GameAudio.Settings.enemy?.die,
            mountainActor.root.transform.position, GameAudioBus.Voice);
        yield return DeathFor(mountainDragonDeath, 2f);
        DestroyActor(mountainActor); mountainActor = null;
        session.Data.SetStoryFlag(MDeath); session.Save(false); Restore();
    }

    IEnumerator StartDragonideEncounter()
    {
        if (dragonide == null) { Debug.LogError("[MapGame story] Assign Dragonide.", this); yield break; }
        yield return AcquirePresentation();
        if (!ownsPresentation) yield break;
        dragonideActor = EnsureActor(dragonide, "Dragonide", DragonideId, encounterPosition,
            null, dragonideCombatSetupId, dragonideRotationEuler, dragonideActor);
        if (dragonideActor == null) { Restore(); yield break; }
        Focus(dragonideActor.root.transform.position + Vector3.up * 1.3f, CameraOffset(5.6f, 1.8f));
        if (!session.Data.HasStoryFlag(DIntro))
        {
            yield return Dialogue(dragonideIntroVoice, dragonideIntroSubtitle);
            session.Data.SetStoryFlag(DIntro); session.Save(false);
        }
        yield return BeginCombat(dragonideActor);
    }

    IEnumerator PresentDragonideDeath()
    {
        if (dragonide == null || dragonideDeath == null)
        { Debug.LogError("[MapGame story] Assign Dragonide death clip.", this); yield break; }
        yield return AcquirePresentation();
        if (!ownsPresentation) yield break;
        dragonideActor = EnsureActor(dragonide, "Dragonide Death", DragonideId, encounterPosition,
            null, dragonideCombatSetupId, dragonideRotationEuler, dragonideActor);
        if (dragonideActor == null) { Restore(); yield break; }
        Focus(dragonideActor.root.transform.position + Vector3.up * 1.3f, CameraOffset(5.5f, 1.8f));
        yield return Dialogue(dragonideDeathVoice, dragonideDeathSubtitle);
        Vector3 position = dragonideActor.root.transform.position;
        if (ringPrefab != null || sceneRingObject != null)
        {
            SpawnRing(position + ringPositionOffset);
            EnsureRingHighlight();
        }
        PlayDeath(dragonideActor, dragonideDeath, dragonide.enemyDeathDissolveMaterial);
        GameAudio.Play(dragonide.audioProfile != null ? dragonide.audioProfile.die : GameAudio.Settings.enemy?.die,
            dragonideActor.root.transform.position, GameAudioBus.Voice);
        yield return DeathFor(dragonideDeath, 1.8f);
        DestroyActor(dragonideActor); dragonideActor = null;
        session.Data.SetStoryFlag(DDeath); session.Save(false);
        Restore();
    }

    void SpawnRing(Vector3 position)
    {
        if (sceneRingObject != null)
        {
            ring = sceneRingObject;
            ring.transform.SetPositionAndRotation(position, Quaternion.identity);
            ring.SetActive(true);
            return;
        }
        if (ringPrefab == null) return;
        ring = Instantiate(ringPrefab, position, Quaternion.identity);
        ring.name = "RING"; ring.transform.localScale = Vector3.one * Mathf.Max(.01f, ringScale);
    }

    void EnsureRingHighlight()
    {
        if (ring == null) return;
        Transform glow = ring.transform.Find("Story Ring Glow");
        if (glow == null)
        {
            var glowObject = new GameObject("Story Ring Glow");
            glow = glowObject.transform;
            glow.SetParent(ring.transform, false);
            glow.localPosition = Vector3.up * .3f;
        }

        var pointLight = glow.GetComponent<Light>();
        if (pointLight == null) pointLight = glow.gameObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = ringGlowColor;
        pointLight.range = Mathf.Max(.5f, ringGlowRange);
        pointLight.intensity = Mathf.Max(0f, ringGlowIntensity);
        pointLight.shadows = LightShadows.None;
        pointLight.enabled = ringGlowIntensity > 0f;
    }

    IEnumerator CollectRingAndOpenGate()
    {
        yield return AcquirePresentation();
        if (!ownsPresentation) yield break;
        if (ring == null && (ringPrefab != null || sceneRingObject != null))
            SpawnRing(encounterPosition + ringPositionOffset);
        EnsureRingHighlight();
        if (ring != null)
        {
            Focus(ring.transform.position + Vector3.up * .5f, CameraOffset(3.2f, 1.2f));
            PlayerScript player = session.MapPlayer;
            if (player != null)
            {
                Vector3 pickup = ring.transform.position;
                pickup.y = player.transform.position.y;
                foreach (Collider itemCollider in ring.GetComponentsInChildren<Collider>(true))
                    if (itemCollider != null) itemCollider.enabled = false;

                float pickupDistance = Vector3.Distance(
                    new Vector3(player.transform.position.x, 0f, player.transform.position.z),
                    new Vector3(pickup.x, 0f, pickup.z));
                float walkTimeout = Mathf.Max(8f, pickupDistance / Mathf.Max(.1f, playerWalkSpeed) + 1f);
                yield return WalkPlayerTo(pickup, walkTimeout, .01f, true);

                Animator pickupAnimator = player.animator;
                if (!PlayState(pickupAnimator, ringPickupAnimationState))
                    Debug.LogWarning($"[MapGame story] Could not play ring pickup animation '{ringPickupAnimationState}'.", this);
                else if (pickupAnimator != null)
                {
                    int separator = ringPickupAnimationState.LastIndexOf('.');
                    int pickupLayer = separator >= 0
                        ? pickupAnimator.GetLayerIndex(ringPickupAnimationState.Substring(0, separator)) : 0;
                    if (pickupLayer < 0) pickupLayer = 0;
                    string pickupStateName = StateLeafName(ringPickupAnimationState);
                    float elapsed = 0f;
                    float timeout = Mathf.Max(.1f, ringPickupAnimationSeconds);
                    while (elapsed < timeout)
                    {
                        AnimatorStateInfo state = pickupAnimator.IsInTransition(pickupLayer)
                            ? pickupAnimator.GetNextAnimatorStateInfo(pickupLayer)
                            : pickupAnimator.GetCurrentAnimatorStateInfo(pickupLayer);
                        if (state.shortNameHash == Animator.StringToHash(pickupStateName) && state.normalizedTime >= 1f)
                            break;
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                if (pickupAnimator != null)
                {
                    if (HasParameter(pickupAnimator, "movementvalue")) pickupAnimator.SetFloat("movementvalue", 0f);
                    PlayState(pickupAnimator, playerWalkState);
                }
            }
            if (!string.IsNullOrWhiteSpace(ringItemId) && session.Config.FindItem(ringItemId) != null) session.AddItem(ringItemId, 1);
            if (ring == sceneRingObject) ring.SetActive(false);
            else Destroy(ring);
            ring = null;
        }
        session.Data.SetStoryFlag(RingTaken); session.Save(false);
        if (gate == null && gatePrefab != null)
        {
            EnsureOverlay(); SetBars(true); SetWipe(true); SetWipeProgress(1.1f);
            gate = Instantiate(gatePrefab, encounterPosition + gatePositionOffset, Quaternion.Euler(gateRotationEuler));
            gate.name = "MapGame Story Gate"; gate.transform.localScale = gateScale;
            Focus(gate.transform.position + Vector3.up * 2f, CameraOffset(6f, 2f));
            yield return RadialWipe(1.1f, -.1f, Mathf.Max(.2f, mapWipeSeconds * .55f));
            yield return new WaitForSecondsRealtime(.2f);
            yield return RadialWipe(-.1f, 1.1f, Mathf.Max(.2f, mapWipeSeconds * .45f));
            SetWipeProgress(1.1f); SetWipe(false);
        }
        else if (gatePrefab == null) Debug.LogWarning("[MapGame story] Assign the portal prefab.", this);
        Restore();
    }

    IEnumerator WalkThroughGate()
    {
        yield return AcquirePresentation();
        if (!ownsPresentation) yield break;
        Vector3 position = gate != null ? gate.transform.position : encounterPosition;
        Vector3 forward = gate != null ? gate.transform.forward : Vector3.forward; forward.y = 0;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();
        Focus(position + Vector3.up * 1.5f, CameraOffset(4.8f, 1.7f));
        yield return Dialogue(playerGateVoice, playerGateSubtitle);
        Vector3 approach = position - forward * 1.2f; approach.y = session.MapPlayer.transform.position.y;
        yield return WalkPlayerTo(approach, 4f);
        Vector3 through = position + forward * 2.4f; through.y = session.MapPlayer.transform.position.y;
        yield return WalkPlayerTo(through, 5f);
        session.Data.SetStoryFlag(Outro); session.Save(false);
        onOutroRequested?.Invoke();
        Restore();
    }

    StoryActor EnsureActor(BattleUnit prefab, string label, string id, Vector3 position, ActionData openingAction,
        string combatSetupId = "default", Vector3 rotationEuler = default, StoryActor existingActor = null)
    {
        if (prefab == null) return null;
        StoryActor actor = existingActor;
        bool created = actor == null || actor.root == null;
        BattleUnit model = null;
        if (created)
        {
            actor = new StoryActor { root = new GameObject("Map Story Encounter • " + label) };
            actor.point = actor.root.AddComponent<WorldInteraction>();
            actor.encounter = actor.root.AddComponent<EnemyEncounterBehaviour>();
            actor.modelRoot = new GameObject("Map Visual");
            actor.modelRoot.transform.SetParent(actor.root.transform, false);
            actor.modelRoot.SetActive(false);
            model = Instantiate(prefab, actor.modelRoot.transform);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            EnemyEncounterBehaviour.PrepareModel(model.gameObject);
            actor.animator = model.animator != null ? model.animator : model.GetComponentInChildren<Animator>(true);
        }

        actor.root.name = "Map Story Encounter • " + label;
        actor.root.transform.SetPositionAndRotation(position, Quaternion.Euler(rotationEuler));
        actor.point.kind = WorldInteractionKind.Encounter; actor.point.displayName = label;
        actor.point.persistentId = id; actor.point.savedWorldId = id; actor.point.range = Mathf.Max(2.5f, encounterRange);
        actor.point.combatSetupId = string.IsNullOrWhiteSpace(combatSetupId) ? "default" : combatSetupId.Trim();
        actor.point.experience = 0; actor.point.enemyPrefabs = new List<BattleUnit> { prefab };
        actor.point.enemySpawn.respawn = false;
        actor.point.openingEnemyAction = openingAction; actor.point.enabled = false;
        var origin = actor.root.GetComponent<EnemyCombatOrigin>();
        if (origin == null) origin = actor.root.AddComponent<EnemyCombatOrigin>();
        origin.SetSpawnPoint(position, actor.root.transform.rotation);
        actor.encounter.mapAnimator = actor.animator;
        actor.encounter.travelMode = EnemyEncounterBehaviour.TravelMode.Flying; actor.encounter.flightHeight = 0;
        actor.encounter.attackRange = actor.point.range; actor.encounter.obstacleLayers = 0;
        actor.encounter.enabled = false;
        if (created)
        {
            actor.encounter.enabled = true;
            model.gameObject.SetActive(true);
        }
        actor.modelRoot.gameObject.SetActive(true);
        actor.encounter.enabled = false;
        if (actor.animator != null)
        {
            actor.animator.enabled = true; actor.animator.applyRootMotion = false; actor.animator.fireEvents = false;
            actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            int movementLayer = actor.animator.GetLayerIndex(actor.encounter.movementLayer);
            if (movementLayer >= 0) actor.animator.SetLayerWeight(movementLayer, 1f);
            foreach (var skin in actor.animator.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
        }
        return actor;
    }

    IEnumerator BeginCombat(StoryActor actor)
    {
        if (actor == null || session == null || !session.OnMap) { Restore(); yield break; }
        actor.point.transform.position = encounterPosition;
        actor.point.GetComponent<EnemyCombatOrigin>()?.SetSpawnPoint(encounterPosition, actor.point.transform.rotation);
        session.EndPresentation(this); ownsPresentation = false;
        session.BeginStoryEncounter(actor.point);
        if (!session.InEncounter)
        {
            Debug.LogWarning("[MapGame story] Encounter could not start; it will retry shortly.", this);
            Restore(); yield return new WaitForSecondsRealtime(1f); yield break;
        }
        while (isActiveAndEnabled && session != null && session.OnMap && session.InEncounter && !LoadingScreen.IsLoading) yield return null;
    }

    IEnumerator AcquirePresentation()
    {
        ownsPresentation = false;
        while (isActiveAndEnabled && session != null && session.OnMap)
        {
            if (session.TryBeginPresentation(this)) { ownsPresentation = true; break; }
            yield return null;
        }
        if (ownsPresentation) { EnsureOverlay(); SetBars(true); EnsureStoryCamera(); }
    }

    IEnumerator Dialogue(GameSound voice, string text)
    {
        EnsureOverlay();
        bool show = !string.IsNullOrWhiteSpace(text); subtitlePanel.gameObject.SetActive(show);
        if (show) subtitle.text = text;
        AudioClip clip = voice != null ? voice.Pick() : null;
        if (clip != null) GameAudio.PlayClipAt(clip, session.MapPlayer.transform.position, voice.volume, GameAudioBus.Voice, voice.spatialBlend);
        yield return new WaitForSecondsRealtime(Mathf.Max(dialogueFallbackSeconds, clip != null ? clip.length : 0));
        subtitlePanel.gameObject.SetActive(false);
    }

    IEnumerator WalkPlayerTo(Vector3 target, float timeout, float stoppingDistance = .3f, bool snapToTarget = false)
    {
        PlayerScript player = session != null ? session.MapPlayer : null;
        if (player == null) yield break;
        Vector3 delta = target - player.transform.position; delta.y = 0;
        if (delta.sqrMagnitude < .16f && !snapToTarget) yield break;
        Face(delta); Animator animator = player.animator; float oldSpeed = animator != null ? animator.speed : 1;
        if (animator != null)
        {
            animator.speed = 1; PlayState(animator, playerWalkState);
            if (HasParameter(animator, "movementvalue")) animator.SetFloat("movementvalue", playerWalkAnimValue);
        }
        float elapsed = 0;
        float stopSqr = Mathf.Max(0f, stoppingDistance) * Mathf.Max(0f, stoppingDistance);
        while (isActiveAndEnabled && elapsed < timeout)
        {
            Vector3 remaining = target - player.transform.position; remaining.y = 0;
            if (remaining.sqrMagnitude <= stopSqr) break;
            Vector3 step = remaining.normalized * Mathf.Max(.1f, playerWalkSpeed) * Time.unscaledDeltaTime;
            if (step.sqrMagnitude > remaining.sqrMagnitude) step = remaining;
            if (player.CC != null && player.CC.enabled) player.CC.Move(step); else player.transform.position += step;
            Focus(player.transform.position + Vector3.up * 1.2f, CameraOffset(5f, 1.8f));
            elapsed += Time.unscaledDeltaTime; yield return null;
        }
        if (snapToTarget)
        {
            Vector3 finalPosition = target;
            finalPosition.y = player.transform.position.y;
            if (player.CC != null && player.CC.enabled)
                player.CC.Move(finalPosition - player.transform.position);
            player.transform.position = finalPosition;
        }
        if (animator != null) { if (HasParameter(animator, "movementvalue")) animator.SetFloat("movementvalue", 0); animator.speed = oldSpeed; }
    }

    void Face(Vector3 direction)
    {
        direction.y = 0;
        if (session?.MapPlayer != null && direction.sqrMagnitude > .001f)
            session.MapPlayer.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    static bool HasParameter(Animator animator, string name)
    {
        foreach (var p in animator.parameters) if (p.name == name) return true;
        return false;
    }

    static bool PlayState(Animator animator, string path)
    {
        if (animator == null || string.IsNullOrWhiteSpace(path) || animator.runtimeAnimatorController == null) return false;
        int dot = path.LastIndexOf('.'); string layerName = dot >= 0 ? path.Substring(0, dot) : "";
        string stateName = dot >= 0 ? path.Substring(dot + 1) : path; int layer = 0;
        if (!string.IsNullOrEmpty(layerName)) { layer = animator.GetLayerIndex(layerName); if (layer < 0) return false; }
        int hash = Animator.StringToHash((layerName.Length > 0 ? layerName + "." : "") + stateName);
        if (!animator.HasState(layer, hash)) hash = Animator.StringToHash(stateName);
        if (!animator.HasState(layer, hash))
        {
            Debug.LogWarning($"[{animator.name}] Animation state '{path}' was not found in controller '{animator.runtimeAnimatorController.name}'.", animator);
            return false;
        }
        animator.enabled = true;
        if (layer > 0) animator.SetLayerWeight(layer, 1f);
        animator.CrossFadeInFixedTime(hash, .14f, layer, 0); return true;
    }

    void PlayDeath(StoryActor actor, AnimationClip clip, Material material)
    {
        EndDeath(); if (actor == null || actor.animator == null || clip == null) return;
        actor.animator.Rebind(); actor.animator.Update(0); actor.animator.applyRootMotion = false; actor.animator.fireEvents = false;
        deathGraph = PlayableGraph.Create("MapGame story enemy death"); deathGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var playable = AnimationClipPlayable.Create(deathGraph, clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
        var output = AnimationPlayableOutput.Create(deathGraph, "Death", actor.animator); output.SetSourcePlayable(playable); deathGraph.Play();
        if (material != null) dissolve = new MapDeathDissolve(actor.animator.transform, material);
        SampleDeathAt(clip, 0);
    }

    IEnumerator DeathFor(AnimationClip clip, float minimum)
    {
        float duration = Mathf.Max(minimum, clip != null ? clip.length : 0), elapsed = 0;
        while (elapsed < duration) { SampleDeathAt(clip, elapsed / duration); elapsed += Time.unscaledDeltaTime; yield return null; }
        SampleDeathAt(clip, 1); yield return new WaitForSecondsRealtime(.25f); EndDeath();
    }

    void SampleDeathAt(AnimationClip clip, float normalized)
    {
        if (deathGraph.IsValid() && clip != null) { deathGraph.GetRootPlayable(0).SetTime(Mathf.Clamp01(normalized) * clip.length); deathGraph.Evaluate(0); }
        dissolve?.Sample(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, normalized)));
    }

    void EndDeath() { if (deathGraph.IsValid()) deathGraph.Destroy(); dissolve?.Dispose(); dissolve = null; }
    void DestroyActor(StoryActor actor) { if (actor?.root != null) Destroy(actor.root); }

    void SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float life)
    {
        if (prefab == null) return;
        GameObject effect = Instantiate(prefab, position, rotation); ConfigureEffect(effect, 1);
        Destroy(effect, Mathf.Max(.2f, life));
    }

    static void ConfigureEffect(GameObject effect, float speed)
    {
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        { var main = ps.main; main.useUnscaledTime = true; main.loop = false; main.simulationSpeed = speed > 0 ? speed : 1; ps.Play(true); }
    }

    Vector3 CameraOffset(float distance, float height)
    {
        Vector3 back = Camera.main != null ? -Camera.main.transform.forward : Vector3.back; back.y = 0;
        if (back.sqrMagnitude < .001f) back = Vector3.back;
        return back.normalized * distance + Vector3.up * height;
    }

    static string StateLeafName(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        int dot = path.LastIndexOf('.');
        return dot >= 0 ? path.Substring(dot + 1) : path;
    }

    void CacheMountainCameraBack()
    {
        Camera view = Camera.main;
        if (view == null) return;
        Vector3 back = -view.transform.forward;
        back.y = 0;
        if (back.sqrMagnitude < .001f) back = Vector3.back;
        mountainCameraBack = back.normalized;
        hasMountainCameraBack = true;
    }

    void CacheMountainActorBounds()
    {
        hasMountainFramingBounds = false;
        mountainFramingActor = null;
        if (mountainActor?.root == null || !TryGetActorBounds(mountainActor, out Bounds worldBounds)) return;

        Transform root = mountainActor.root.transform;
        Vector3 min = worldBounds.min, max = worldBounds.max;
        bool found = false;
        Bounds localBounds = default;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 worldPoint = new Vector3(
                (corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y,
                (corner & 4) == 0 ? min.z : max.z);
            Vector3 localPoint = root.InverseTransformPoint(worldPoint);
            if (!found) { localBounds = new Bounds(localPoint, Vector3.zero); found = true; }
            else localBounds.Encapsulate(localPoint);
        }

        if (!found) return;
        mountainFramingActor = mountainActor;
        mountainFramingLocalBounds = localBounds;
        hasMountainFramingBounds = true;
    }

    bool TryGetMountainActorBounds(out Bounds bounds)
    {
        bounds = default;
        if (!hasMountainFramingBounds || mountainFramingActor?.root == null) return false;
        Transform root = mountainFramingActor.root.transform;
        Vector3 min = mountainFramingLocalBounds.min, max = mountainFramingLocalBounds.max;
        bool found = false;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 localPoint = new Vector3(
                (corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y,
                (corner & 4) == 0 ? min.z : max.z);
            Vector3 worldPoint = root.TransformPoint(localPoint);
            if (!found) { bounds = new Bounds(worldPoint, Vector3.zero); found = true; }
            else bounds.Encapsulate(worldPoint);
        }
        return found;
    }

    Vector3 MountainCameraOffset(float distance, float height)
    {
        Vector3 back = hasMountainCameraBack ? mountainCameraBack : Vector3.back;
        return back * distance + Vector3.up * height;
    }

    void EnsureStoryCamera()
    {
        if (storyCamera == null)
        {
            var cameraObject = new GameObject("MapGame Story Camera");
            cameraObject.transform.SetParent(transform, false);
            storyCamera = cameraObject.AddComponent<CinemachineCamera>();
            storyCamera.Priority = -100;
            storyCameraDefaultFieldOfView = storyCamera.Lens.FieldOfView;
            hasStoryCameraDefaultFieldOfView = true;
        }
        storyCamera.enabled = true; storyCamera.Priority = 150;
    }

    void Focus(Vector3 target, Vector3 offset)
    {
        if (storyCamera == null) return;
        Vector3 position = target + offset;
        Vector3 direction = target - position;
        Quaternion rotation = direction.sqrMagnitude > .001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : storyCamera.transform.rotation;

        storyCamera.transform.position = position;
        direction = target - position;
        if (direction.sqrMagnitude > .001f) storyCamera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    void FocusMountainActors()
    {
        if (TryGetMountainActorBounds(out Bounds bounds) || TryGetActorBounds(mountainActor, out bounds))
        {
            if (TryGetActorBounds(dragonideActor, out Bounds dragonideBounds)) bounds.Encapsulate(dragonideBounds);
            FocusMountainStory(bounds.center, bounds);
            return;
        }
        if (mountainActor != null && mountainActor.root != null)
            FocusMountainStory(mountainActor.root.transform.position + Vector3.up * 1.5f);
    }

    void FocusFireballPoint(Vector3 point)
    {
        EnsureStoryCamera();
        var lens = storyCamera.Lens;
        lens.FieldOfView = Mathf.Clamp(mountainCameraFieldOfView, 25f, 85f);
        storyCamera.Lens = lens;
        Focus(point, MountainCameraOffset(Mathf.Max(2f, mountainFireballCameraDistance), mountainFireballCameraElevation));
    }

    void FocusMountainStory(Vector3 target, Bounds? framingBounds = null)
    {
        EnsureStoryCamera();
        var lens = storyCamera.Lens;
        lens.FieldOfView = Mathf.Clamp(mountainCameraFieldOfView, 25f, 85f);
        storyCamera.Lens = lens;

        Bounds bounds;
        bool hasBounds = framingBounds.HasValue;
        if (hasBounds) bounds = framingBounds.Value;
        else
        {
            hasBounds = TryGetMountainActorBounds(out bounds) || TryGetActorBounds(mountainActor, out bounds);
            if (hasBounds && TryGetActorBounds(dragonideActor, out Bounds dragonideBounds)) bounds.Encapsulate(dragonideBounds);
        }

        float distance = Mathf.Max(0f, mountainCameraMinimumDistance);
        if (hasBounds)
        {
            float tangent = Mathf.Tan(lens.FieldOfView * .5f * Mathf.Deg2Rad);
            float aspect = Camera.main != null ? Mathf.Max(.1f, Camera.main.aspect) : 16f / 9f;
            float fitHeight = bounds.size.y / Mathf.Max(.01f, tangent * 2f);
            float horizontalDiameter = Mathf.Sqrt(bounds.size.x * bounds.size.x + bounds.size.z * bounds.size.z);
            float fitWidth = horizontalDiameter / Mathf.Max(.01f, tangent * 2f * aspect);
            distance = Mathf.Max(distance, Mathf.Max(fitHeight, fitWidth) * Mathf.Max(1f, mountainCameraFramingPadding));
        }
        Focus(target, MountainCameraOffset(distance, mountainCameraElevation));
    }

    static bool TryGetActorBounds(StoryActor actor, out Bounds bounds)
    {
        bounds = default;
        if (actor == null || actor.modelRoot == null) return false;
        var renderers = actor.modelRoot.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (renderer == null || (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))) continue;
            Bounds local = renderer.localBounds;
            Vector3 min = local.min, max = local.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 localPoint = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 worldPoint = renderer.transform.TransformPoint(localPoint);
                if (!found) { bounds = new Bounds(worldPoint, Vector3.zero); found = true; }
                else bounds.Encapsulate(worldPoint);
            }
        }
        return found;
    }

    void EnsureOverlay()
    {
        if (canvas != null) return;
        var root = new GameObject("MapGame Story Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false); canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32600;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        topBar = MakeImage("Cinematic Bar Top", root.transform, Color.black);
        Anchors(topBar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -92), Vector2.zero);
        bottomBar = MakeImage("Cinematic Bar Bottom", root.transform, Color.black);
        Anchors(bottomBar.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 92));
        subtitlePanel = MakeImage("Dialogue Subtitle Panel", root.transform, new Color(.005f, .005f, .008f, .84f));
        Anchors(subtitlePanel.rectTransform, new Vector2(.12f, .045f), new Vector2(.88f, .17f), Vector2.zero, Vector2.zero);
        var label = new GameObject("Dialogue Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(subtitlePanel.transform, false);
        var rect = label.GetComponent<RectTransform>(); Anchors(rect, Vector2.zero, Vector2.one, new Vector2(24, 10), new Vector2(-24, -10));
        subtitle = label.GetComponent<TextMeshProUGUI>(); subtitle.alignment = TextAlignmentOptions.Center; subtitle.fontSize = 30;
        subtitle.color = new Color(.95f, .92f, .86f); subtitle.enableAutoSizing = true; subtitle.fontSizeMin = 20; subtitle.fontSizeMax = 30;
        wipe = MakeImage("Radial Map Dissolve", root.transform, Color.black); Anchors(wipe.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Shader shader = Shader.Find("UI/MapGameStoryRadialWipe");
        if (shader != null) { wipeMaterial = new Material(shader); wipe.material = wipeMaterial; }
        subtitlePanel.gameObject.SetActive(false); wipe.gameObject.SetActive(false); SetBars(false);
    }

    static Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }

    static void Anchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }

    void SetBars(bool value)
    { if (canvas != null) { topBar.gameObject.SetActive(value); bottomBar.gameObject.SetActive(value); } }
    void SetWipe(bool value) { if (wipe != null) wipe.gameObject.SetActive(value); }
    void SetWipeProgress(float value)
    {
        if (wipeMaterial != null) wipeMaterial.SetFloat(ProgressId, value);
        else if (wipe != null) { var c = wipe.color; c.a = Mathf.Clamp01(1 - value); wipe.color = c; }
    }

    IEnumerator RadialWipe(float from, float to, float duration)
    {
        SetWipe(true);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        { SetWipeProgress(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / duration))); yield return null; }
        SetWipeProgress(to);
    }

    void Restore()
    {
        hasMountainFramingBounds = false;
        mountainFramingActor = null;
        hasMountainCameraBack = false;
        EndDeath();
        if (storyCamera != null)
        {
            if (hasStoryCameraDefaultFieldOfView)
            {
                var lens = storyCamera.Lens;
                lens.FieldOfView = storyCameraDefaultFieldOfView;
                storyCamera.Lens = lens;
            }
            storyCamera.Priority = -100; storyCamera.enabled = false;
        }
        if (subtitlePanel != null) subtitlePanel.gameObject.SetActive(false);
        SetWipe(false); SetBars(false);
        if (ownsPresentation) session?.EndPresentation(this);
        ownsPresentation = false;
    }

    void OnDisable() { StopAllCoroutines(); Restore(); }
    void OnDestroy() { EndDeath(); if (wipeMaterial != null) Destroy(wipeMaterial); }
    void OnDrawGizmos()
    {
        Vector3 center = triggerPosition;
        float radius = Mathf.Max(.5f, triggerRadius);
        float halfHeight = Mathf.Max(.5f, triggerHeightTolerance);
        Color previous = Gizmos.color;

        // The runtime trigger is a horizontal radius plus a Y tolerance. Draw that
        // cylinder in Edit Mode even when the setup object is not selected.
        Gizmos.color = new Color(.12f, .85f, 1f, .95f);
        DrawTriggerCircle(center, radius, -halfHeight, 32);
        DrawTriggerCircle(center, radius, halfHeight, 32);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            Vector3 edge = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(center + edge - Vector3.up * halfHeight, center + edge + Vector3.up * halfHeight);
        }
        Gizmos.DrawWireSphere(center, .3f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(encounterPosition, .65f);
        Gizmos.color = new Color(1f, .35f, .1f, .95f);
        Gizmos.DrawLine(mountainDragonStartPosition, encounterPosition);
        Gizmos.DrawWireSphere(mountainDragonStartPosition, .45f);
        Gizmos.color = previous;

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(.12f, .85f, 1f, 1f);
        UnityEditor.Handles.Label(center + Vector3.up * (halfHeight + .5f),
            $"QUEST {requiredQuestId} • STORY TRIGGER\nR {radius:0.##}m / H ±{halfHeight:0.##}m");
        UnityEditor.Handles.color = Color.white;
#endif
    }

    void DrawTriggerCircle(Vector3 center, float radius, float yOffset, int segments)
    {
        Vector3 previous = center + new Vector3(radius, yOffset, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, yOffset, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
