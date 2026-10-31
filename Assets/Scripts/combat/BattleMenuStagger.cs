using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Starts child button entrances in the configured visual order.</summary>
[DisallowMultipleComponent]
public sealed class BattleMenuStagger : MonoBehaviour
{
    [Tooltip("Entrance order. Leave empty to use child hierarchy order.")]
    public List<BattleMenuButtonSlide> buttons = new List<BattleMenuButtonSlide>();
    [Range(0f, .2f)] public float staggerInterval = .055f;
    public bool selectFirstButton = true;
    Coroutine pendingEntrance;

    void OnEnable() => Play();

    public void Play()
    {
        if (!isActiveAndEnabled) return;
        if (pendingEntrance != null) StopCoroutine(pendingEntrance);
        pendingEntrance = StartCoroutine(PlayAfterLayout());
    }

    IEnumerator PlayAfterLayout()
    {
        // Runtime theme setup and LayoutGroups can finish before bases are read.
        yield return null;
        var ordered = buttons != null && buttons.Count > 0
            ? buttons : new List<BattleMenuButtonSlide>(GetComponentsInChildren<BattleMenuButtonSlide>(true));
        var seen = new HashSet<BattleMenuButtonSlide>();
        int index = 0;
        GameObject first = null;
        foreach (var item in ordered)
        {
            if (item == null || !item.isActiveAndEnabled || !seen.Add(item)) continue;
            item.PlayEntrance(index * staggerInterval);
            item.RefreshInputFocus();
            index++;
            var button = item.GetComponent<Button>();
            if (first == null && (button == null || button.IsInteractable())) first = item.gameObject;
        }
        if (selectFirstButton && first != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first);
        pendingEntrance = null;
    }

    void OnDisable()
    {
        if (pendingEntrance != null) StopCoroutine(pendingEntrance);
        pendingEntrance = null;
        var items = GetComponentsInChildren<BattleMenuButtonSlide>(true);
        foreach (var item in items) if (item != null) item.ResetImmediately();
    }
}
