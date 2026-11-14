using System.Collections;
using UnityEngine;

public class LedgeToRoofClimb : MonoBehaviour
{
    PlayerClimb playerClimb;

    public LayerMask ledgeGroundLayer;

    [Header("Roof Climb Detection")]
    public float rayHeight = 1.8f;
    public float roofInset = 0.45f;         // Đưa ray vào BÊN TRONG mái, tránh bắn đúng mép collider.
    public float roofProbeHeight = 0.8f;
    public float roofProbeDistance = 1.5f;
    [Min(0.1f)] public float maximumRoofRise = 1.5f;
    [Min(0f)] public float roofLandingSearchDistance = 0.6f;
    [Range(1, 8)] public int roofLandingSamples = 4;
    [Tooltip("Include surfaces the PlayerScript can walk on (for example Obstacle platforms), in addition to Ledge Ground Layer.")]
    public bool includePlayerGroundLayers = true;

    [Header("Roof Climb Match Target")]
    [Range(0f, 1f)] public float matchStartTime = 0.15f;
    [Range(0f, 1f)] public float matchEndTime = 0.92f;
    public float finalGroundPadding = 0.02f;

    RaycastHit ledgeToClimbHit;

    public bool foundLedgeToRoofClimb;

    public GameObject climbPointObjPrefab;
    public GameObject climbPointObj;

    bool isClimbingToRoof = false;
    public bool IsClimbingToRoof => isClimbingToRoof;

    // Target ROOT đứng an toàn trên mái. Không dùng RightFoot làm target nữa.
    Vector3 roofRootTarget;
    Quaternion roofTargetRotation;
    bool hasRoofRootTarget;

    private void Awake()
    {
        playerClimb = GetComponent<PlayerClimb>();
    }

    private void Update()
    {
        if (CampaignSession.InputBlocked) return;
        if (!isClimbingToRoof)
        {
            if (playerClimb.CanStartLedgeAction) RefreshRoofTarget();
            else { foundLedgeToRoofClimb = false; hasRoofRootTarget = false; }
        }

        // FIX CHÍNH:
        // Match ROOT tới vị trí đứng trên mái thay vì Match RightFoot tới mặt sàn.
        // RightFoot có offset theo pose/clip; khi GrabLedge hoặc DropToLedgeHang làm root lệch,
        // Unity sẽ bù offset chân bằng cách kéo cả root xuống dưới mái.
        AnimatorStateInfo state = playerClimb.animator.GetCurrentAnimatorStateInfo(0);
        if (hasRoofRootTarget &&
            state.IsName("climbuproof") &&
            state.normalizedTime < matchEndTime &&
            !playerClimb.animator.isMatchingTarget &&
            !playerClimb.animator.IsInTransition(0))
        {
            playerClimb.animator.MatchTarget(
                roofRootTarget,
                roofTargetRotation,
                AvatarTarget.Root,
                new MatchTargetWeightMask(Vector3.one, 0f),
                matchStartTime,
                matchEndTime
            );
        }
    }

    /// <summary>Uses the committed ledge and walkable layers; does not depend on root-motion height or input order.</summary>
    public bool RefreshRoofTarget()
    {
        foundLedgeToRoofClimb = false; hasRoofRootTarget = false;
        if (playerClimb == null) playerClimb = GetComponent<PlayerClimb>();
        if (playerClimb == null || !playerClimb.HasStableLedge) return false;
        Vector3 forward = playerClimb.StableLedgeForward.normalized;
        Vector3 top = playerClimb.StableLedgeTopPoint;
        int mask = ledgeGroundLayer;
        var movement = GetComponent<PlayerScript>();
        if (includePlayerGroundLayers && movement != null) mask |= movement.surfaceLayer;
        var cc = GetComponent<CharacterController>();
        float height = Mathf.Max(roofProbeHeight, maximumRoofRise + .05f);
        int count = Mathf.Max(1, roofLandingSamples);
        for (int i = 0; i < count; i++)
        {
            float inset = roofInset + (count <= 1 ? 0 : roofLandingSearchDistance * i / (count - 1));
            Vector3 origin = top + forward * inset + Vector3.up * height;
            Debug.DrawRay(origin, Vector3.down * (height + Mathf.Max(.1f, roofProbeDistance - roofProbeHeight)), Color.blue);
            if (!Physics.Raycast(origin, Vector3.down, out var ground, height + Mathf.Max(.1f, roofProbeDistance - roofProbeHeight), mask, QueryTriggerInteraction.Ignore)) continue;
            float rise = ground.point.y - top.y;
            if (ground.normal.y < .6f || rise < -.15f || rise > maximumRoofRise) continue;
            Vector3 target = GetStandingRootPosition(ground.point);
            // Check above the proposed landing surface, never from transform.position.y during a hop.
            Vector3 clearanceOrigin = new Vector3(top.x, ground.point.y + Mathf.Max(.1f, rayHeight - playerClimb.rayHeight), top.z) - forward * .15f;
            if (Physics.Raycast(clearanceOrigin, forward, inset + .15f, mask, QueryTriggerInteraction.Ignore)) continue;
            if (cc != null)
            {
                float radius = cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) * .9f;
                float bodyHeight = Mathf.Max(radius * 2, cc.height * Mathf.Abs(transform.lossyScale.y));
                Vector3 center = target + transform.rotation * Vector3.Scale(cc.center, transform.lossyScale);
                Vector3 bottom = center - Vector3.up * (bodyHeight * .5f - radius);
                Vector3 upper = center + Vector3.up * (bodyHeight * .5f - radius);
                if (Physics.CheckCapsule(bottom, upper, radius, mask, QueryTriggerInteraction.Ignore)) continue;
            }
            ledgeToClimbHit = ground; roofRootTarget = target; roofTargetRotation = Quaternion.LookRotation(forward);
            foundLedgeToRoofClimb = true; hasRoofRootTarget = true;
            return true;
        }
        return false;
    }

    public bool TryBeginRoofClimb()
    {
        if (playerClimb == null || !playerClimb.CanStartLedgeAction || !RefreshRoofTarget()) return false;
        isClimbingToRoof = true;
        playerClimb.BeginRoofClimbInputLock();
        playerClimb.animator.SetFloat("movementvalue", 0f);
        if (climbPointObjPrefab != null) climbPointObj = Instantiate(climbPointObjPrefab, roofRootTarget, roofTargetRotation);
        StartCoroutine(LedgeToClimb());
        return true;
    }

    IEnumerator LedgeToClimb()
    {
        if (!hasRoofRootTarget)
        {
            isClimbingToRoof = false;
            yield break;
        }

        playerClimb.animator.CrossFade("climbuproof", 0.2f);

        // Chờ vào animation.
        yield return null;

        while (playerClimb.animator.IsInTransition(0))
            yield return null;

        while (playerClimb.animator.GetCurrentAnimatorStateInfo(0).IsName("climbuproof") || playerClimb.animator.IsInTransition(0))
        {
            if (!playerClimb.animator.IsInTransition(0) && playerClimb.animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f) break;
            yield return null;
        }

        // SNAP CHẮC CHẮN ROOT lên mái trước khi bật CharacterController lại.
        // Vì CC đang bị khóa trong BeginRoofClimbInputLock nên không bị physics đẩy ngược trong lúc snap.
        transform.position = roofRootTarget;
        transform.rotation = roofTargetRotation;
        Physics.SyncTransforms();

        if (climbPointObj != null)
        {
            Destroy(climbPointObj);
            climbPointObj = null;
        }

        playerClimb.animator.SetFloat("movementvalue", 0f);

        // FIX GIỮ W:
        // Không chờ người chơi nhả W ở đây.
        // Nếu chờ, roofClimbInputLocked vẫn giữ Player ở ClimbingState,
        // CharacterController tiếp tục bị tắt và Root Motion tiếp tục bật.
        // Khi clip climbuproof đã kết thúc, state kế tiếp có thể kéo root lệch/rơi xuống.
        //
        // Sau khi đã snap root lên mái thì trả control NGAY.
        playerClimb.EndRoofClimbInputLock();

        // Đồng bộ lại sau khi CharacterController được bật.
        Physics.SyncTransforms();

        hasRoofRootTarget = false;
        isClimbingToRoof = false;
    }

    Vector3 GetStandingRootPosition(Vector3 roofSurfacePoint)
    {
        Vector3 result = roofSurfacePoint;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float bottomOffset = (cc.center.y - cc.height * 0.5f) * scaleY;

            result.y =
                roofSurfacePoint.y
                - bottomOffset
                + cc.skinWidth
                + finalGroundPadding;
        }

        return result;
    }
}
