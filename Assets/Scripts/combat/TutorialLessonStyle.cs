using UnityEngine;

/// <summary>Shared authored art for exploration and combat lessons.</summary>
[CreateAssetMenu(menuName = "Adventure/UI/Tutorial Lesson Style")]
public sealed class TutorialLessonStyle : ScriptableObject
{
    public Sprite inkBrush;
    public Material inkMaterial;
    [Range(0f, 1f)] public float opacity = .94f;
    [Range(0f, .5f)] public float brushOverhang = .28f;
    public Color ornamentColor = new Color(.69f, .60f, .42f, .09f);
    public Color borderColor = new Color(.80f, .77f, .65f, .42f);
}
