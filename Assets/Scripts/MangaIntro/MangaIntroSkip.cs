using UnityEngine;
using UnityEngine.Playables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Lets the player end the manga intro by holding Space.</summary>
[DisallowMultipleComponent]
public sealed class MangaIntroSkip : MonoBehaviour
{
    [SerializeField] PlayableDirector director;
    [SerializeField] GameObject prompt;
    [SerializeField] HoldProgressRingGraphic progress;
    [SerializeField] CanvasGroup fadeOverlay;
    [SerializeField, Min(.05f)] float holdSeconds = .85f;
    [SerializeField, Min(.1f)] float promptSeconds = 2f;

    double holdStartedAt = -1d;
    double promptUntil = -1d;
    bool skipped;

    void OnEnable()
    {
        if (director != null)
        {
            director.played += OnDirectorPlayed;
            director.stopped += OnDirectorStopped;
        }

        HidePrompt();
    }

    void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnDirectorPlayed;
            director.stopped -= OnDirectorStopped;
        }

        HidePrompt();
    }

    void Update()
    {
        bool playing = !skipped && director != null && director.isActiveAndEnabled &&
            director.state == PlayState.Playing;
        if (!playing)
        {
            HidePrompt();
            return;
        }

        double now = Time.unscaledTimeAsDouble;
        ReadSkipKeys(out bool spaceHeld, out bool revealPrompt);
        if (revealPrompt)
            promptUntil = now + promptSeconds;
        SetPromptVisible(now < promptUntil);

        if (!spaceHeld)
        {
            ResetHold();
            return;
        }

        if (holdStartedAt < 0d)
            holdStartedAt = now;

        float fraction = Mathf.Clamp01((float)((now - holdStartedAt) / Mathf.Max(.05f, holdSeconds)));
        if (progress != null)
        {
            if (!progress.gameObject.activeSelf)
                progress.gameObject.SetActive(true);
            progress.SetProgress(fraction);
        }

        if (fraction >= 1f)
        {
            skipped = true;
            director.Stop();
            SetOverlayBlack();
        }
    }

    void LateUpdate()
    {
        // Timeline can release animated values as its graph is torn down.
        if (skipped)
            SetOverlayBlack();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused)
            HidePrompt();
    }

    void OnDirectorPlayed(PlayableDirector _)
    {
        skipped = false;
        HidePrompt();
    }

    void OnDirectorStopped(PlayableDirector _)
        => HidePrompt();

    void HidePrompt()
    {
        promptUntil = -1d;
        ResetHold();
        SetPromptVisible(false);
    }

    void ResetHold()
    {
        holdStartedAt = -1d;
        if (progress != null && progress.gameObject.activeSelf)
        {
            progress.SetProgress(0f);
            progress.gameObject.SetActive(false);
        }
    }

    void SetPromptVisible(bool visible)
    {
        if (prompt != null && prompt.activeSelf != visible && !transform.IsChildOf(prompt.transform))
            prompt.SetActive(visible);
    }

    void SetOverlayBlack()
    {
        if (fadeOverlay != null && fadeOverlay.alpha != 1f)
            fadeOverlay.alpha = 1f;
    }

    static void ReadSkipKeys(out bool spaceHeld, out bool revealPrompt)
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            spaceHeld = keyboard.spaceKey.isPressed;
            revealPrompt = spaceHeld || keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.escapeKey.isPressed || keyboard.escapeKey.wasPressedThisFrame;
            return;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        spaceHeld = Input.GetKey(KeyCode.Space);
        revealPrompt = spaceHeld || Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKey(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Escape);
#else
        spaceHeld = false;
        revealPrompt = false;
#endif
    }
}
