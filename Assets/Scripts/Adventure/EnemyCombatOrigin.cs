using UnityEngine;

/// <summary>Stores the authored world pose independently of patrol and combat movement.</summary>
[DisallowMultipleComponent]
public sealed class EnemyCombatOrigin : MonoBehaviour
{
    public bool HasSpawnPoint { get; private set; }
    public Vector3 SpawnPosition { get; private set; }
    public Quaternion SpawnRotation { get; private set; } = Quaternion.identity;
    internal uint Revision { get; private set; }

    public void CaptureSpawnPoint()
    {
        if (!HasSpawnPoint)
            SetSpawnPoint(transform.position, transform.rotation);
    }

    // Pool/spawner contract: place the actor, set its new spawn pose, then enable patrol.
    public void SetSpawnPoint(Vector3 position, Quaternion rotation)
    {
        SpawnPosition = position;
        SpawnRotation = rotation;
        HasSpawnPoint = true;
        Revision++;
    }

    public void ResetSpawnPoint()
    {
        HasSpawnPoint = false;
        SpawnPosition = default;
        SpawnRotation = Quaternion.identity;
        Revision++;
    }

    public bool TryGetSpawnPoint(out Pose pose)
    {
        pose = HasSpawnPoint ? new Pose(SpawnPosition, SpawnRotation) : default;
        return HasSpawnPoint;
    }

    internal static EnemyCombatOrigin CaptureFor(WorldInteraction encounter)
    {
        var origin = encounter.GetComponent<EnemyCombatOrigin>();
        if (origin == null) origin = encounter.gameObject.AddComponent<EnemyCombatOrigin>();
        origin.CaptureSpawnPoint();
        return origin;
    }
}
