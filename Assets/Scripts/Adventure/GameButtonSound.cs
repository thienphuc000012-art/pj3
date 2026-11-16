using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class GameButtonSound : MonoBehaviour
{
    Button button;
    Button.ButtonClickedEvent boundEvent;
    public static void Ensure(Button button)
    {
        if (button == null) return;
        var sound = button.GetComponent<GameButtonSound>();
        if (sound == null) sound = button.gameObject.AddComponent<GameButtonSound>();
        sound.Bind(true);
    }
    void Awake() => Bind(true);
    void OnEnable() => Bind(true);
    void LateUpdate() => Bind(false);
    void Bind(bool force)
    {
        if (button == null) button = GetComponent<Button>();
        if (!force && ReferenceEquals(boundEvent, button.onClick)) return;
        boundEvent?.RemoveListener(Play);
        boundEvent = button.onClick;
        boundEvent.RemoveListener(Play); boundEvent.AddListener(Play);
    }
    void OnDestroy() => boundEvent?.RemoveListener(Play);
    void Play()
    {
        // Button already validated pointer/keyboard activation before invoking onClick.
        // Its callback may have closed the panel by the time this listener runs.
        if (GetComponent<CombatActionButtonFeedback>() == null) GameAudio.Button();
    }
}
