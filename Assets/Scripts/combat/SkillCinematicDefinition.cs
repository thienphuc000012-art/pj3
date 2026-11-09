using UnityEngine;

/// <summary>Reusable camera Timeline assigned to one or more Action Data assets.</summary>
[CreateAssetMenu(fileName = "SkillCinematic", menuName = "Combat/Skill Cinematic")]
public sealed class SkillCinematicDefinition : ScriptableObject
{
    [Tooltip("Prefab with Skill Cinematic Rig and a Playable Director containing the Timeline and its prefab bindings.")]
    public SkillCinematicRig rigPrefab;
}
