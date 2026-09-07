using UnityEngine;

public class ShimmyController : MonoBehaviour
{
    PlayerClimb playerClimbScript;

    public float sphereRadius;
    public float sphereGap;

    public float rayHeight = 1.6f;
    public float rayLength = 1.0f;

    public bool canMoveRight;
    public bool canMoveLeft;

    public RaycastHit ledgeHit;

    public bool leftBtn;
    public bool rightBtn;
    public float ledgeMoveSpeed = 0.5f;
    float horizontalValue;

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

        // 2. KHÓA SHIMMY KHI ĐANG THỰC HIỆN ANIMATION KHÁC (Hop up, Hop down, Drop, Climb roof...)
        AnimatorStateInfo state = playerClimbScript.animator.GetCurrentAnimatorStateInfo(0);
        bool isPlayingAction = state.IsName("braced hang hop up") ||
                               state.IsName("brace hang drop") ||
                               state.IsName("bracehangdrop") ||
                               state.IsName("droptofreehang") ||
                               state.IsName("climbuproof");

        // Nếu đang kẹt animation hành động hoặc đang chuyển đổi animation (transition) -> Không cho di chuyển
        if (isPlayingAction || playerClimbScript.animator.IsInTransition(0))
        {
            ResetShimmy();
            return;
        }

        // 3. HỆ THỐNG RAYCAST KÉP (Dành cho cả tường và mái nhà)
        Vector3 rayStart = transform.position + Vector3.up * rayHeight;
        Debug.DrawRay(rayStart, transform.forward * rayLength, Color.magenta); // Tia chuẩn

        // Bắn tia chuẩn trước
        bool hit = Physics.Raycast(rayStart, transform.forward, out ledgeHit, rayLength, playerClimbScript.ledgeLayer);

        // Nếu tia chuẩn hụt (xảy ra khi ở trên mái nhà mỏng), bắn một tia dự phòng thấp hơn
        if (!hit)
        {
            Vector3 lowerRayStart = rayStart - Vector3.up * 0.3f;
            Debug.DrawRay(lowerRayStart, transform.forward * rayLength, Color.cyan); // Tia dự phòng thấp
            hit = Physics.Raycast(lowerRayStart, transform.forward, out ledgeHit, rayLength, playerClimbScript.ledgeLayer);
        }

        // 4. Nếu trúng vách, kiểm tra di chuyển
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
        if (ledgeHit.point != Vector3.zero)
        {
            // Kiểm tra xem 2 bên trái/phải có còn đường để bám không
            canMoveRight = Physics.CheckSphere(ledgeHit.point + transform.right * sphereGap, sphereRadius, playerClimbScript.ledgeLayer);
            canMoveLeft = Physics.CheckSphere(ledgeHit.point - transform.right * sphereGap, sphereRadius, playerClimbScript.ledgeLayer);

            // Nhận Input độc lập, không dùng if/else đè lên nhau nữa
            bool inputRight = Input.GetKey(KeyCode.D);
            bool inputLeft = Input.GetKey(KeyCode.A);

            // Chỉ cho phép gán nút nếu hướng đó có thể di chuyển (có tường)
            rightBtn = inputRight && canMoveRight;
            leftBtn = inputLeft && canMoveLeft;

            // Xử lý chống bấm 2 nút cùng lúc
            if (leftBtn && rightBtn)
            {
                leftBtn = false;
                rightBtn = false;
            }

            // Gán giá trị di chuyển
            if (leftBtn)
                horizontalValue = -1f;
            else if (rightBtn)
                horizontalValue = 1f;
            else
                horizontalValue = 0f;
        }
        else
        {
            ResetShimmy();
            return; // Dừng hàm nếu không hit point
        }

        // Truyền giá trị vào Animator và Di chuyển Transform
        playerClimbScript.animator.SetFloat("movementvalue", horizontalValue, 0.05f, Time.deltaTime);

        if (horizontalValue != 0)
        {
            transform.position += transform.right * horizontalValue * ledgeMoveSpeed * Time.deltaTime;
        }
    }

    // Đưa mọi thông số về 0 để tránh kẹt
    void ResetShimmy()
    {
        leftBtn = false;
        rightBtn = false;
        horizontalValue = 0;

        // SỬA LỖI Ở ĐÂY: Chỉ ép movementvalue về 0 NẾU ĐANG LEO TRÈO. 
        // Nếu ở dưới đất, trả toàn quyền điều khiển Animator cho PlayerScript.
        if (playerClimbScript.isClimbing)
        {
            playerClimbScript.animator.SetFloat("movementvalue", 0, 0.05f, Time.deltaTime);
        }
    }

    private void OnDrawGizmos()
    {
        if (ledgeHit.point != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(ledgeHit.point + transform.right * sphereGap, sphereRadius);
            Gizmos.DrawSphere(ledgeHit.point - transform.right * sphereGap, sphereRadius);
        }
    }
}