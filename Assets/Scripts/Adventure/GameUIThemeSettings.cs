using UnityEngine;

[CreateAssetMenu(menuName = "Adventure/UI Theme Settings")]
public sealed class GameUIThemeSettings : ScriptableObject
{
    public Sprite buttonBackground;
    public Sprite skillBackground;
    public Sprite dialogueBackground;
    public Sprite combatActionBackground;
    public Sprite combatSubMenuBackground;
    public AudioClip combatButtonPressSound;
}
