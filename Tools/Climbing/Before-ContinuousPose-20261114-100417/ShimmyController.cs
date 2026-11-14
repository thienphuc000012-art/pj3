using UnityEngine;

[DefaultExecutionOrder(100)]
public class ShimmyController : MonoBehaviour
{
    PlayerClimb playerClimbScript;

    [Header("Shimmy Side Check")]
    public float sphereRadius = 0.08f; // Chỉ dùng để vẽ Gizmo cho dễ nhìn
    public float sphereGap = 0.35f;
    public float sideProbeOutset = 0.12f; // Đẩy điểm ray ra khỏi mặt tường
    public float sideProbeDepth = 0.25f;  // Độ sâu ray bắn ngược vào tường

    public float rayHeight = 1.6f;
    public float rayLength = 1.0f;
    [Tooltip("Wall probe height below the committed ledge, independent of the player's hanging pose.")]
    [Min(0.01f)] public float ledgeProbeBelowTop = 0.1f;
    [Tooltip("Maximum height change allowed while moving sideways; larger changes require a hop.")]
    [Min(0f)] public float maxShimmyHeightChange = 0.15f;

    public bool canMoveRight;
    public bool canMoveLeft;

    public RaycastHit ledgeHit;

    public bool leftBtn;
    public bool rightBtn;
    public float ledgeMoveSpeed = 0.5f;
    float horizontalValue;

    [Header("Hanging Foot Clearance")]
    public bool keepFeetOutsideWall = true;
    [Tooltip("Minimum foot/toe bone distance outside the hanging wall plane, in metres.")]
    [Min(0f)] public float footWallPadding = 0.06f;
    [Tooltip("Maximum pose correction per leg. Increase for clips whose feet pass far through the wall.")]
    [Min(0f)] public float maxFootWallCorrection = 0.45f;
    Animator footAnimator;
    Transform leftThigh, leftCalf, leftFoot, leftToe, rightThigh, rightCalf, rightFoot, rightToe;

    // Hướng ngang thật sự của mép tường.
    // Không dùng transform.right trực tiếp vì rotation của Player có thể lệch vài độ.
    Vector3 ledgeTangent;
    Vector3 currentWallNormal;

    private void Awake()
    {
        playerClimbScript = GetComponent<PlayerClimb>();
    }

    private void Update()
    {
        float input = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        StepShimmy(input, Time.deltaTime);
    }

    private void LateUpdate() { CorrectHangingFeet(); }

    /// <summary>Pose correction after animation: keep the feet outside the wall without moving the root or hands.</summary>
    public void CorrectHangingFeet()
    {
        if (playerClimbScript == null) playerClimbScript = GetComponent<PlayerClimb>();
        if (!keepFeetOutsideWall || playerClimbScript == null || !playerClimbScript.UsesManualHangingMovement || CampaignSession.InputBlocked) return;
        var anim = playerClimbScript.animator;
        if (!anim.isHuman) return;
        if (footAnimator != anim)
        {
            footAnimator = anim;
            leftThigh = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg); leftCalf = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot); leftToe = anim.GetBoneTransform(HumanBodyBones.LeftToes);
            rightThigh = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg); rightCalf = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot); rightToe = anim.GetBoneTransform(HumanBodyBones.RightToes);
        }
        Vector3 normal = playerClimbScript.StableLedgeWallNormal;
        Vector3 plane = playerClimbScript.StableLedgeWallPoint;
        CorrectLeg(leftThigh, leftCalf, leftFoot, leftToe, plane, normal);
        CorrectLeg(rightThigh, rightCalf, rightFoot, rightToe, plane, normal);
    }

    void CorrectLeg(Transform thigh, Transform calf, Transform foot, Transform toe, Vector3 plane, Vector3 normal)
    {
        if (thigh == null || calf == null || foot == null) return;
        float distance = Vector3.Dot(foot.position - plane, normal);
        if (toe != null) distance = Mathf.Min(distance, Vector3.Dot(toe.position - plane, normal));
        float push = Mathf.Clamp(footWallPadding - distance, 0, maxFootWallCorrection);
        if (push <= 0.00001f) return;
        Vector3 hip = thigh.position, knee = calf.position, ankle = foot.position;
        Quaternion footRotation = foot.rotation;
        float upperLength = Vector3.Distance(hip, knee), lowerLength = Vector3.Distance(knee, ankle);
        Vector3 target = ankle + normal * push;
        Vector3 axis = target - hip;
        if (axis.sqrMagnitude < .000001f || upperLength < .0001f || lowerLength < .0001f) return;
        float maxReach = upperLength + lowerLength - .0001f;
        if (axis.sqrMagnitude > maxReach * maxReach)
        {
            // Preserve the requested wall clearance when the leg is nearly straight.
            // Clamping the entire vector would pull the toes back through the wall.
            float outward = Mathf.Clamp(Vector3.Dot(axis, normal), -maxReach, maxReach);
            Vector3 alongWall = Vector3.ProjectOnPlane(axis, normal);
            float available = Mathf.Sqrt(Mathf.Max(0, maxReach * maxReach - outward * outward));
            target = hip + normal * outward + Vector3.ClampMagnitude(alongWall, available);
            axis = target - hip;
        }
        float reach = Mathf.Max(axis.magnitude, Mathf.Abs(upperLength - lowerLength) + .0001f);
        axis.Normalize(); target = hip + axis * reach;
        Vector3 bend = Vector3.ProjectOnPlane(knee - hip, axis);
        if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(normal, axis);
        if (bend.sqrMagnitude < .000001f) return;
        bend.Normalize();
        float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2 * reach);
        float height = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
        Vector3 kneeTarget = hip + axis * along + bend * height;
        thigh.rotation = Quaternion.FromToRotation(knee - hip, kneeTarget - hip) * thigh.rotation;
        calf.rotation = Quaternion.FromToRotation(foot.position - calf.position, target - calf.position) * calf.rotation;
        foot.rotation = footRotation;
    }

    /// <summary>Runs the same movement for keyboard input and isolated Editor checks.</summary>
    public void StepShimmy(float input, float deltaTime)
    {
        if (playerClimbScript == null) playerClimbScript = GetComponent<PlayerClimb>();
        if (playerClimbScript == null || CampaignSession.InputBlocked || !playerClimbScript.CanStartLedgeAction)
        {
            ResetShimmy();
            return;
        }
        var state = playerClimbScript.animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("bracehangdrop") || state.IsName("idle to hang") || state.IsName("droptofreehang"))
        {
            ResetShimmy();
            return;
        }

        currentWallNormal = playerClimbScript.StableLedgeWallNormal;
        currentWallNormal.y = 0;
        currentWallNormal.Normalize();
        // Right relative to the wall-facing direction, including diagonal walls.
        ledgeTangent = Vector3.Cross(Vector3.up, -currentWallNormal).normalized;
        Vector3 top = playerClimbScript.StableLedgeTopPoint;
        if (!TryProbeLedge(top, out ledgeHit, out _)) { ResetShimmy(); return; }

        float gap = Mathf.Max(0.01f, sphereGap);
        canMoveLeft = TryProbeLedge(top - ledgeTangent * gap, out _, out _);
        canMoveRight = TryProbeLedge(top + ledgeTangent * gap, out _, out _);
        leftBtn = input < -0.01f && canMoveLeft;
        rightBtn = input > 0.01f && canMoveRight;
        horizontalValue = leftBtn ? -1f : rightBtn ? 1f : 0;

        if (horizontalValue != 0 && deltaTime > 0)
        {
            float step = Mathf.Min(Mathf.Max(0, ledgeMoveSpeed) * deltaTime, gap);
            Vector3 next = top + ledgeTangent * horizontalValue * step;
            if (TryProbeLedge(next, out var nextWall, out var nextTop))
            {
                // Move the root and its anchor together, so Hop/RoofClimb cannot snap back to the old position.
                transform.position += nextTop.point - top;
                playerClimbScript.SetStableLedge(nextWall, nextTop);
                ledgeHit = nextWall;
            }
            else { leftBtn = false; rightBtn = false; horizontalValue = 0; }
        }
        playerClimbScript.animator.SetFloat("movementvalue", horizontalValue, 0.05f, Mathf.Max(0, deltaTime));
    }

    bool TryProbeLedge(Vector3 top, out RaycastHit wall, out RaycastHit surface)
    {
        wall = default; surface = default;
        Vector3 origin = top - Vector3.up * ledgeProbeBelowTop + currentWallNormal * Mathf.Max(.05f, sideProbeOutset);
        float length = Mathf.Max(.05f, sideProbeOutset) + Mathf.Max(.05f, sideProbeDepth);
        bool found = Physics.Raycast(origin, -currentWallNormal, out wall, length, playerClimbScript.ledgeLayer, QueryTriggerInteraction.Ignore);
        Debug.DrawRay(origin, -currentWallNormal * length, found ? Color.green : Color.red);
        if (!found || Mathf.Abs(wall.normal.y) > .5f || Vector3.Dot(wall.normal, currentWallNormal) < .8f) return false;
        Vector3 downOrigin = wall.point - currentWallNormal * .025f + Vector3.up * (ledgeProbeBelowTop + .25f);
        if (!Physics.Raycast(downOrigin, Vector3.down, out surface, ledgeProbeBelowTop + .5f,
            playerClimbScript.ledgeLayer, QueryTriggerInteraction.Ignore)) return false;
        return surface.normal.y >= .6f && Mathf.Abs(surface.point.y - playerClimbScript.StableLedgeTopPoint.y) <= maxShimmyHeightChange;
    }

    void ResetShimmy()
    {
        leftBtn = false;
        rightBtn = false;
        canMoveLeft = false;
        canMoveRight = false;
        horizontalValue = 0;
        if (playerClimbScript != null && playerClimbScript.isClimbing && playerClimbScript.animator != null)
            playerClimbScript.animator.SetFloat("movementvalue", 0, 0.05f, Time.deltaTime);
    }

    private void OnDrawGizmos()
    {
        if (ledgeHit.point == Vector3.zero)
            return;

        Vector3 normal = ledgeHit.normal;
        normal.y = 0f;

        if (normal.sqrMagnitude < 0.0001f)
            return;

        normal.Normalize();

        Vector3 tangent = Vector3.Cross(Vector3.up, normal).normalized;
        if (Vector3.Dot(tangent, transform.right) < 0f)
            tangent = -tangent;

        Vector3 rightPoint = ledgeHit.point + tangent * sphereGap;
        Vector3 leftPoint = ledgeHit.point - tangent * sphereGap;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(rightPoint, sphereRadius);
        Gizmos.DrawWireSphere(leftPoint, sphereRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(ledgeHit.point, rightPoint);
        Gizmos.DrawLine(ledgeHit.point, leftPoint);
    }
}
