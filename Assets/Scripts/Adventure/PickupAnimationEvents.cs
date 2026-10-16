using System;
using UnityEngine;

// Added automatically to the map player's Animator; never grants rewards by itself.
public sealed class PickupAnimationEvents : MonoBehaviour
{
    public Action OnPickupMoment;
    public Action OnChestOpenMoment;
    public void AnimEvent_OpenChest() => OnChestOpenMoment?.Invoke();
    public void AnimEvent_PickupItem() => OnPickupMoment?.Invoke();
}
