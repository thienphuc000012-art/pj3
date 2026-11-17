using UnityEngine;

[DisallowMultipleComponent]
public sealed class QuestPortalTravelGate : MonoBehaviour
{
    public QuestPortalDefinition definition;
    public bool IsOpen => definition != null && definition.IsUnlocked(CampaignSession.Instance?.Data);
}
