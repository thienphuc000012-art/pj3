using UnityEngine;

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
