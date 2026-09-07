using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class PlayerScript : MonoBehaviour
{
    [Header("player movement")]
    public float walkSpeed = 2f;
    public float runSpeed = 4f;
    float currentSpeed;

    public MainCameraController MCC;
    public EnviromentChecker enviromentChecker;
    public float rotspeed = 5f;
    Quaternion requiredRotation;
    bool playerControl = true;
    public bool playerInAction { get; private set; }

    [Header("player Animator")]
    public Animator animator;

    private bool isClimbingLadder;
    private bool isMountingLadder; // Biến kiểm tra đang trong quá trình xoay/bước lên/xuống thang
    private Vector3 lastGrabLadderDirection;

    [Header("Player Collision & Gravity")]
    public CharacterController CC;
    public float surfaceCheckRadius = 0.2f;
    public Vector3 surfaceCheckOffset;
    public LayerMask surfaceLayer;
    public bool onSurface { get; private set; }
    public bool playerOnLedge { get; set; }
    public bool playerHanging { get; set; }
    public LedgeInfo LedgeInfo { get; set; }

    [SerializeField] float fallingSpeed;
    [SerializeField] Vector3 moveDir;
    [SerializeField] Vector3 requiredMoveDir;
    Vector3 velocity;

    [Header("Jump Settings")]
    public float jumpForce = 5f;
    bool wasOnSurface;

    private void Update()
    {
        if (!playerControl || playerHanging || isMountingLadder)
            return;

        velocity = Vector3.zero;

        wasOnSurface = onSurface;
        SurfaceCheck();

        // KIỂM TRA TIẾP ĐẤT (LANDING)
        if (!wasOnSurface && onSurface && !playerInAction && !isClimbingLadder)
        {
            animator.SetTrigger("Land");
        }

        if (onSurface && !isClimbingLadder)
        {
            if (fallingSpeed <= 0f)
            {
                fallingSpeed = -2f;
            }

            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            currentSpeed = isRunning ? runSpeed : walkSpeed;

            velocity = moveDir * currentSpeed;

            playerOnLedge = enviromentChecker.CheckLedge(moveDir, out LedgeInfo ledgeInfo);
            if (playerOnLedge)
            {
                LedgeInfo = ledgeInfo;
                playerLedgeMovement();
            }

            float animValue = velocity.magnitude / runSpeed;
            animator.SetFloat("movementvalue", animValue, 0.1f, Time.deltaTime);
        }
        else if (!isClimbingLadder)
        {
            fallingSpeed += Physics.gravity.y * Time.deltaTime;
            velocity = transform.forward * walkSpeed / 2;
        }

        if (!isClimbingLadder)
        {
            velocity.y = fallingSpeed;
        }

        PlayerMovement();

        // Cập nhật thông số Animator
        animator.SetBool("OnSurface", onSurface);
        animator.SetFloat("VerticalSpeed", fallingSpeed);

        bool isFalling = !onSurface && fallingSpeed <= 0.1f && !isClimbingLadder;
        animator.SetBool("IsFalling", isFalling);

        // --- CẬP NHẬT ANIMATOR CHO LEO THANG ---
        animator.SetBool("IsClimbing", isClimbingLadder);
    }

    public void PerformJump()
    {
        fallingSpeed = jumpForce;
        animator.SetTrigger("Jump");
    }

    void PlayerMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        float movementAmount = Mathf.Clamp01(Mathf.Abs(horizontal) + Mathf.Abs(vertical));
        var movementInput = (new Vector3(horizontal, 0, vertical)).normalized;

        requiredMoveDir = MCC.flatRotation * movementInput;

        // --- LOGIC LADDER (LEO THANG) ---
        float ladderGrabDistance = 0.6f;
        float ccHeight = (CC != null) ? CC.height : 2.0f;

        // Vị trí kiểm tra tại mốc 2/3 chiều cao CC và chân
        Vector3 rayStartPosTwoThirds = transform.position + Vector3.up * (ccHeight * (2f / 3f));
        Vector3 rayStartPosFeet = transform.position + Vector3.up * 0.2f;

        if (!isClimbingLadder)
        {
            // 1. TÌM THANG
            Vector3 searchDir = movementAmount > 0 ? requiredMoveDir : transform.forward;

            bool foundForward = Physics.Raycast(rayStartPosTwoThirds, searchDir, out RaycastHit hit, ladderGrabDistance) ||
                                Physics.Raycast(rayStartPosFeet, searchDir, out hit, ladderGrabDistance);

            bool foundBelow = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hitBelow, 1.2f);

            if (foundForward && hit.transform.TryGetComponent(out Ladder ladderForward))
            {
                // 1. Lấy trục ngang (Width) của Thang trong World Space
                Vector3 ladderRight = ladderForward.transform.right;
                ladderRight.y = 0;
                ladderRight.Normalize();

                // 2. Lấy hướng pháp tuyến mặt va chạm (loại bỏ độ nghiêng Y)
                Vector3 hitNormalXZ = new Vector3(hit.normal.x, 0, hit.normal.z).normalized;

                // 3. LỌC MẶT HÔNG: Điểm va chạm phải là Mặt Trước/Sau (loại bỏ mặt bên cột thang)
                bool isFrontFace = Mathf.Abs(Vector3.Dot(hitNormalXZ, ladderRight)) < 0.3f;

                // 4. BỀ MẶT ĐỨNG: Tránh va chạm vào đỉnh thang hoặc mặt sàn
                bool isVerticalSurface = Mathf.Abs(hit.normal.y) < 0.3f;

                // 5. HƯỚNG DI CHUYỂN: Người chơi phải đi trực diện VÀO mặt thang
                bool isFacingLadder = Vector3.Dot(searchDir, -hitNormalXZ) > 0.5f;

                // 6. TÍNH KHOẢNG CÁCH TỪ TÂM COLLIDER (Tránh lỗi do Pivot model nằm ở góc)
                Vector3 ladderCenter = hit.collider.bounds.center;
                Vector3 hitOffset = hit.point - ladderCenter;
                float distancePointFromCenter = Mathf.Abs(Vector3.Dot(hitOffset, ladderRight));

                // GIỚI HẠN VÙNG BÁM LEO:
                // Giảm xuống 0.15f (tổng chiều rộng vùng bám = 0.3m) để loại bỏ hoàn toàn 2 cột viền
                float maxClimbWidth = 0.15f;
                bool isWithinCenterWidth = distancePointFromCenter < maxClimbWidth;

                // CHỈ BÁM THANG KHI THỎA MÃN TẤT CẢ ĐIỀU KIỆN
                if (isFrontFace && isVerticalSurface && isFacingLadder && isWithinCenterWidth)
                {
                    if (movementAmount > 0 || !onSurface)
                    {
                        // Tự động ép điểm bám về đúng TRỤC GIỮA của thang theo chiều ngang
                        Vector3 centeredHitPoint = hit.point - (Vector3.Dot(hitOffset, ladderRight) * ladderRight);
                        GrabLadder(-hitNormalXZ, centeredHitPoint);
                    }
                }
            }
            else if (foundBelow && hitBelow.transform.TryGetComponent(out Ladder ladderBelow) && vertical < 0 && !isMountingLadder)
            {
                Vector3 outwardNormal = transform.forward;
                Vector3 probePos = hitBelow.point + transform.forward * 0.6f + Vector3.down * 0.3f;

                if (Physics.Raycast(probePos, -transform.forward, out RaycastHit faceHit, 1.0f) && faceHit.transform.TryGetComponent(out Ladder _))
                {
                    outwardNormal = faceHit.normal;
                }
                else
                {
                    Vector3 ladderFwd = ladderBelow.transform.forward;
                    if (Vector3.Dot(transform.forward, ladderFwd) < 0)
                        ladderFwd = -ladderFwd;
                    outwardNormal = ladderFwd;
                }

                outwardNormal.y = 0;
                outwardNormal.Normalize();

                Vector3 grabDir = -outwardNormal;
                Vector3 targetPos = hitBelow.point + outwardNormal * 0.35f;
                targetPos.y = hitBelow.point.y - 0.8f;

                StartCoroutine(ClimbDownFromTopRoutine(targetPos, grabDir));
                return;
            }
        }
        else
        {
            // 2. KHI ĐANG LEO: Kiểm tra va chạm ở mốc 2/3 CC và Chân
            bool twoThirdsHit = Physics.Raycast(rayStartPosTwoThirds, lastGrabLadderDirection, out RaycastHit hitTwoThirds, ladderGrabDistance);
            bool feetHit = Physics.Raycast(rayStartPosFeet, lastGrabLadderDirection, out RaycastHit hitFeet, ladderGrabDistance);
            bool hitGround = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.3f, surfaceLayer) && vertical < 0;
            bool jumpOff = Input.GetKeyDown(KeyCode.Space);

            // Cập nhật tốc độ leo thang cho Blend Tree (Lên = 1, Idle = 0, Xuống = -1)
            animator.SetFloat("ClimbSpeed", vertical, 0.1f, Time.deltaTime);

            // LEO TỚI MỐC 2/3 CC -> TỰ ĐỘNG BƯỚC LÊN ĐỈNH
            if (vertical > 0 && !twoThirdsHit)
            {
                StartCoroutine(ClimbToTopSnapRoutine());
                return;
            }

            // Bỏ bám thang khi tuột chân, chạm đất hoặc bấm Space
            if ((!twoThirdsHit && !feetHit) || hitGround || jumpOff)
            {
                DropLadder();
                return;
            }
        }

        // --- THỰC THI DI CHUYỂN CUỐI CÙNG ---
        if (isClimbingLadder)
        {
            fallingSpeed = 0f;
            velocity = Vector3.zero;

            if (CC.enabled)
            {
                Vector3 climbVelocity = new Vector3(0, vertical * walkSpeed, 0);
                CC.Move(climbVelocity * Time.deltaTime);
            }

            if (lastGrabLadderDirection != Vector3.zero)
            {
                requiredRotation = Quaternion.LookRotation(lastGrabLadderDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, requiredRotation, rotspeed * 2f * Time.deltaTime);
            }
        }
        else
        {
            if (CC.enabled)
            {
                CC.Move(velocity * Time.deltaTime);
            }

            if (movementAmount > 0 && moveDir.magnitude > 0.2f)
            {
                requiredRotation = Quaternion.LookRotation(moveDir);
            }
            moveDir = requiredMoveDir;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, requiredRotation, rotspeed * Time.deltaTime);
        }
    }

    void SurfaceCheck()
    {
        if (isClimbingLadder)
        {
            onSurface = false;
            return;
        }

        onSurface = Physics.CheckSphere(transform.TransformPoint(surfaceCheckOffset), surfaceCheckRadius, surfaceLayer);
    }

    private void GrabLadder(Vector3 grabDirection, Vector3 hitPoint)
    {
        isClimbingLadder = true;
        grabDirection.y = 0;
        this.lastGrabLadderDirection = grabDirection.normalized;

        animator.applyRootMotion = false;

        // Căn chỉnh vị trí CC sát vào mặt thang
        Vector3 snapPos = transform.position;
        Vector3 ladderSurfacePos = hitPoint - lastGrabLadderDirection * 0.35f;
        snapPos.x = ladderSurfacePos.x;
        snapPos.z = ladderSurfacePos.z;

        CC.enabled = false;
        transform.position = snapPos;
        CC.enabled = true;

        requiredRotation = Quaternion.LookRotation(lastGrabLadderDirection);
        transform.rotation = requiredRotation;
    }

    private void DropLadder()
    {
        isClimbingLadder = false;
        isMountingLadder = false;
        fallingSpeed = 0f;
        animator.SetFloat("ClimbSpeed", 0f);
        animator.applyRootMotion = false;
    }

    private IEnumerator ClimbToTopSnapRoutine()
    {
        isMountingLadder = true;
        isClimbingLadder = false;

        // Tìm vị trí sàn phía trên đỉnh thang từ mốc 2/3 CC
        Vector3 searchStart = transform.position + transform.forward * 0.5f + Vector3.up * 1.2f;
        Vector3 targetPos = transform.position + transform.forward * 0.5f + Vector3.up * 0.5f;

        if (Physics.Raycast(searchStart, Vector3.down, out RaycastHit hitGround, 2.0f, surfaceLayer))
        {
            targetPos = hitGround.point;
        }

        Vector3 startPos = transform.position;
        float duration = 0.25f;
        float elapsed = 0f;

        CC.enabled = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
        CC.enabled = true;

        DropLadder();
    }

    void playerLedgeMovement()
    {
        float angle = Vector3.Angle(LedgeInfo.surfaceHit.normal, requiredMoveDir);
        if (angle < 90)
        {
        }
    }

    private IEnumerator ClimbDownFromTopRoutine(Vector3 targetPos, Vector3 grabDir)
    {
        isMountingLadder = true;
        isClimbingLadder = true;

        if (grabDir.sqrMagnitude < 0.001f)
        {
            grabDir = -transform.forward;
            grabDir.y = 0;
            grabDir.Normalize();
        }

        lastGrabLadderDirection = grabDir;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(grabDir);

        float duration = 0.35f;
        float elapsed = 0f;

        CC.enabled = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        CC.enabled = true;
        isMountingLadder = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.TransformPoint(surfaceCheckOffset), surfaceCheckRadius);
    }

    public IEnumerator PerformAction(string AnimationName, CompareTargetParameter ctp = null, Quaternion RequiredRotation = new Quaternion(), bool LookAtObstacle = false, float ParkourActionDelay = 0f)
    {
        playerInAction = true;
        animator.CrossFadeInFixedTime(AnimationName, 0.2f);
        yield return null;
        var animationState = animator.GetCurrentAnimatorStateInfo(0);
        float rotateStartTime = (ctp != null) ? ctp.startTime : 0f;
        float timerCounter = 0f;

        while (timerCounter <= animationState.length)
        {
            timerCounter += Time.deltaTime;
            float normalizedTimeCounter = timerCounter / animationState.length;
            if (LookAtObstacle && normalizedTimeCounter > rotateStartTime)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    RequiredRotation,
                    rotspeed * Time.deltaTime
                );
            }

            if (ctp != null && !animator.IsInTransition(0))
            {
                CompareTarget(ctp);
            }

            if (animator.IsInTransition(0) && timerCounter > 0.5f)
            {
                break;
            }

            yield return null;
        }

        yield return new WaitForSeconds(ParkourActionDelay);
        playerInAction = false;
    }

    void CompareTarget(CompareTargetParameter compareTargetParameters)
    {
        animator.MatchTarget(compareTargetParameters.position, transform.rotation, compareTargetParameters.bodyPart, new MatchTargetWeightMask(compareTargetParameters.positionWeight, 0), compareTargetParameters.startTime, compareTargetParameters.endTime);
    }

    public void SetControl(bool hasControl)
    {
        this.playerControl = hasControl;

        if (CC != null)
        {
            CC.enabled = hasControl;
        }

        animator.applyRootMotion = !hasControl;

        if (!hasControl)
        {
            animator.SetFloat("movementvalue", 0f);
            velocity = Vector3.zero;
            fallingSpeed = 0f;
            requiredRotation = transform.rotation;
        }
    }

    public void EnableCC(bool enabled)
    {
        CC.enabled = enabled;
    }

    public void ResetRequiredRotation()
    {
        requiredRotation = transform.rotation;
    }

    public bool HasPlayerControl
    {
        get => playerControl;
        set => playerControl = value;
    }
}
public class CompareTargetParameter
{
    public Vector3 position;
    public AvatarTarget bodyPart;
    public Vector3 positionWeight;
    public float startTime;
    public float endTime;
}