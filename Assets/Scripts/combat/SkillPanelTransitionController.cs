using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if DOTWEEN
using DG.Tweening;
#endif

/// <summary>Presentation only. CombatManager continues to own actions, turns and targeting.</summary>
[DisallowMultipleComponent]
public sealed class SkillPanelTransitionController : MonoBehaviour, ICancelHandler
{
    public enum TransitionState { Closed, Opening, Open }
    public enum MotionEase { Linear, OutCubic, OutBack, InOutCubic }

    [Header("Existing Canvas references (keep this component outside Skills Panel)")]
    public Button skillsButton;
    public RectTransform skillsPanel;
    public RectTransform skillItemsRoot;
    public RectTransform commandMenu;
    public BrushRevealController brushReveal;

    [Header("Items and wipe direction")]
    public Button itemsButton;
    public RectTransform itemsViewport;
    public ScrollRect itemsScroll;
    public RectTransform itemRowsRoot;
    public BrushRevealController.SweepDirection skillsDirection = BrushRevealController.SweepDirection.RightToLeft;
    public BrushRevealController.SweepDirection itemsDirection = BrushRevealController.SweepDirection.RightToLeft;

    [Header("Button slide — Canvas units")]
    public float buttonSlideDistance = 24f;
    [Min(.01f)] public float buttonDuration = .12f;
    public MotionEase buttonEase = MotionEase.OutBack;
    [Range(0f, 3f)] public float overshoot = 1.4f;

    [Header("Opening timeline — unscaled seconds")]
    [Min(0f)] public float revealDelay = .10f;
    [Min(.01f)] public float revealDuration = .30f;
    public MotionEase revealEase = MotionEase.Linear;
    [Min(0f)] public float itemsDelay = .13f;
    [Min(.01f)] public float itemDuration = .17f;
    [Min(0f)] public float staggerDelay = .03f;
    public float itemSlideDistance = 24f;
    public MotionEase itemEase = MotionEase.OutCubic;
    [Min(0f)] public float minimumOpenDuration = .48f;
    [Tooltip("Maximum time the ink animation advances per rendered frame. Keeps a scene-loading or camera/shader hitch from skipping the entire wipe. Only affects this UI transition.")]
    [Range(.016f, .1f)] public float maximumFrameStep = .05f;

    [Header("Standalone callbacks")]
    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent onClosed = new UnityEvent();

    public TransitionState State { get; private set; }
    public bool IsTransitioning => State == TransitionState.Opening;
    public bool IsOpen => State == TransitionState.Open;
    public bool IsVisible => State != TransitionState.Closed;

    readonly List<SkillItemAnimator> items = new List<SkillItemAnimator>();
    CanvasGroup panelGroup, commandGroup;
    RectTransform activePanel;
    Button activeButton;
    BrushRevealController activeBrush, itemsBrush;
    bool showingItems, restoreItemScrolling;
    BattleMenuButtonSlide buttonMotion;
    RectTransform plainButtonRect;
    Vector2 plainButtonOrigin;
    bool plainButtonCaptured, commandHidden, resetting;
    bool savedPanelInteractable, savedPanelRaycasts, savedCommandInteractable, savedCommandRaycasts;
    bool returnNavigationFocus;
    float savedCommandAlpha = 1f;
    float clock, reveal;
    int openingFrame;
    Action openedCallback, closedCallback, cancelCallback;
    Coroutine fallbackRoutine;
#if DOTWEEN
    Sequence sequence;
#endif

    public void Bind(Action opened, Action closed, Action cancel)
    {
        openedCallback = opened;
        closedCallback = closed;
        cancelCallback = cancel;
    }

    public bool Initialize() => InitializePanel(false);

    /// <summary>Build both masks during battle setup, before the command menu accepts input.</summary>
    public void PreparePanels()
    {
        if (!isActiveAndEnabled || IsVisible) return;
        if (InitializePanel(false)) ResetTransition();
        if (InitializePanel(true)) ResetTransition();
    }

    bool InitializePanel(bool forItems)
    {
        var panel = forItems ? itemsViewport : skillsPanel;
        if (panel == null || panel == transform || transform.IsChildOf(panel)) return false;
        if (brushReveal == null) brushReveal = GetComponent<BrushRevealController>();
        if (brushReveal == null) brushReveal = gameObject.AddComponent<BrushRevealController>();
        UpgradeOriginalTimings();
        var brush = forItems ? GetItemsBrush() : brushReveal;
        if (!brush.Initialize(panel, forItems ? itemsDirection : skillsDirection)) return false;
        activeBrush = brush;
        activePanel = panel;
        activeButton = forItems ? itemsButton : skillsButton;
        showingItems = forItems;
        buttonMotion = activeButton != null ? activeButton.GetComponent<BattleMenuButtonSlide>() : null;
        panelGroup = panel.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        commandGroup = null;
        if (commandMenu != null)
        {
            commandGroup = commandMenu.GetComponent<CanvasGroup>();
            if (commandGroup == null) commandGroup = commandMenu.gameObject.AddComponent<CanvasGroup>();
        }
        savedPanelInteractable = panelGroup.interactable;
        savedPanelRaycasts = panelGroup.blocksRaycasts;
        savedCommandInteractable = commandGroup == null || commandGroup.interactable;
        savedCommandRaycasts = commandGroup == null || commandGroup.blocksRaycasts;
        savedCommandAlpha = commandGroup != null && commandMenu.GetComponent<CombatActionMenuTransition>() == null
            ? commandGroup.alpha : 1f;
        plainButtonCaptured = false;
        plainButtonRect = null;
        if (buttonMotion == null && activeButton != null && activeButton.transform is RectTransform sourceRect)
        {
            plainButtonRect = sourceRect;
            plainButtonOrigin = plainButtonRect.anchoredPosition;
            plainButtonCaptured = true;
        }
        return true;
    }

    BrushRevealController GetItemsBrush()
    {
        if (itemsBrush == null)
        {
            var host = new GameObject("Items Ink Effect");
            host.transform.SetParent(transform, false);
            itemsBrush = host.AddComponent<BrushRevealController>();
        }
        itemsBrush.CopyStyleFrom(brushReveal);
        return itemsBrush;
    }

    void UpgradeOriginalTimings()
    {
        // Existing Canvas components may still have the original narrow stroke's defaults.
        if (revealEase != MotionEase.OutCubic || !Mathf.Approximately(revealDuration, .26f) ||
            !Mathf.Approximately(itemsDelay, .24f) || !Mathf.Approximately(itemDuration, .12f)) return;
        revealEase = MotionEase.Linear;
        revealDuration = .30f;
        itemsDelay = .13f;
        itemDuration = .17f;
    }

    public void OpenSkillPanel() => TryOpenPanel(false);

    public bool TryOpenPanel(bool forItems)
    {
        if (!isActiveAndEnabled) return false;
        if (IsVisible) return showingItems == forItems;
        if (!InitializePanel(forItems)) return false;
        KillTimeline();
        State = TransitionState.Opening;
        openingFrame = Time.frameCount;
        returnNavigationFocus = false;
        activePanel.gameObject.SetActive(true);
        activeBrush.CancelPlayback();
        // Set the hidden mask before rebuilding the newly enabled Canvas. On
        // subsequent opens it still contains the previous fully revealed pose.
        activeBrush.SetProgress(0f);
        activeBrush.SetBrushOpacity(0f);
        Canvas.ForceUpdateCanvases();
        if (showingItems && itemsScroll != null && itemsScroll.enabled)
        {
            restoreItemScrolling = true;
            itemsScroll.StopMovement();
            itemsScroll.enabled = false;
        }
        items.Clear();
        var rows = showingItems ? itemRowsRoot : skillItemsRoot;
        if (rows != null) foreach (Transform child in rows)
        {
            if (!child.gameObject.activeSelf) continue;
            if (child.GetComponent<Selectable>() == null) continue;
            var item = child.GetComponent<SkillItemAnimator>();
            if (item == null) item = child.gameObject.AddComponent<SkillItemAnimator>();
            item.transitionController = this;
            if (!showingItems)
            {
                item.hiddenOffset = new Vector2(itemSlideDistance, 0f);
                item.Prepare();
            }
            items.Add(item);
        }
        GatePanel(false);
        GateCommands(false);
        // Keep a stable non-Selectable cancel receiver during the wipe. A null
        // selection would make InputSystemUIInputModule drop gamepad Cancel.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        commandHidden = commandMenu == null || !commandMenu.gameObject.activeSelf;
        SetButtonOffset(0f);
        float duration = Mathf.Max(minimumOpenDuration, buttonDuration, revealDelay + revealDuration);
        if (!showingItems && items.Count > 0) duration = Mathf.Max(duration, itemsDelay + (items.Count - 1) * staggerDelay + itemDuration);
        SampleOpen(0f);
        PlayTimeline(duration, SampleOpen, FinishOpen);
        return true;
    }

    void SampleOpen(float time)
    {
        SetButtonOffset(buttonSlideDistance * Ease(Segment(time, 0f, buttonDuration), buttonEase, overshoot));
        if (time >= Mathf.Max(buttonDuration, revealDelay) && !commandHidden) HideCommands();
        float sweepTime = Segment(time, revealDelay, revealDuration);
        // OutBack may overshoot. Keep the revealed area monotonic as the front advances.
        reveal = Mathf.Max(reveal, Mathf.Clamp01(Ease(sweepTime, revealEase, overshoot)));
        activeBrush.SetProgress(reveal);
        activeBrush.SetBrushOpacity(Mathf.Clamp01(Mathf.Min(sweepTime * 12f, (1f - sweepTime) * 8f)));
        if (!showingItems)
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null) items[i].SetProgress(Ease(Segment(time, itemsDelay + i * staggerDelay, itemDuration), itemEase, overshoot));
    }

    void FinishOpen()
    {
        if (State != TransitionState.Opening || activePanel == null || !activePanel.gameObject.activeInHierarchy) return;
        State = TransitionState.Open;
        activeBrush.ResetTransition();
        HideCommands();
        GatePanel(true);
        RestoreItemScrolling();
        if (!showingItems)
            foreach (var item in items)
                if (item != null) { item.SetProgress(1f); item.SetInteractionEnabled(true); }
        if (!showingItems && openedCallback != null) openedCallback();
        else FocusFirstItem();
        onOpened.Invoke();
    }

    public void CloseSkillPanel()
    {
        if (!IsVisible) return;
        Button source = activeButton;
        bool navigationFocus = returnNavigationFocus;
        ResetTransition();
        if (closedCallback != null) closedCallback();
        else if (commandMenu != null)
        {
            var menu = commandMenu.GetComponent<CombatActionMenuTransition>();
            if (menu != null) menu.SetVisible(true);
            else commandMenu.gameObject.SetActive(true);
        }

        if (source != null && source.isActiveAndEnabled && source.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(source.gameObject,
                navigationFocus ? new AxisEventData(EventSystem.current) : null);
        onClosed.Invoke();
    }

    void RestoreItemScrolling()
    {
        if (restoreItemScrolling && itemsScroll != null) itemsScroll.enabled = true;
        restoreItemScrolling = false;
    }

    /// <summary>Stops any wipe and hides its panel immediately without reopening commands.</summary>
    public void ResetTransition()
    {
        if (resetting) return;
        resetting = true;
        KillTimeline();
        State = TransitionState.Closed;
        foreach (var item in items) if (item != null) item.Restore();
        items.Clear();
        if (activeBrush != null) activeBrush.ResetTransition();
        if (buttonMotion != null) buttonMotion.SetTransitionMotion(false);
        if (plainButtonCaptured && plainButtonRect != null) plainButtonRect.anchoredPosition = plainButtonOrigin;
        if (panelGroup != null)
        {
            panelGroup.interactable = savedPanelInteractable;
            panelGroup.blocksRaycasts = savedPanelRaycasts;
        }
        if (commandGroup != null)
        {
            commandGroup.interactable = savedCommandInteractable;
            commandGroup.blocksRaycasts = savedCommandRaycasts;
            commandGroup.alpha = savedCommandAlpha;
        }
        RestoreItemScrolling();
        var eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
            (eventSystem.currentSelectedGameObject == gameObject ||
             (activePanel != null && eventSystem.currentSelectedGameObject.transform.IsChildOf(activePanel))))
            eventSystem.SetSelectedGameObject(null);
        if (activePanel != null) activePanel.gameObject.SetActive(false);
        activePanel = null;
        activeButton = null;
        activeBrush = null;
        panelGroup = commandGroup = null;
        plainButtonCaptured = false;
        buttonMotion = null;
        reveal = 0f;
        returnNavigationFocus = false;
        resetting = false;
    }

    void GatePanel(bool enabled)
    {
        if (panelGroup == null) return;
        panelGroup.interactable = enabled && savedPanelInteractable;
        panelGroup.blocksRaycasts = enabled && savedPanelRaycasts;
    }

    void GateCommands(bool enabled)
    {
        if (commandGroup == null) return;
        commandGroup.interactable = enabled;
        commandGroup.blocksRaycasts = enabled;
    }

    void HideCommands()
    {
        if (commandHidden) return;
        commandHidden = true;
        if (commandMenu == null) return;
        var menu = commandMenu.GetComponent<CombatActionMenuTransition>();
        if (menu != null) menu.SetVisible(false, true);
        else commandMenu.gameObject.SetActive(false);
    }

    void SetButtonOffset(float offset)
    {
        if (activeButton == null || !activeButton.gameObject.activeInHierarchy) return;
        if (buttonMotion != null) buttonMotion.SetTransitionMotion(true, offset);
        else if (plainButtonRect != null) plainButtonRect.anchoredPosition = plainButtonOrigin + new Vector2(offset, 0f);
    }

    void FocusFirstItem()
    {
        foreach (var item in items)
            if (item != null && item.CanSelect)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(item.Selectable.gameObject);
                break;
            }
    }

    // One unscaled clock evaluates overlapping tracks. This gives DOTween and
    // coroutine fallback identical timings, including interruption poses.
    void PlayTimeline(float duration, Action<float> sample, Action complete)
    {
        clock = 0f;
#if DOTWEEN
        // Drive only this sequence with a bounded presentation clock. A raw
        // independent tween can consume the entire wipe on the first heavy
        // frame after switching from the exploration map/camera.
        var timeline = DOTween.Sequence().SetUpdate(UpdateType.Manual, true).SetTarget(this).SetAutoKill(true);
        sequence = timeline;
        timeline.Append(DOTween.To(() => clock, t =>
        {
            if (sequence != timeline) return;
            clock = t;
            sample(t);
        }, duration, duration).SetEase(DG.Tweening.Ease.Linear));
        timeline.OnComplete(() =>
        {
            if (sequence != timeline) return;
            sequence = null;
            complete();
        });
#else
        fallbackRoutine = StartCoroutine(PlayWithoutDOTween(duration, sample, complete));
#endif
    }

    IEnumerator PlayWithoutDOTween(float duration, Action<float> sample, Action complete)
    {
        while (clock < duration)
        {
            yield return null;
            clock = Mathf.Min(duration, clock + FrameStep(Time.unscaledDeltaTime));
            sample(clock);
        }
        fallbackRoutine = null;
        complete();
    }

    void KillTimeline()
    {
#if DOTWEEN
        var previousTimeline = sequence;
        sequence = null;
        if (previousTimeline != null)
        {
            if (Application.isPlaying) previousTimeline.Kill(false);
            // DOTween's instance Kill is disabled before its runtime initialization.
            else DOTween.Kill(this, false);
        }
#endif
        if (fallbackRoutine != null) StopCoroutine(fallbackRoutine);
        fallbackRoutine = null;
    }

    static float Segment(float time, float start, float duration) => Mathf.Clamp01((time - start) / Mathf.Max(.001f, duration));

    float FrameStep(float unscaledDelta) => Mathf.Min(Mathf.Max(0f, unscaledDelta), Mathf.Clamp(maximumFrameStep, .016f, .1f));

    void AdvanceTimeline(float unscaledDelta)
    {
#if DOTWEEN
        var timeline = sequence;
        if (timeline != null && timeline.IsActive())
        {
            float step = FrameStep(unscaledDelta);
            timeline.ManualUpdate(step, step);
        }
#endif
    }

    public static float Ease(float t, MotionEase ease, float back = 1.4f)
    {
        t = Mathf.Clamp01(t);
        switch (ease)
        {
            case MotionEase.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
            case MotionEase.OutBack: float v = t - 1f; return 1f + (back + 1f) * v * v * v + back * v * v;
            case MotionEase.InOutCubic: return t < .5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * .5f;
            default: return t;
        }
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (!IsVisible) return;
        returnNavigationFocus = true;
        if (cancelCallback != null) cancelCallback();
        else CloseSkillPanel();
        eventData?.Use();
    }

    void Update()
    {
        if (IsVisible && (activePanel == null || !activePanel.gameObject.activeInHierarchy))
        {
            ResetTransition();
            return;
        }
        // The opening frame's delta belongs to the frame before the click.
        if (IsTransitioning && Time.frameCount != openingFrame) AdvanceTimeline(Time.unscaledDeltaTime);
    }
    void OnDisable() => ResetTransition();
}
