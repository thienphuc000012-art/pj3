using UnityEngine;

public enum AdventureQuestObjectiveType
{
    CollectItem,
    DefeatEnemy
}

[CreateAssetMenu(menuName = "Adventure/Quest", fileName = "Quest")]
public sealed class AdventureQuest : ScriptableObject
{
    [Tooltip("Unique save key. Leave empty to use the asset name.")]
    public string questId;
    public string displayName = "New Quest";
    [TextArea(2, 6)] public string description;
    public AdventureQuestObjectiveType objectiveType;
    [Min(1)] public int requiredCount = 1;
    [Tooltip("Item ID from Campaign Config. Used for Collect Item objectives.")]
    public string targetItemId;
    [Tooltip("Enemy prefab to defeat. Leave empty to count enemies at the selected encounter point.")]
    public BattleUnit targetEnemy;
    [Tooltip("Optional WorldInteraction persistent ID used for the compass destination. If empty, a matching pickup or encounter is found automatically.")]
    public string objectiveWorldPointId;
    [Tooltip("Use this for objectives in unloaded chunks or a different map. Leave off to capture the matching WorldInteraction position when available.")]
    public bool useFixedObjectivePosition;
    [Tooltip("Tên scene chứa mục tiêu. Bắt buộc khi dùng tọa độ cố định; để trống để dùng scene hiện tại.")]
    public string objectiveMapScene;
    public Vector3 objectivePosition;

    public string Id => string.IsNullOrWhiteSpace(questId) ? name : questId.Trim();
}
