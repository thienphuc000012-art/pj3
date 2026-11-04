using UnityEngine;
using UnityEngine.UI;

// Buff/debuff instances share a prefab with the larger enemy HUD.
[DisallowMultipleComponent]
public sealed class PartyHUDBuffLayout : MonoBehaviour
{
    [Min(1)] public float iconSize = 23;
    void OnTransformChildrenChanged()
    {
        foreach (Transform child in transform)
        {
            var layout = child.GetComponent<LayoutElement>();
            if (layout == null) layout = child.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = iconSize;
            layout.minHeight = layout.preferredHeight = iconSize;
            layout.flexibleWidth = layout.flexibleHeight = 0;
        }
    }
}
