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

    private void Start()
    {
        playerClimbScript = GetComponent<PlayerClimb>();
    }

    private void Update()
    {
        // 1. Nếu không ở trạng thái leo trèo thì reset shimmy
        if (!playerClimbScript.isClimbing)
        {
            ResetShimmy();
            return;
        }

        // 2. KHÓA SHIMMY KHI ĐANG THỰC HIỆN ANIMATION KHÁC
        AnimatorStateInfo state = playerClimbScript.animator.GetCurrentAnimatorStateInfo(0);
        bool isPlayingAction = state.IsName("braced hang hop up") ||
                               state.IsName("brace hang drop") ||
                               state.IsName("bracehangdrop") ||
                               state.IsName("droptofreehang") ||
                               state.IsName("climbuproof");

        if (isPlayingAction || playerClimbScript.animator.IsInTransition(0))
        {
            ResetShimmy();
            return;
        }

        // 3. HỆ THỐNG RAYCAST KÉP
        Vector3 rayStart = transform.position + Vector3.up * rayHeight;
        Debug.DrawRay(rayStart, transform.forward * rayLength, Color.magenta);

        bool hit = Physics.Raycast(
            rayStart,
            transform.forward,
            out ledgeHit,
            rayLength,
            playerClimbScript.ledgeLayer,
            QueryTriggerInteraction.Ignore
        );

        if (!hit)
        {
            Vector3 lowerRayStart = rayStart - Vector3.up * 0.3f;
            Debug.DrawRay(lowerRayStart, transform.forward * rayLength, Color.cyan);

            hit = Physics.Raycast(
                lowerRayStart,
                transform.forward,
                out ledgeHit,
                rayLength,
                playerClimbScript.ledgeLayer,
                QueryTriggerInteraction.Ignore
            );
        }

        if (hit)
        {
            CheckSphere();
        }
        else
        {
            ResetShimmy();
        }
    }

    void CheckSphere()
    {
        if (ledgeHit.point == Vector3.zero)
        {
            ResetShimmy();
            return;
        }

        // Lấy normal ngang của mặt tường.
        currentWallNormal = ledgeHit.normal;
        currentWallNormal.y = 0f;

        if (currentWallNormal.sqrMagnitude < 0.0001f)
        {
            ResetShimmy();
            return;
        }

        currentWallNormal.Normalize();

        // Tính tangent song song với mặt tường từ normal.
        // Sau đó ép dấu để tangent luôn cùng phía với transform.right của Player.
        ledgeTangent = Vector3.Cross(Vector3.up, currentWallNormal).normalized;
        if (Vector3.Dot(ledgeTangent, transform.right) < 0f)
            ledgeTangent = -ledgeTangent;

        // Hai điểm trái/phải được tạo từ CÙNG một tangent -> luôn đối xứng.
        Vector3 rightPoint = ledgeHit.point + ledgeTangent * sphereGap;
        Vector3 leftPoint = ledgeHit.point - ledgeTangent * sphereGap;

        // Thay CheckSphere bằng ray từ ngoài mặt tường bắn ngược vào.
        // Nhờ vậy sphere không còn chạm "ké" mép collider và gây kẹt.
        canMoveRight = HasWallAtSide(rightPoint);
        canMoveLeft = HasWallAtSide(leftPoint);

        bool inputRight = Input.GetKey(KeyCode.D);
        bool inputLeft = Input.GetKey(KeyCode.A);

        rightBtn = inputRight && canMoveRight;
        leftBtn = inputLeft && canMoveLeft;

        if (leftBtn && rightBtn)
        {
            leftBtn = false;
            rightBtn = false;
        }

        if (leftBtn)
            horizontalValue = -1f;
        else if (rightBtn)
            horizontalValue = 1f;
        else
            horizontalValue = 0f;

        playerClimbScript.animator.SetFloat("movementvalue", horizontalValue, 0.05f, Time.deltaTime);

        if (horizontalValue != 0f)
        {
            // QUAN TRỌNG:
            // Di chuyển theo tangent thật của tường, KHÔNG dùng transform.right.
            // Điều này ngăn Player từ từ bị đẩy xiên vào trong/ra ngoài mép.
            transform.position += ledgeTangent * horizontalValue * ledgeMoveSpeed * Time.deltaTime;
        }
    }

    bool HasWallAtSide(Vector3 sidePoint)
    {
        // ledgeHit.normal thường hướng từ tường ra ngoài phía Player.
        // Đẩy origin ra ngoài rồi bắn ngược vào mặt tường.
        Vector3 origin = sidePoint + currentWallNormal * sideProbeOutset;
        float distance = sideProbeOutset + sideProbeDepth;

        bool hit = Physics.Raycast(
            origin,
            -currentWallNormal,
            out RaycastHit sideHit,
            distance,
            playerClimbScript.ledgeLayer,
            QueryTriggerInteraction.Ignore
        );

        Debug.DrawRay(
            origin,
            -currentWallNormal * distance,
            hit ? Color.green : Color.red
        );

        return hit;
    }

    void ResetShimmy()
    {
        leftBtn = false;
        rightBtn = false;
        horizontalValue = 0f;

        if (playerClimbScript.isClimbing)
        {
            playerClimbScript.animator.SetFloat("movementvalue", 0, 0.05f, Time.deltaTime);
        }
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
