using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public enum ExplorationTutorialAction { Movement = 0, Scan = 1, Menu = 2, Parkour = 3, Interact = 4, Encounter = 6 }

/// <summary>Only scene m: portal arrival, a short panorama, then hands-on lessons on existing gameplay systems.</summary>
[DefaultExecutionOrder(200)]
public sealed class MExplorationTutorial : MonoBehaviour
{
    public const string ProgressId = "m.portal-and-exploration.v1";
    public const int LessonCount = 7;
    public const int CurriculumVersion = 2;
    [Header("Existing systems")]
    public MapGameSetup mapSetup;
    public PlayerScript player;
    public PortalArrivalVisual portal;
    public CinemachineCamera cinematicCamera;
    public CinemachineCamera gameplayCamera;
    public Collider parkourObstacle;
    public WorldInteraction campfire;
    [Tooltip("Enemy được đánh dấu trong hint cuối. Hoàn tất khi chạm trán đúng Enemy này, bằng E/chuột trái hoặc bị Enemy tấn công.")]
    public WorldInteraction encounterEnemy;
    [Header("Arrival poses")]
    public Transform walkStart;
    public Transform walkExit;
    public Transform walkCameraStart;
    public Transform walkCameraEnd;
    public Transform[] panorama = Array.Empty<Transform>();
    [Min(.1f)] public float walkSeconds = 2.6f;
    [Min(.1f)] public float panoramaShotSeconds = 3f;
    [Min(.1f)] public float returnCameraSeconds = .8f;
    [Min(0f)] public float portalShutdownDistance = .8f;
    [Min(.05f)] public float portalFadeSeconds = .65f;
    [Range(25, 80)] public float cinematicFieldOfView = 48;
    [Header("After arrival: voice and discovery")]
    [Tooltip("Các câu thoại phát lần lượt sau khi camera lia map và trở về player.")]
    public AudioClip[] arrivalDialogue = Array.Empty<AudioClip>();
    public AudioSource arrivalVoice;
    public WorldInteraction arrivalDiscoveryArea;
    public DiscoveryAreaBanner arrivalDiscoveryBanner;
    [Min(0f)] public float dialogueGapSeconds = .18f;
    [Min(0f), Tooltip("Chờ sau khi hết thoại và tên khu vực, rồi mới hiện tutorial.")]
    public float tutorialDelaySeconds = 2f;
    [Header("Tutorial canvas")]
    public Canvas tutorialCanvas;
    public GuidedCombatTutorialView lessons;
    public RectTransform[] keyHints = new RectTransform[5]; // WASD, Space, scan, P, E
    public RectTransform worldFocus;
    public TMP_Text worldFocusLabel;
    public GameObject cinematicBars;
    [Min(.5f)] public float movementPracticeDistance = 2f;
    [Tooltip("Persist tutorial progress through the existing campaign autosave. Disable only in isolated editor validation.")]
    public bool persistProgress = true;

    CampaignSession session;
    ExplorationTutorialProgress progress;
    AdventureScanner scanner;
    AdventureCanvasRoot adventureCanvas;
    bool locked, practicing, menuOpened, restOpened, parkourStarted;
    bool scannerUsed;
    int shownLesson = -1, jumpBaseline;
    Vector3 practiceOrigin;
    AnimatorUpdateMode originalAnimationUpdate;
    AnimatorCullingMode originalCulling;
    bool animationCaptured;
    CinemachineCore.GetBlendOverrideDelegate previousBlendOverride;
    bool blendOverrideInstalled;
    static MExplorationTutorial activePractice;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPracticeOwner() => activePractice = null;
    public static bool Allows(ExplorationTutorialAction action, PlayerScript actor = null)
    {
        var owner = activePractice;
        if (owner == null || !owner.practicing || owner.progress == null || owner.progress.complete ||
            owner.gameObject.scene.name != "m" || actor != null && owner.player != actor) return true;
        return owner.CurrentLesson >= (int)action;
    }
    public bool IsPresenting => locked;
    public bool IsPracticing => practicing;
    public int CurrentLesson => progress != null ? progress.nextLesson : -1;

    IEnumerator Start()
    {
        if (gameObject.scene.name != "m") yield break;
        float deadline = Time.realtimeSinceStartup + 45f;
        while ((mapSetup == null || !mapSetup.StartupReady || CampaignSession.Instance == null || LoadingScreen.IsLoading) && Time.realtimeSinceStartup < deadline)
            yield return null;
        session = CampaignSession.Instance;
        if (mapSetup == null || !mapSetup.StartupReady || session == null || session.Data == null || player == null)
        { Debug.LogWarning("[m Tutorial] Map setup/player is not ready; controls remain with gameplay."); yield break; }
        progress = FindProgress(session.Data, false);
        // Older saves in m must never be teleported back to a newly added opening.
        if (progress == null && session.RestoredSavedMapPosition)
        {
            progress = FindProgress(session.Data, true); progress.introComplete = progress.complete = true; progress.nextLesson = LessonCount;
        }
        else progress ??= FindProgress(session.Data, true);
        if (progress.introComplete) portal?.SetCharge(0);
        if (progress.complete) { HideAll(); yield break; }
        SetupViews();
        if (!progress.introComplete)
        {
            if (session.RestoredSavedMapPosition) { progress.introComplete = true; portal?.SetCharge(0); }
            else yield return Arrival();
        }
        if (!isActiveAndEnabled || player == null || session == null || !session.OnMap) yield break;
        BeginPractice();
    }

    public static ExplorationTutorialProgress FindProgress(CampaignSave data, bool create)
    {
        if (data == null) return null;
        data.explorationTutorials ??= new List<ExplorationTutorialProgress>();
        var entry = data.explorationTutorials.Find(p => p != null && p.id == ProgressId);
        if (entry == null && create) { entry = new ExplorationTutorialProgress { id = ProgressId, curriculumVersion = CurriculumVersion }; data.explorationTutorials.Add(entry); }
        if (entry != null && entry.curriculumVersion < CurriculumVersion)
        {
            int previousLesson = Mathf.Clamp(entry.nextLesson, 0, LessonCount);
            entry.movementCompleted = previousLesson >= 1;
            entry.jumpCompleted = previousLesson >= 2;
            // Keep finished practice; old graduates receive only the new final hint.
            entry.nextLesson = entry.complete || previousLesson >= LessonCount ? 6 : Mathf.Max(0, previousLesson - 1);
            entry.complete = false;
            entry.curriculumVersion = CurriculumVersion;
        }
        return entry;
    }
    void SetupViews()
    {
        adventureCanvas = AdventureCanvasRoot.Acquire(AdventureCanvasKind.Adventure);
        var font = adventureCanvas.GetComponentInChildren<TMP_Text>(true)?.font;
        if (tutorialCanvas != null) lessons = GuidedCombatTutorialView.Create(tutorialCanvas, font);
        if (font != null && tutorialCanvas != null)
            foreach (var text in tutorialCanvas.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
        scanner = AdventureScanner.EnsureForMap(player);
        if (scanner != null) scanner.onScanStarted.AddListener(ScanUsed);
        GameLocalization.LanguageChanged += RefreshLanguage;
        lessons?.Close();
        if (cinematicBars != null) cinematicBars.SetActive(false);
    }
    IEnumerator Arrival()
    {
        if (walkStart == null || walkExit == null || cinematicCamera == null || walkCameraStart == null || walkCameraEnd == null || !session.TryBeginPresentation(this))
        { Debug.LogWarning("[m Tutorial] Arrival references unavailable; skipping camera sequence safely."); progress.introComplete = true; portal?.SetCharge(0); yield break; }
        locked = true;
        try
        {
            if (cinematicBars != null) cinematicBars.SetActive(true);
            foreach (var hint in keyHints) if (hint != null) hint.gameObject.SetActive(false);
            if (worldFocus != null) worldFocus.gameObject.SetActive(false);
            bool controllerEnabled = player.CC != null && player.CC.enabled;
            if (player.CC != null) player.CC.enabled = false;
            player.transform.SetPositionAndRotation(walkStart.position, walkStart.rotation);
            if (player.CC != null) player.CC.enabled = controllerEnabled;
            player.ResetAfterCinematic();
            var animator = player.animator;
            if (animator != null)
            {
                originalAnimationUpdate = animator.updateMode; originalCulling = animator.cullingMode; animationCaptured = true;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.CrossFadeInFixedTime("Base Layer.Basic Movement", .12f); animator.SetFloat("movementvalue", .5f);
            }
            cinematicCamera.transform.SetPositionAndRotation(walkCameraStart.position, walkCameraStart.rotation);
            var lens = cinematicCamera.Lens; lens.FieldOfView = cinematicFieldOfView; cinematicCamera.Lens = lens;
            previousBlendOverride = CinemachineCore.GetBlendOverride;
            CinemachineCore.GetBlendOverride = ArrivalBlend; blendOverrideInstalled = true;
            cinematicCamera.Priority = 100; cinematicCamera.enabled = true; portal?.SetCharge(1f);
            float age = 0, fadeAge = -1;
            while (age < Mathf.Max(.1f, walkSeconds))
            {
                age += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(age / Mathf.Max(.1f, walkSeconds));
                Vector3 desired = Vector3.Lerp(walkStart.position, walkExit.position, t);
                if (player.CC != null && player.CC.enabled) player.CC.Move(desired - player.transform.position);
                else player.transform.position = desired;
                float cameraT = Mathf.SmoothStep(0, 1, t);
                cinematicCamera.transform.SetPositionAndRotation(Vector3.Lerp(walkCameraStart.position, walkCameraEnd.position, cameraT),
                    Quaternion.Slerp(walkCameraStart.rotation, walkCameraEnd.rotation, cameraT));
                float passed = Vector3.Dot(player.transform.position - walkStart.position, walkStart.forward);
                if (passed >= portalShutdownDistance && fadeAge < 0) fadeAge = 0;
                if (fadeAge >= 0) { fadeAge += Time.unscaledDeltaTime; portal?.SetCharge(1f - fadeAge / Mathf.Max(.05f, portalFadeSeconds)); }
                yield return null;
            }
            animator?.SetFloat("movementvalue", 0f);
            portal?.SetCharge(0);
            if (Vector3.Distance(player.transform.position, walkExit.position) > .5f)
                Debug.LogWarning("[m Tutorial] Arrival walk was obstructed. Keeping the safe current position; inspect portal/path colliders.");
            foreach (var pose in panorama)
                if (pose != null) yield return BlendTo(pose.position, pose.rotation, panoramaShotSeconds);
            player.MCC?.SetCameraRotation(player.transform.eulerAngles.y, 12f); player.MCC?.SnapCameraTarget();
            if (gameplayCamera != null)
            {
                gameplayCamera.PreviousStateIsValid = false;
                gameplayCamera.UpdateCameraState(Vector3.up, -1f);
                yield return null;
                yield return BlendTo(gameplayCamera.State.GetFinalPosition(), gameplayCamera.State.GetFinalOrientation(), returnCameraSeconds, gameplayCamera.Lens.FieldOfView);
            }
            // Our own camera motion already performs the blend. Let the brain
            // hand back this frame without its additional two-second default blend.
            cinematicCamera.enabled = false;
            yield return null;
            yield return ArrivalDialogue();
        }
        finally { RestorePresentation(); }
        progress.introComplete = true; SaveProgress();
    }
    IEnumerator ArrivalDialogue()
    {
        // AdventureUI hides the HUD during a presentation. This scene-owned copy
        // shares the discovery styling but stays visible with the cinematic bars.
        if (arrivalDiscoveryArea != null)
        {
            arrivalDiscoveryArea.CompleteCollection(requireRange: true, announceDiscovery: false);
            arrivalDiscoveryBanner?.Enqueue(arrivalDiscoveryArea.Id,
                string.IsNullOrWhiteSpace(arrivalDiscoveryArea.displayName) ? arrivalDiscoveryArea.name : arrivalDiscoveryArea.displayName);
        }
        if (arrivalVoice != null && arrivalDialogue != null)
        {
            arrivalVoice.playOnAwake = false; arrivalVoice.loop = false; arrivalVoice.pitch = 1f;
            arrivalVoice.spatialBlend = 0f; arrivalVoice.ignoreListenerPause = true;
            bool previousLine = false;
            foreach (var clip in arrivalDialogue)
            {
                if (clip == null) continue;
                if (previousLine && dialogueGapSeconds > 0) yield return new WaitForSecondsRealtime(dialogueGapSeconds);
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                while (clip.loadState == AudioDataLoadState.Loading) yield return null;
                if (clip.loadState == AudioDataLoadState.Failed) { Debug.LogWarning("[m Tutorial] Cannot load arrival dialogue: " + clip.name); continue; }
                arrivalVoice.clip = clip;
                if (Application.isPlaying) arrivalVoice.Play();
                yield return new WaitForSecondsRealtime(clip.length);
                arrivalVoice.Stop(); previousLine = true;
            }
            arrivalVoice.clip = null;
        }
        while (arrivalDiscoveryBanner != null && arrivalDiscoveryBanner.IsPlaying) yield return null;
        if (tutorialDelaySeconds > 0) yield return new WaitForSecondsRealtime(tutorialDelaySeconds);
    }
    IEnumerator BlendTo(Vector3 position, Quaternion rotation, float seconds, float targetFieldOfView = -1f)
    {
        Vector3 from = cinematicCamera.transform.position; Quaternion turnFrom = cinematicCamera.transform.rotation;
        float fromFov = cinematicCamera.Lens.FieldOfView;
        float age = 0;
        while (age < Mathf.Max(.1f, seconds))
        {
            age += Time.unscaledDeltaTime; float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / Mathf.Max(.1f, seconds)));
            cinematicCamera.transform.SetPositionAndRotation(Vector3.Lerp(from, position, t), Quaternion.Slerp(turnFrom, rotation, t));
            if (targetFieldOfView > 0) { var lens = cinematicCamera.Lens; lens.FieldOfView = Mathf.Lerp(fromFov, targetFieldOfView, t); cinematicCamera.Lens = lens; }
            yield return null;
        }
    }
    void RestorePresentation()
    {
        if (arrivalVoice != null) { arrivalVoice.Stop(); arrivalVoice.clip = null; }
        arrivalDiscoveryBanner?.Cancel();
        if (!locked && !animationCaptured && !blendOverrideInstalled) return;
        if (blendOverrideInstalled)
        {
            if (CinemachineCore.GetBlendOverride == ArrivalBlend) CinemachineCore.GetBlendOverride = previousBlendOverride;
            else CinemachineCore.GetBlendOverride -= ArrivalBlend;
            previousBlendOverride = null; blendOverrideInstalled = false;
        }
        if (cinematicCamera != null) { cinematicCamera.Priority = -100; cinematicCamera.enabled = false; }
        if (cinematicBars != null) cinematicBars.SetActive(false);
        if (player != null)
        {
            if (animationCaptured && player.animator != null) { player.animator.updateMode = originalAnimationUpdate; player.animator.cullingMode = originalCulling; }
            player.ResetAfterCinematic(); player.MCC?.SnapCameraTarget();
        }
        animationCaptured = false;
        if (locked && session != null) session.EndPresentation(this);
        locked = false;
    }
    CinemachineBlendDefinition ArrivalBlend(ICinemachineCamera from, ICinemachineCamera to, CinemachineBlendDefinition original, UnityEngine.Object owner)
    {
        if (ReferenceEquals(from, cinematicCamera) || ReferenceEquals(to, cinematicCamera))
            return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        return previousBlendOverride != null ? previousBlendOverride(from, to, original, owner) : original;
    }
    void BeginPractice()
    {
        practicing = true; activePractice = this;
        session.EncounterStarted -= OnEncounterStarted;
        session.EncounterStarted += OnEncounterStarted;
        foreach (var hint in keyHints) if (hint != null) hint.gameObject.SetActive(true);
        ResetPractice(); ShowLesson();
    }
    void ResetPractice()
    {
        practiceOrigin = player.transform.position; jumpBaseline = player.JumpCount;
        scannerUsed = menuOpened = restOpened = parkourStarted = false;
    }
    void ScanUsed() { if (CurrentLesson == 1) scannerUsed = true; }
    void OnEncounterStarted(WorldInteraction point)
    {
        if (!practicing || progress == null || encounterEnemy == null || point != encounterEnemy) return;
        progress.enemyEncountered = true;
        if (CurrentLesson == 6) Advance();
        else SaveProgress();
    }
    void RefreshLanguage() { if (practicing) { shownLesson = -1; ShowLesson(); } }
    void Update()
    {
        if (!practicing || progress == null || session == null || player == null) return;
        if (!session.OnMap || session.MapPlayer != player || session.Busy || session.PresentationActive || LoadingScreen.IsLoading)
        { lessons?.Close(); return; }
        if (lessons != null && !lessons.IsOpen) ShowLesson();
        bool done = false;
        switch (CurrentLesson)
        {
            case 0:
                Vector3 delta = player.transform.position - practiceOrigin; delta.y = 0;
                bool changed = false;
                if (!progress.movementCompleted && delta.magnitude >= movementPracticeDistance) { progress.movementCompleted = true; changed = true; }
                if (!progress.jumpCompleted && player.JumpCount > jumpBaseline) { progress.jumpCompleted = true; changed = true; }
                done = progress.movementCompleted && progress.jumpCompleted;
                if (changed && !done) SaveProgress();
                break;
            case 1: done = scannerUsed; break;
            case 2:
                if (session.Menu == AdventureMenu.Main || session.Menu == AdventureMenu.Party) menuOpened = true;
                done = menuOpened && session.Menu == AdventureMenu.None; break;
            case 3:
                bool near = parkourObstacle != null && Vector3.Distance(player.transform.position, parkourObstacle.ClosestPoint(player.transform.position)) < 6f;
                var climb = player.GetComponent<PlayerClimb>();
                bool action = player.playerInAction || climb != null && climb.isClimbing;
                if (near && action) parkourStarted = true;
                done = parkourStarted && !action; break;
            case 4: done = session.Menu == AdventureMenu.Rest && session.RestPoint == campfire; break;
            case 5:
                if (session.Menu == AdventureMenu.Rest && session.RestPoint == campfire) restOpened = true;
                done = restOpened && session.Menu == AdventureMenu.None; break;
            case 6: done = progress.enemyEncountered || encounterEnemy != null && encounterEnemy.IsConsumed; break;
        }
        if (done) Advance();
    }
    public void Advance()
    {
        if (progress == null || !practicing) return;
        progress.nextLesson = Mathf.Clamp(progress.nextLesson + 1, 0, LessonCount);
        if (progress.nextLesson >= LessonCount)
        {
            progress.complete = true; practicing = false; HideAll();
            if (activePractice == this) activePractice = null;
            session?.Notify(GameLocalization.IsEnglish ? "Exploration tutorial completed." : "Đã hoàn thành hướng dẫn khám phá.");
        }
        else { ResetPractice(); ShowLesson(); }
        SaveProgress();
    }
    void SaveProgress() { if (persistProgress && Application.isPlaying && session != null && session.Data != null) session.Save(); }
    void HideAll()
    {
        lessons?.Close(); if (cinematicBars != null) cinematicBars.SetActive(false);
        foreach (var hint in keyHints) if (hint != null) hint.gameObject.SetActive(false);
        if (worldFocus != null) worldFocus.gameObject.SetActive(false);
    }
    void OnDisable()
    {
        if (activePractice == this) activePractice = null;
        if (!Application.isPlaying) return;
        StopAllCoroutines();
        practicing = false; RestorePresentation(); HideAll();
        if (scanner != null) scanner.onScanStarted.RemoveListener(ScanUsed);
        if (session != null) session.EncounterStarted -= OnEncounterStarted;
        GameLocalization.LanguageChanged -= RefreshLanguage;
    }
    void OnDrawGizmosSelected()
    {
        if (walkStart == null || walkExit == null) return;
        Gizmos.color = Color.cyan; Gizmos.DrawLine(walkStart.position, walkExit.position); Gizmos.DrawWireSphere(walkStart.position, .3f);
        var previous = walkCameraEnd;
        foreach (var pose in panorama) { if (pose == null) continue; if (previous != null) Gizmos.DrawLine(previous.position, pose.position); previous = pose; }
    }

    // Lesson text/focus is kept here so language changes update the currently active page.
    void ShowLesson()
    {
        if (lessons == null || progress == null || progress.complete || player == null) return;
        int index = Mathf.Clamp(CurrentLesson, 0, LessonCount - 1); bool en = GameLocalization.IsEnglish;
        var actions = new[] { ExplorationTutorialAction.Movement, ExplorationTutorialAction.Movement,
            ExplorationTutorialAction.Scan, ExplorationTutorialAction.Menu, ExplorationTutorialAction.Interact };
        for (int i = 0; keyHints != null && i < keyHints.Length && i < actions.Length; i++)
            if (keyHints[i] != null)
            {
                var group = keyHints[i].GetComponent<CanvasGroup>(); if (group == null) group = keyHints[i].gameObject.AddComponent<CanvasGroup>();
                group.alpha = Allows(actions[i], player) ? 1f : .28f;
            }
        string scan = scanner != null && scanner.settings != null ? scanner.settings.scanKey.ToString() : "Q";
        string[] titles = en ? new[] { "Movement and jump", "Scan your surroundings", "Main menu", "Parkour", "Interact", "Campfire", "Enemy encounter" }
            : new[] { "Di chuyển và nhảy", "Quét môi trường", "Menu chính", "Parkour", "Tương tác", "Lửa trại", "Chạm trán kẻ địch" };
        string[] bodies = en ? new[] {
            "Use W A S D to move and Space to jump while standing on walkable ground.\n\nMove a short distance and jump once to continue. Press Shift to toggle running; move the mouse to look around.",
            "Press " + scan + " to send a scan pulse and reveal nearby points of interest.\n\nThe highlighted circle shows the scan key and its cooldown.",
            "Press P to open the main menu. Review Party, Inventory, Quests and Load Game.\n\nClose the menu with P or Esc to continue.",
            "Go to the highlighted obstacle near the portal. Face it, hold W and press Space to vault or climb.\n\nFinish the action to continue.",
            "Go to the highlighted campfire. When its interaction prompt appears, press E to interact.\n\nE also interacts with items, chests and other available objects.",
            "The campfire restores party health and opens rest options: save, attributes, skills and unlocked fast travel.\n\nOpen it with E if needed, then close its menu with Esc to continue.",
            "Go to the highlighted enemy. When in interaction range, press E or left-click to start combat.\n\nAn enemy that detects and attacks you can also trigger an encounter. Encounter this enemy to finish the tutorial." }
            : new[] {
            "Dùng W A S D để di chuyển và Space để nhảy khi đứng trên nền.\n\nĐi một đoạn ngắn và nhảy một lần để tiếp tục. Bấm Shift để bật/tắt chạy; di chuột để quan sát.",
            "Bấm " + scan + " để quét và phát hiện các điểm đáng chú ý gần bạn.\n\nVòng tròn được làm sáng hiển thị phím quét và thời gian hồi.",
            "Bấm P để mở menu chính. Tại đây có Đội hình, Rương đồ, Nhiệm vụ và Tải game.\n\nĐóng menu bằng P hoặc Esc để tiếp tục.",
            "Đi tới vật cản được đánh dấu gần cổng. Hướng mặt vào vật cản, giữ W và bấm Space để vượt hoặc leo lên.\n\nHoàn tất động tác để tiếp tục.",
            "Đi tới lửa trại được đánh dấu. Khi có lời nhắc tương tác, bấm E để sử dụng.\n\nE cũng dùng để nhặt đồ, mở rương và tương tác với vật thể phù hợp.",
            "Lửa trại hồi máu cho đội và mở các mục nghỉ: lưu game, thuộc tính, kỹ năng và dịch chuyển đã mở khóa.\n\nNếu cần, bấm E để mở lại; đóng menu bằng Esc để tiếp tục.",
            "Đi tới kẻ địch được đánh dấu. Khi vào phạm vi tương tác, bấm E hoặc chuột trái để bắt đầu chiến đấu.\n\nKẻ địch phát hiện và tấn công bạn cũng có thể mở encounter. Chạm trán kẻ địch này để hoàn tất hướng dẫn." };
        var targets = FocusTargets(index);
        lessons.Close();
        lessons.Show(index + 1, LessonCount, targets, titles[index], bodies[index], en ? "Perform the action to continue" : "Thực hiện thao tác để tiếp tục", null);
        lessons.SetHandsOn(true); shownLesson = index;
    }
    List<RectTransform> FocusTargets(int index)
    {
        var targets = new List<RectTransform>();
        int key = index == 0 ? 0 : index == 1 ? 2 : index == 2 ? 3 : index == 3 ? 1 : 4;
        if (keyHints != null && key < keyHints.Length && keyHints[key] != null) targets.Add(keyHints[key]);
        if (index == 0 && keyHints != null && keyHints.Length > 1 && keyHints[1] != null) targets.Add(keyHints[1]);
        if (index == 1 && scanner != null)
        {
            var rect = scanner.GetComponent<AdventureScanHUD>()?.CooldownRect; if (rect != null && rect.gameObject.activeInHierarchy) { targets.Clear(); targets.Add(rect); }
        }
        if (index >= 3 && worldFocus != null && worldFocus.gameObject.activeInHierarchy) { targets.Clear(); targets.Add(worldFocus); }
        if (index == 5 && session != null && session.Menu == AdventureMenu.Rest && adventureCanvas != null)
        {
            var panel = adventureCanvas.transform.Find("Menu/Rest") as RectTransform;
            if (panel != null && panel.gameObject.activeInHierarchy) { targets.Clear(); targets.Add(panel); }
        }
        return targets;
    }
    void LateUpdate()
    {
        if (!practicing || worldFocus == null || tutorialCanvas == null || player == null || session == null) return;
        if (session.PresentationActive)
        {
            lessons?.Close(); worldFocus.gameObject.SetActive(false); shownLesson = -1;
            foreach (var hint in keyHints) if (hint != null) hint.gameObject.SetActive(false);
            return;
        }
        foreach (var hint in keyHints) if (hint != null) hint.gameObject.SetActive(true);
        Transform target = CurrentLesson == 3 ? parkourObstacle != null ? parkourObstacle.transform : null :
            CurrentLesson == 4 || CurrentLesson == 5 ? campfire != null ? campfire.transform : null :
            CurrentLesson == 6 ? encounterEnemy != null ? encounterEnemy.transform : null : null;
        var camera = Camera.main;
        bool visible = target != null && camera != null && session.Menu == AdventureMenu.None;
        Vector3 point = target != null ? target.position + Vector3.up : Vector3.zero;
        if (CurrentLesson == 3 && parkourObstacle != null) point = parkourObstacle.bounds.center;
        Vector3 screen = visible ? camera.WorldToScreenPoint(point) : Vector3.zero;
        if (visible && screen.z < 0) { screen.x = Screen.width - screen.x; screen.y = Screen.height - screen.y; }
        if (worldFocus.gameObject.activeSelf != visible) { worldFocus.gameObject.SetActive(visible); shownLesson = -1; }
        if (visible)
        {
            var root = tutorialCanvas.transform as RectTransform;
            screen.x = Mathf.Clamp(screen.x, 140, Screen.width - 140); screen.y = Mathf.Clamp(screen.y, 110, Screen.height - 110);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local)) worldFocus.anchoredPosition = local;
            if (worldFocusLabel != null)
            {
                string label = CurrentLesson == 3 ? "Parkour" : CurrentLesson == 6 ? GameLocalization.IsEnglish ? "Enemy" : "Kẻ địch" : GameLocalization.IsEnglish ? "Campfire" : "Lửa trại";
                worldFocusLabel.text = label + " • " + Mathf.RoundToInt(Vector3.Distance(player.transform.position, target.position)) + " m";
            }
        }
        if (shownLesson != CurrentLesson) ShowLesson();
    }
}
