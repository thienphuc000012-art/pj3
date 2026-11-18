using System.Collections.Generic;
using UnityEngine;

/// <summary>Authored map points, including points in unloaded chunks; used by the save-point cheat.</summary>
public sealed class AdventureRestPointCatalog : ScriptableObject
{
    public List<UnlockedRestPoint> points = new List<UnlockedRestPoint>();
    public static UnlockedRestPoint FromPoint(WorldInteraction point, string mapScene)
    {
        Vector3 position = point.transform.position + point.transform.forward * Mathf.Max(1.5f, point.range * .7f);
        return new UnlockedRestPoint { id = point.Id, displayName = point.displayName, sceneName = mapScene, position = position, rotation = point.transform.rotation };
    }
}
