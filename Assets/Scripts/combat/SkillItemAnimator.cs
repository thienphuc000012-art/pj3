using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Deterministic slide/opacity track for one skill row. The panel transition owns
/// timing and stagger; this component never starts a second competing tween.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
public sealed class SkillItemAnimator : MonoBehaviour, ICancelHandler
{
    [Tooltip("Optional cancel receiver. Assigned by SkillPanelTransitionController when the row enters.")]
    public SkillPanelTransitionController transitionController;
    [Tooltip("Optional visual child. Assign a child when a LayoutGroup drives the row itself. Existing anchors, rotation and scale are preserved.")]
    public RectTransform motionTarget;
    [Tooltip("Hidden position relative to the authored position, in local Canvas units.")]
    public Vector2 hiddenOffset = new Vector2(24f, 0f);
    [Tooltip("Optional fade group for generic rows. CombatSkillRowUI uses its own opacity multiplier instead.")]
    public CanvasGroup fadeGroup;

    RectTransform capturedTarget;
    CombatSkillRowUI skillRow;
    Selectable selectable;
    Vector2 authoredPosition;
    float authoredAlpha = 1f;
    bool captured;
    float progress = 1f;

    public float Progress => progress;
    public Selectable Selectable => selectable != null ? selectable : GetComponent<Selectable>();
    public bool CanSelect => Selectable != null && Selectable.IsActive() && Selectable.IsInteractable();

    /// <summary>Call after populating/layout, before applying any transition offset.</summary>
    public void Capture()
    {
        if (captured && capturedTarget != null) return;
        capturedTarget = motionTarget != null ? motionTarget : transform as RectTransform;
        if (capturedTarget == null) return;
        authoredPosition = capturedTarget.anchoredPosition;
        skillRow = GetComponent<CombatSkillRowUI>();
        selectable = GetComponent<Selectable>();
        if (skillRow == null)
        {
            if (fadeGroup == null) fadeGroup = capturedTarget.GetComponent<CanvasGroup>();
            if (fadeGroup == null) fadeGroup = capturedTarget.gameObject.AddComponent<CanvasGroup>();
            authoredAlpha = fadeGroup.alpha;
        }
        captured = true;
    }

    /// <summary>Starts hidden. Unlock input only when the entire panel finishes.</summary>
    public void Prepare()
    {
        Capture();
        SetInteractionEnabled(false);
        SetProgress(0f);
    }

    /// <summary>Receives eased visibility from the panel's single animation timeline.</summary>
    public void SetProgress(float visibility)
    {
        Capture();
        if (!captured || capturedTarget == null) return;
        // Permit a small positional overshoot from OutBack; alpha stays valid.
        progress = visibility;
        capturedTarget.anchoredPosition = authoredPosition + hiddenOffset * (1f - visibility);
        if (skillRow != null) skillRow.SetTransitionAlpha(Mathf.Clamp01(visibility));
        else if (fadeGroup != null) fadeGroup.alpha = authoredAlpha * Mathf.Clamp01(visibility);
    }

    public void SetInteractionEnabled(bool value)
    {
        Capture();
        if (skillRow != null) skillRow.SetTransitionInputEnabled(value);
        // The panel's CanvasGroup owns raycasts and generic Selectable input.
        // Never overwrite Button.interactable, which is owned by resource checks.
    }

    /// <summary>Restores the authored state after close, cancellation or disable.</summary>
    public void Restore()
    {
        if (!captured) return;
        progress = 1f;
        if (capturedTarget != null) capturedTarget.anchoredPosition = authoredPosition;
        if (skillRow != null)
        {
            skillRow.SetTransitionAlpha(1f);
            skillRow.SetTransitionInputEnabled(true);
        }
        else if (fadeGroup != null) fadeGroup.alpha = authoredAlpha;
    }

    /// <summary>Restore before changing layout; the next Capture reads the new pose.</summary>
    public void ResetCapture()
    {
        Restore();
        captured = false;
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (transitionController == null) transitionController = GetComponentInParent<SkillPanelTransitionController>();
        if (transitionController != null) transitionController.OnCancel(eventData);
        else if (transform.parent != null)
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.cancelHandler);
    }

    void OnDisable() => Restore();
    void OnDestroy() => Restore();
}
