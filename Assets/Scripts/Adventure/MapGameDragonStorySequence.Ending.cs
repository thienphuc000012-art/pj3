using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed partial class MapGameDragonStorySequence
{
    [Serializable]
    public sealed class EndingGuest
    {
        public GameObject model;
        public string speakerName;
        public string runState = "Base Layer.Run";
        public string idleState = "Base Layer.IdleA";
        public string greetingState = "Base Layer.Hi";
        [Min(.1f)] public float scale = 1.25f;
        [Tooltip("Chiều cao tối thiểu của phụ đề tính từ chân NPC.")]
        [Min(.1f)] public float captionHeight = 2.35f;
        [TextArea] public string line;
        public GameSound voice = new GameSound { spatialBlend = 0f, volume = 1f };
    }

    [Header("Ending staging")]
    [Min(.1f)] public float ringShakeSeconds = .55f;
    [Min(0f)] public float ringShakeAmplitude = .12f;
    [Min(.1f)] public float endingWalkSpeed = 1.05f;
    [Min(1f)] public float endingGateLeadIn = 3.5f;
    [Min(0f), Tooltip("Khoảng player đi thêm qua mặt cổng trước khi hiện outro, tính bằng mét.")]
    public float endingGateExitDistance = .15f;
    [Tooltip("Chiều cao tối đa của ánh sáng Plane001, tính theo chiều cao nhân vật.")]
    [Min(1f)] public float endingGateEnergyHeightMultiplier = 1.25f;
    [Min(1f)] public float endingNpcRunSpeed = 3.2f;
    [Header("Ending chunk dissolve")]
    public Shader chunkDissolveShader;
    [Min(.5f)] public float chunkDissolveSeconds = 9f;
    [Min(8f)] public float chunkDissolveKeepRadius = 12f;
    [Header("Ending guests")]
    [Tooltip("Khoảng cách từ xương đầu đến phụ đề của NPC.")]
    [Min(0f)] public float captionAboveHead = .72f;
    [TextArea] public string mainQuestCall = "Khoan đã, lữ khách! Đợi tôi với!";
    public GameSound mainQuestCallVoice = new GameSound { spatialBlend = 0f, volume = 1f };
    public EndingGuest mainQuestGuest = new EndingGuest();
    public AdventureQuest shelterQuest, cookingQuest;
    public EndingGuest shelterGuest = new EndingGuest();
    public EndingGuest cookingGuest = new EndingGuest();
    public UnityEngine.Timeline.TimelineAsset outroTimeline;
    [Header("Finale music")]
    [Tooltip("Nhạc lúc mở cutscene boss và trong cả hai trận MountainDragon / Dragonide.")]
    public GameMusic finaleBossMusic = new GameMusic();
    [Tooltip("Nhạc khi hiện màn thắng và trong cutscene sau mỗi trận boss cuối.")]
    public GameMusic finaleVictoryMusic = new GameMusic();
    [Tooltip("Nhạc bắt đầu khi phát EndingOutro.")]
    public GameMusic outroMusic = new GameMusic();

    readonly List<GameObject> endingActors = new List<GameObject>(3);
    MapEndingChunkDissolve endingDissolve;
    MapChunkStreamer endingStreamer;
    TMP_Text endingCaption;
    MapEndingCreditsView creditsView;

    IEnumerator PresentEnding()
    {
        yield return AcquirePresentation(finaleVictoryMusic);
        if (!ownsPresentation) yield break;
        try
        {
            if (session.MapPlayer == null || gatePrefab == null || outroTimeline == null)
            {
                Debug.LogError("[MapGame ending] Assign the gate prefab and outro timeline, and bind the map player.", this);
                yield break;
            }

            bool bothSideQuestsComplete = IsEndingQuestComplete(shelterQuest) && IsEndingQuestComplete(cookingQuest);
            if (!session.Data.HasStoryFlag(RingTaken))
                yield return CollectRing();
            else if (ring != null)
                ring.SetActive(false);

            Transform player = session.MapPlayer.transform;
            Vector3 ringFloor = player.position;
            Vector3 gateCenter = encounterPosition + gatePositionOffset;
            Quaternion gateRotation = Quaternion.Euler(gateRotationEuler);
            Vector3 passage = Vector3.ProjectOnPlane(gateRotation * Vector3.forward, Vector3.up).normalized;
            if (passage.sqrMagnitude < .001f) passage = Vector3.forward;
            if (Vector3.Dot(gateCenter - ringFloor, passage) < 0f) passage = -passage;
            float leadIn = Vector3.Dot(gateCenter - ringFloor, passage);
            gateCenter += passage * Mathf.Max(0f, endingGateLeadIn - leadIn);
            Vector3 side = Vector3.Cross(Vector3.up, passage);
            Vector3 approach = new Vector3(gateCenter.x, ringFloor.y, gateCenter.z) - passage * 1.45f;
            Vector3 through = new Vector3(gateCenter.x, ringFloor.y, gateCenter.z) +
                passage * Mathf.Max(0f, endingGateExitDistance);
            Vector3 walkCamera = -passage * 4.8f + side * 2f + Vector3.up * 1.4f;

            yield return ShakeOnRingPickup(ringFloor + Vector3.up, walkCamera);
            gate = Instantiate(gatePrefab, gateCenter, gateRotation, transform);
            gate.name = "MapGame Story Gate";
            gate.transform.localScale = gateScale;
            float floorHeight = GuestGround(approach + passage * 1.45f).y;
            var playerCollider = session.MapPlayer.CC;
            float playerHeight = playerCollider != null
                ? playerCollider.height * Mathf.Abs(playerCollider.transform.lossyScale.y) : 1.8f;
            AlignGateOpening(gate, gateCenter, floorHeight, playerHeight * endingGateEnergyHeightMultiplier);
            foreach (var collider in gate.GetComponentsInChildren<Collider>())
                collider.enabled = false;

            Focus(Vector3.Lerp(ringFloor, approach, .75f) + Vector3.up * 1.8f,
                -passage * 8f + side * 5f + Vector3.up * 3.2f);
            yield return RevealEndingDissolve(approach);

            yield return WalkEndingPath(approach, walkCamera);

            // Start the call off-screen, then follow the main quest NPC as they run up.
            Vector3 meeting = player.position;
            var actors = new List<(EndingGuest guest, GameObject actor, Vector3 destination)>(3);
            AddEndingGuest(actors, mainQuestGuest, meeting, passage, side, 0f);
            if (bothSideQuestsComplete)
            {
                AddEndingGuest(actors, shelterGuest, meeting, passage, side, -1.9f);
                AddEndingGuest(actors, cookingGuest, meeting, passage, side, 1.9f);
            }

            if (actors.Count > 0)
            {
                AudioClip callClip = mainQuestCallVoice?.Pick();
                float callStarted = Time.unscaledTime;
                if (callClip != null)
                    GameAudio.PlayClipAt(callClip, actors[0].actor.transform.position,
                        mainQuestCallVoice.volume, GameAudioBus.Voice, mainQuestCallVoice.spatialBlend);
                SetEndingCallSubtitle(true);
                yield return new WaitForSecondsRealtime(.65f);
                Face(actors[0].actor.transform.position - player.position);
                yield return RunEndingGuests(actors, player, passage, side);
                float remainingCall = callClip != null ? callClip.length - (Time.unscaledTime - callStarted) : 0f;
                if (remainingCall > 0f) yield return new WaitForSecondsRealtime(remainingCall);
                SetEndingCallSubtitle(false);

                foreach (var entry in actors)
                    yield return ShowEndingGreeting(entry.guest, entry.actor, player, passage, side);
            }

            Focus(player.position + Vector3.up * 1.2f, passage * 2.7f + side * 1.65f + Vector3.up * .3f);
            var animator = session.MapPlayer.animator;
            PlayState(animator, "Base Layer.Talking");
            yield return Dialogue(playerGateVoice, playerGateSubtitle);
            PlayState(animator, playerWalkState);
            yield return WalkEndingPath(through, walkCamera);

            if (outroMusic?.clip != null) GameAudio.Instance?.SetSceneMusic(this, outroMusic);
            onOutroRequested?.Invoke();
            creditsView = new GameObject("Ending Outro").AddComponent<MapEndingCreditsView>();
            creditsView.transform.SetParent(transform, false);
            yield return creditsView.Play(outroTimeline);

            session.Data.SetStoryFlag(Outro);
            session.Save(false);
            session.ReturnToTitleFromEnding(this);
        }
        finally
        {
            Restore();
        }
    }

    bool IsEndingQuestComplete(AdventureQuest quest) =>
        quest != null && session.QuestProgress(quest.Id)?.completed == true;

    static void AlignGateOpening(GameObject portal, Vector3 crossing, float floorHeight, float maxEnergyHeight)
    {
        var visual = portal.GetComponent<PortalArrivalVisual>();
        if (visual == null || visual.energySurfaces.Length == 0 || visual.energySurfaces[0] == null) return;
        var energy = visual.energySurfaces[0];
        Bounds opening = energy.bounds;
        portal.transform.position += new Vector3(crossing.x - opening.center.x,
            floorHeight - opening.min.y, crossing.z - opening.center.z);

        if (maxEnergyHeight <= 0f || opening.size.y <= maxEnergyHeight) return;
        energy.transform.localScale *= maxEnergyHeight / opening.size.y;
        Bounds resized = energy.bounds;
        energy.transform.position += new Vector3(crossing.x - resized.center.x,
            floorHeight - resized.min.y, crossing.z - resized.center.z);
    }

    IEnumerator ShakeOnRingPickup(Vector3 focus, Vector3 offset)
    {
        float duration = Mathf.Max(.1f, ringShakeSeconds);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float fade = Mathf.Pow(1f - elapsed / duration, 2f);
            Vector3 shake = storyCamera.transform.right * Mathf.Sin(elapsed * 47f) +
                Vector3.up * (.65f * Mathf.Sin(elapsed * 59f));
            Focus(focus, offset + shake * (ringShakeAmplitude * fade));
            yield return null;
        }
        Focus(focus, offset);
    }

    IEnumerator RevealEndingDissolve(Vector3 center)
    {
        endingStreamer = MapChunkStreamer.Instance;
        if (endingStreamer == null) yield break;
        yield return endingStreamer.PinLoadedChunksForEnding();
        endingDissolve = new MapEndingChunkDissolve(endingStreamer, chunkDissolveShader, center, chunkDissolveKeepRadius);
        float duration = Mathf.Max(.5f, chunkDissolveSeconds);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            endingDissolve.Sample(Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        endingDissolve.Sample(1f);
    }

    IEnumerator WalkEndingPath(Vector3 target, Vector3 offset)
    {
        float speed = Mathf.Max(.1f, endingWalkSpeed);
        float duration = Vector3.Distance(session.MapPlayer.transform.position, target) / speed + .5f;
        yield return WalkPlayerTo(target, duration, .015f, speedOverride: speed, cameraOffset: offset, scriptedPath: true);
    }

    void AddEndingGuest(List<(EndingGuest guest, GameObject actor, Vector3 destination)> actors,
        EndingGuest guest, Vector3 meeting, Vector3 passage, Vector3 side, float lateral)
    {
        if (guest?.model == null) return;
        Vector3 destination = meeting - passage * (lateral == 0f ? 2.15f : 2.85f) + side * lateral;
        Vector3 start = destination - passage * 4.8f;
        var actor = Instantiate(guest.model, GuestGround(start), Quaternion.LookRotation(passage), transform);
        actor.name = "Ending NPC - " + guest.speakerName;
        actor.transform.localScale *= guest.scale;
        endingActors.Add(actor);
        foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var animator = actor.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PlayState(animator, guest.idleState);
        }
        actors.Add((guest, actor, destination));
    }

    Vector3 GuestGround(Vector3 position)
    {
        var player = session.MapPlayer;
        int mask = player.surfaceLayer.value != 0 ? player.surfaceLayer.value : Physics.DefaultRaycastLayers;
        if (Physics.Raycast(position + Vector3.up * .8f, Vector3.down, out var hit, 1.8f, mask, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y;
        return position;
    }

    IEnumerator RunEndingGuests(List<(EndingGuest guest, GameObject actor, Vector3 destination)> actors,
        Transform player, Vector3 passage, Vector3 side)
    {
        var starts = new Vector3[actors.Count];
        for (int i = 0; i < actors.Count; i++)
        {
            starts[i] = actors[i].destination - passage * 4.8f;
            PlayState(actors[i].actor.GetComponentInChildren<Animator>(), actors[i].guest.runState);
        }
        float duration = 4.8f / Mathf.Max(1f, endingNpcRunSpeed);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < actors.Count; i++)
                actors[i].actor.transform.position = GuestGround(Vector3.Lerp(starts[i], actors[i].destination, elapsed / duration));
            Focus(actors[0].actor.transform.position + Vector3.up * 1.05f,
                passage * 3.6f + side * 2.4f + Vector3.up * .65f);
            yield return null;
        }
        foreach (var entry in actors)
        {
            entry.actor.transform.position = GuestGround(entry.destination);
            var look = Vector3.ProjectOnPlane(player.position - entry.actor.transform.position, Vector3.up);
            entry.actor.transform.rotation = Quaternion.LookRotation(look);
            PlayState(entry.actor.GetComponentInChildren<Animator>(), entry.guest.idleState);
        }
    }

    void SetEndingCallSubtitle(bool visible)
    {
        subtitlePanel.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(mainQuestCall));
        if (visible) subtitle.text = mainQuestCall;
    }

    IEnumerator ShowEndingGreeting(EndingGuest guest, GameObject actor, Transform player, Vector3 passage, Vector3 side)
    {
        Face(actor.transform.position - player.position);
        var animator = actor.GetComponentInChildren<Animator>();
        PlayState(animator, guest.greetingState);
        Focus(actor.transform.position + Vector3.up * 1.55f, passage * 4.15f + side * 1.85f + Vector3.up * .6f);
        if (endingCaption == null)
        {
            var caption = new GameObject("Ending NPC Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
            caption.transform.SetParent(canvas.transform, false);
            endingCaption = caption.GetComponent<TextMeshProUGUI>();
            endingCaption.rectTransform.sizeDelta = new Vector2(490f, 156f);
            endingCaption.rectTransform.pivot = new Vector2(.5f, 0f);
            endingCaption.alignment = TextAlignmentOptions.Bottom;
            endingCaption.font = Resources.Load<TMP_FontAsset>("Adventure/UI/OptionsFont") ?? subtitle.font;
            endingCaption.fontSize = 28f;
            endingCaption.raycastTarget = false;
        }
        endingCaption.text = "<color=#FFDA97>" + guest.speakerName + "</color>\n" + guest.line;
        var canvasRect = (RectTransform)canvas.transform;
        float captionWidth = Mathf.Min(490f, Mathf.Max(1f, canvasRect.rect.width - 48f));
        float captionTextHeight = endingCaption.GetPreferredValues(endingCaption.text, captionWidth, Mathf.Infinity).y;
        endingCaption.rectTransform.sizeDelta = new Vector2(captionWidth, captionTextHeight);
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        AudioClip clip = guest.voice?.Pick();
        if (clip != null)
            GameAudio.PlayClipAt(clip, actor.transform.position, guest.voice.volume, GameAudioBus.Voice, guest.voice.spatialBlend);
        float duration = Mathf.Max(2.5f, (guest.line?.Length ?? 0) / 25f, clip != null ? clip.length : 0f);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            var camera = Camera.main;
            if (camera != null)
            {
                Vector3 anchor = actor.transform.position + Vector3.up * guest.captionHeight;
                if (head != null)
                {
                    anchor.x = head.position.x;
                    anchor.z = head.position.z;
                    anchor.y = Mathf.Max(anchor.y, head.position.y + captionAboveHead);
                }
                Vector3 screen = camera.WorldToScreenPoint(anchor);
                endingCaption.gameObject.SetActive(screen.z > 0f);
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screen, null, out Vector2 local))
                {
                    Rect frame = canvasRect.rect;
                    float sidePadding = captionWidth * .5f + 16f;
                    local.x = Mathf.Clamp(local.x, frame.xMin + sidePadding, frame.xMax - sidePadding);
                    local.y = Mathf.Min(local.y, frame.yMax - topBar.rectTransform.rect.height - captionTextHeight - 12f);
                    endingCaption.rectTransform.anchoredPosition = local;
                }
            }
            yield return null;
        }
        endingCaption.gameObject.SetActive(false);
        PlayState(animator, guest.idleState);
    }

    void RestoreEnding()
    {
        endingDissolve?.Dispose();
        endingDissolve = null;
        if (endingStreamer != null) endingStreamer.ReleaseEndingChunks();
        endingStreamer = null;
        foreach (var actor in endingActors) if (actor != null) Destroy(actor);
        endingActors.Clear();
        if (endingCaption != null) endingCaption.gameObject.SetActive(false);
        if (creditsView != null) Destroy(creditsView.gameObject);
        creditsView = null;
        if (gate != null) Destroy(gate);
        gate = null;
    }
}
