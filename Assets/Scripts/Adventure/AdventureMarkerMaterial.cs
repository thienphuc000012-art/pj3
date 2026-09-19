using UnityEngine;
public class AdventureMarkerMaterial : MonoBehaviour
{
    public Material material;
    void OnDestroy() { if (material != null) Destroy(material); }
}
