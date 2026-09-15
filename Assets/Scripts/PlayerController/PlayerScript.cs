using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class PlayerScript : MonoBehaviour
{
    [Header("player movement")]
    public float walkSpeed = 2f;
    public float runSpeed = 4f;
    float currentSpeed;
    private bool isRunning = false;

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

    // Root Motion được xử lý thủ công trong OnAnimatorMove.
    // Điều này tránh việc bật/tắt Animator.applyRootMotion ở runtime làm Animator bị re-initialize.
    private bool useClimbTopRootMotion;

    [Header("Ladder Settings")]
    [SerializeField] private float ladderGrabDistance = 0.6f;

    [Header("Climb Down From Top")]
    [Tooltip("Khoang cach tia tu chan Player ban THANG VE PHIA TRUOC de tim dau thang.")]
    [SerializeField] private float topLadderRayDistance = 1.2f;
    [Tooltip("Nang tia len khoi day CharacterController mot chut.")]
    [SerializeField] private float topLadderRayFootOffset = 0.12f;
    [Tooltip("Sai so do cao giua chan Player va dinh collider cua thang.")]
    [SerializeField] private float topLadderHeightTolerance = 0.65f;
    [SerializeField] private float ladderSurfaceOffset = 0.35f;
    [SerializeField] private float climbDownDropHeight = 1f;
    [SerializeField] private float climbDownTransitionDuration = 0.8f;
    [Tooltip("Thoi gian chi dung de ha Player xuong diem bam sau khi da SNAP ngay ra mat ngoai thang.")]
    [SerializeField] private float climbDownVerticalAlignDuration = 0.18f;

    [Header("Climb To Top - Surface Search")]
    [Tooltip("Khoang cach dua diem tim san vao phia tren dinh thang.")]
    [SerializeField] private float climbTopForwardDistance = 0.75f;
    [Tooltip("Do cao diem bat dau ray tim mat san tren dinh thang.")]
    [SerializeField] private float climbTopSearchHeight = 1.5f;
    [Tooltip("Khoang cach ray ban xuong de tim mat san tren dinh thang.")]
    [SerializeField] private float climbTopSearchDownDistance = 2.5f;
    [Tooltip("Nang chan Player len khoi mat san mot chut sau khi MatchTarget ket thuc.")]
    [SerializeField] private float climbTopGroundOffset = 0.03f;

    [Header("Climb To Top - Match Target")]
    [Tooltip("State animation leo qua dinh thang. Ten full path trong Animator.")]
    [SerializeField] private string climbTopAnimationStateName = "Base Layer.ClimbToTop";
    [Tooltip("Thoi gian blend tu animation leo thang sang ClimbToTop. Voi Animator.CrossFade day la normalized transition duration.")]
    [SerializeField] private float climbTopAnimationBlend = 0.03f;
    [Range(0f, 0.5f)]
    [Tooltip("Diem bat dau cua clip ClimbToTop. Tang nhe neu frame dau clip khong khop pose ClimbUp. Thu 0.03 - 0.10.")]
    [SerializeField] private float climbTopStartNormalizedTime = 0.05f;
    [Range(0f, 1f)]
    [Tooltip("Normalized time bat dau MatchTarget. Thu 0.30 truoc.")]
    [SerializeField] private float climbTopMatchStart = 0.30f;
    [Range(0f, 1f)]
    [Tooltip("Normalized time ket thuc MatchTarget. Thu 0.90 - 0.95.")]
    [SerializeField] private float climbTopMatchEnd = 0.98f;
    [Tooltip("Trong so MatchTarget theo X/Y/Z. (1,1,1) = match day du vi tri.")]
    [SerializeField] private Vector3 climbTopMatchPositionWeight = Vector3.one;
    [Range(0f, 1f)]
    [Tooltip("Trong so match rotation. De 0 neu chi muon MatchTarget vi tri.")]
    [SerializeField] private float climbTopMatchRotationWeight = 0f;
    [Tooltip("Thoi gian toi da cho Animator vao state ClimbToTop.")]
    [SerializeField] private float climbTopStateEnterTimeout = 0.6f;

    [Header("Climb To Top - Standing")]
    [Tooltip("State dung yen ngay sau ClimbToTop. Tao state nay trong Animator voi animation Standing/Idle ngan.")]
    [SerializeField] private string climbTopStandingStateName = "Base Layer.Standing";
    [Tooltip("Thoi gian blend tu ClimbToTop sang Standing.")]
    [SerializeField] private float climbTopStandingBlendDuration = 0.08f;
    [Tooltip("Thoi gian can vi tri mem ve target trong luc animation Standing bat dau. Tranh teleport o cuoi ClimbToTop.")]
    [SerializeField] private float climbTopStandingAlignDuration = 0.12f;
    [Tooltip("Thoi gian giu animation Standing truoc khi ve Basic Movement.")]
    [SerializeField] private float climbTopStandingHoldDuration = 0.18f;
    [Range(0.90f, 1.0f)]
    [Tooltip("Cho ClimbToTop chay den gan cuoi clip roi moi sang Standing. Thu 0.98.")]
    [SerializeField] private float climbTopFinishNormalizedTime = 0.98f;

    [Header("Climb To Top - Exit")]
    [Tooltip("State sau Standing.")]
    [SerializeField] private string climbTopExitStateName = "Base Layer.Basic Movement";
    [Tooltip("Thoi gian blend tu Standing sang Basic Movement.")]
    [SerializeField] private float climbTopExitBlendDuration = 0.08f;

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

    private void Awake()
    {
        // MatchTarget yêu cầu Apply Root Motion phải được bật.
        // Giữ nó luôn ON và tự quyết định lúc nào áp delta trong OnAnimatorMove
        // để Animator không bị re-initialize giữa chừng.
        if (animator != null)
            animator.applyRootMotion = true;
    }

    private void OnAnimatorMove()
    {
        if (animator == null)
            return;

        // Bình thường movement do CharacterController xử lý nên bỏ root motion.
        // Chỉ áp root motion khi ClimbToTop hoặc khi control đã bị khóa cho action khác.
        bool shouldApplyRootMotion = useClimbTopRootMotion || !playerControl;
        if (!shouldApplyRootMotion)
            return;

        Vector3 deltaPosition = animator.deltaPosition;
        Quaternion deltaRotation = animator.deltaRotation;

        // Trong ClimbToTop CC được tắt, nên delta của animation/MatchTarget
        // được áp trực tiếp vào GameObject Player.
        transform.position += deltaPosition;
        transform.rotation = transform.rotation * deltaRotation;
    }

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

            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                isRunning = !isRunning;
            }

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

            // ---------------------------------------------------------
            // RAYCAST RIÊNG CHO LEO TỪ TRÊN XUỐNG
            // - Bắt đầu NGAY Ở CHÂN Player.
            // - Bắn THẲNG VỀ PHÍA TRƯỚC theo transform.forward.
            // - KHÔNG bắn xéo và KHÔNG bắn xuống dưới.
            // - Ray được kiểm tra liên tục khi Player đang đứng để Scene View luôn nhìn thấy.
            // - Chỉ được coi là "đầu thang" khi chân Player gần ngang với đỉnh collider Ladder.
            // ---------------------------------------------------------
            bool topHitIsLadder = false;
            RaycastHit hitBelow = default;
            Ladder ladderBelow = null;

            Vector3 topRayOrigin;

            // Lấy đúng vị trí đáy CharacterController rồi nâng lên một chút.
            if (CC != null && CC.enabled)
            {
                Bounds ccBounds = CC.bounds;
                topRayOrigin = new Vector3(
                    ccBounds.center.x,
                    ccBounds.min.y + topLadderRayFootOffset,
                    ccBounds.center.z
                );
            }
            else
            {
                topRayOrigin = transform.position + Vector3.up * topLadderRayFootOffset;
            }

            // Tia nằm ngang 100%.
            Vector3 topRayDirection = transform.forward;
            topRayDirection.y = 0f;

            if (topRayDirection.sqrMagnitude < 0.001f)
                topRayDirection = Vector3.forward;

            topRayDirection.Normalize();

            // Chỉ dò khi Player đang đứng trên bề mặt và chưa ở trạng thái leo/chuyển trạng thái.
            if (onSurface && !isMountingLadder)
            {
                RaycastHit[] topHits = Physics.RaycastAll(
                    topRayOrigin,
                    topRayDirection,
                    topLadderRayDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore
                );

                float nearestLadderDistance = float.MaxValue;

                foreach (RaycastHit topHit in topHits)
                {
                    Ladder hitLadder = topHit.transform.GetComponentInParent<Ladder>();
                    if (hitLadder == null)
                        continue;

                    // Tính độ cao thật của đầu thang từ tất cả collider thuộc Ladder.
                    Collider[] ladderColliders = hitLadder.GetComponentsInChildren<Collider>();
                    float ladderTopY = float.NegativeInfinity;

                    foreach (Collider ladderCollider in ladderColliders)
                    {
                        if (ladderCollider != null && ladderCollider.enabled)
                            ladderTopY = Mathf.Max(ladderTopY, ladderCollider.bounds.max.y);
                    }

                    if (float.IsNegativeInfinity(ladderTopY))
                        continue;

                    float playerFootY = topRayOrigin.y - topLadderRayFootOffset;
                    float heightDifference = Mathf.Abs(playerFootY - ladderTopY);

                    // Ở dưới chân thang sẽ không vượt qua điều kiện này.
                    bool playerIsAtTop = heightDifference <= topLadderHeightTolerance;

                    if (!playerIsAtTop)
                        continue;

                    if (topHit.distance < nearestLadderDistance)
                    {
                        nearestLadderDistance = topHit.distance;
                        hitBelow = topHit;
                        ladderBelow = hitLadder;
                        topHitIsLadder = true;
                    }
                }

                // Vẽ MỖI FRAME nên tia không còn chớp/mất.
                // Xanh = đã bắt đúng đầu Ladder, đỏ = chưa bắt được.
                Debug.DrawRay(
                    topRayOrigin,
                    topRayDirection * topLadderRayDistance,
                    topHitIsLadder ? Color.green : Color.red,
                    0f,
                    false
                );
            }

            if (!topHitIsLadder &&
                foundForward &&
                hit.transform.GetComponentInParent<Ladder>() is Ladder ladderForward)
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

                // CHỈ BÁM THANG KHI:
                // - Đang đứng đúng mặt trước/sau của thang
                // - Đang hướng về thang
                // - Ở trong vùng giữa của thang
                // - Người chơi NHẤN G
                //
                // Không còn tự động bám chỉ vì đi sát vào thang.
                if (isFrontFace && isVerticalSurface && isFacingLadder && isWithinCenterWidth)
                {
                    if (Input.GetKeyDown(KeyCode.G))
                    {
                        // Ép điểm bám về đúng trục giữa của thang theo chiều ngang.
                        Vector3 centeredHitPoint =
                            hit.point - (Vector3.Dot(hitOffset, ladderRight) * ladderRight);

                        GrabLadder(-hitNormalXZ, centeredHitPoint);
                        return;
                    }
                }
            }
            // ĐỨNG TRÊN ĐỈNH THANG -> RAYCAST BẮT LADDER -> BẤM G ĐỂ LEO XUỐNG
            if (topHitIsLadder &&
                ladderBelow != null &&
                Input.GetKeyDown(KeyCode.G) &&
                !isMountingLadder)
            {
                // =========================================================
                // FIX: PLAYER Ở TRÊN MÁI ĐANG NẰM Ở MẶT SAU CỦA THANG.
                // Khi leo xuống phải đưa Player sang MẶT ĐỐI DIỆN với vị trí
                // hiện tại, tức là đúng mặt ngoài giống khi leo từ dưới lên.
                //
                // KHÔNG dùng hit.normal của rung thang nữa vì ray ở đầu thang
                // có thể chạm mặt sau / cạnh của rung và chọn nhầm phía.
                // =========================================================

                // 1) Trục ngang của thang. Trục này trong logic leo từ dưới
                // đã hoạt động đúng nên dùng nó để dựng mặt phẳng thang.
                Vector3 ladderRight = ladderBelow.transform.right;
                ladderRight.y = 0f;

                if (ladderRight.sqrMagnitude < 0.001f)
                {
                    ladderRight = transform.right;
                    ladderRight.y = 0f;
                }

                ladderRight.Normalize();

                // 2) Gom bounds của toàn bộ collider Ladder để lấy tâm thật.
                Collider[] ladderColliders = ladderBelow.GetComponentsInChildren<Collider>();
                Bounds ladderBounds = hitBelow.collider.bounds;
                bool hasBounds = false;

                foreach (Collider ladderCollider in ladderColliders)
                {
                    if (ladderCollider == null || !ladderCollider.enabled)
                        continue;

                    if (!hasBounds)
                    {
                        ladderBounds = ladderCollider.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        ladderBounds.Encapsulate(ladderCollider.bounds);
                    }
                }

                Vector3 ladderCenter = ladderBounds.center;

                // 3) Hai mặt trước/sau của thang nằm theo trục vuông góc
                // với ladderRight.
                Vector3 ladderNormalAxis = Vector3.Cross(Vector3.up, ladderRight);
                ladderNormalAxis.y = 0f;

                if (ladderNormalAxis.sqrMagnitude < 0.001f)
                {
                    ladderNormalAxis = topRayDirection;
                    ladderNormalAxis.y = 0f;
                }

                ladderNormalAxis.Normalize();

                // 4) Xác định Player hiện đang đứng ở phía nào của thang.
                Vector3 ladderToPlayer = transform.position - ladderCenter;
                ladderToPlayer.y = 0f;

                float playerSide = Vector3.Dot(ladderToPlayer, ladderNormalAxis);

                Vector3 playerSideNormal;

                if (Mathf.Abs(playerSide) > 0.001f)
                {
                    playerSideNormal = playerSide > 0f
                        ? ladderNormalAxis
                        : -ladderNormalAxis;
                }
                else
                {
                    // Fallback: phía Player nằm ngược với hướng ray đang bắn vào thang.
                    playerSideNormal = -topRayDirection;
                    playerSideNormal.y = 0f;
                    playerSideNormal.Normalize();
                }

                // QUAN TRỌNG:
                // Player đang ở trên mái / mặt trong của thang.
                // Mặt ngoài để leo xuống là PHÍA ĐỐI DIỆN với Player hiện tại.
                Vector3 outsideNormal = -playerSideNormal;
                outsideNormal.y = 0f;
                outsideNormal.Normalize();

                // 5) Khi ở ngoài, Player phải nhìn ngược lại vào thang.
                Vector3 grabDir = -outsideNormal;
                grabDir.y = 0f;
                grabDir.Normalize();

                // 6) Tính độ dày của Ladder theo hướng trước/sau.
                // Sau đó đặt TÂM Player ra ngoài mặt collider đúng khoảng cách.
                Vector3 e = ladderBounds.extents;
                float ladderHalfDepth =
                    Mathf.Abs(outsideNormal.x) * e.x +
                    Mathf.Abs(outsideNormal.z) * e.z;

                float ccRadius = (CC != null) ? CC.radius : 0.3f;
                float surfaceGap = Mathf.Max(ladderSurfaceOffset, ccRadius + 0.05f);

                // Căn Player vào giữa chiều ngang của thang và đưa sang mặt ngoài.
                Vector3 targetPos = ladderCenter +
                                    outsideNormal * (ladderHalfDepth + surfaceGap);

                targetPos.y = transform.position.y - climbDownDropHeight;

                // Debug để kiểm tra:
                // BLUE  = mặt Player đang đứng trước khi leo xuống (mặt trong/mái)
                // GREEN = mặt ngoài mà Player sẽ được đưa tới
                // YELLOW= hướng Player nhìn vào thang
                Debug.DrawRay(ladderCenter, playerSideNormal * 1.0f, Color.blue, 2.0f);
                Debug.DrawRay(ladderCenter, outsideNormal * 1.0f, Color.green, 2.0f);
                Debug.DrawRay(targetPos, grabDir * 1.0f, Color.yellow, 2.0f);

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

            // =========================================================
            // LEO TỚI ĐỈNH -> CHUYỂN THẲNG SANG CLIMBTOTOP
            // =========================================================
            // Kiểm tra TRƯỚC khi cập nhật ClimbSpeed để frame phát hiện đầu thang
            // không tiếp tục đẩy Blend Tree ClimbUp thêm một nhịp nữa.
            if (vertical > 0 && !twoThirdsHit)
            {
                velocity = Vector3.zero;
                fallingSpeed = 0f;

                StartCoroutine(ClimbToTopSnapRoutine());
                return;
            }

            // Chỉ cập nhật Blend Tree leo thang nếu chưa bắt đầu ClimbToTop.
            // Lên = 1, Idle = 0, Xuống = -1.
            animator.SetFloat("ClimbSpeed", vertical, 0.1f, Time.deltaTime);

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

        animator.applyRootMotion = true;
        useClimbTopRootMotion = false;

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
        useClimbTopRootMotion = false;
        animator.applyRootMotion = true;
    }

    private IEnumerator ClimbToTopSnapRoutine()
    {
        // =============================================================
        // CLIMB TO TOP = ANIMATION + MATCH TARGET + ONANIMATORMOVE
        // =============================================================
        // Animator.applyRootMotion được giữ ON suốt thời gian chạy game.
        // OnAnimatorMove chỉ áp animator.deltaPosition/deltaRotation khi
        // useClimbTopRootMotion = true. Vì vậy MatchTarget có thể warp
        // root motion thật sự NGAY TRONG animation thay vì cuối clip mới snap.

        isMountingLadder = true;
        fallingSpeed = 0f;
        velocity = Vector3.zero;

        Vector3 climbIntoPlatform = lastGrabLadderDirection;
        climbIntoPlatform.y = 0f;

        if (climbIntoPlatform.sqrMagnitude < 0.001f)
        {
            climbIntoPlatform = transform.forward;
            climbIntoPlatform.y = 0f;
        }

        climbIntoPlatform.Normalize();

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // -------------------------------------------------------------
        // 1) TÌM MẶT SÀN AN TOÀN TRÊN ĐỈNH
        // -------------------------------------------------------------
        float feetOffsetFromTransform = 0f;
        if (CC != null && CC.enabled)
        {
            feetOffsetFromTransform =
                CC.bounds.min.y - transform.position.y;
        }

        float ccRadiusForLanding = (CC != null) ? CC.radius : 0.30f;
        float minimumSafeLandingDepth =
            Mathf.Max(0.40f, ccRadiusForLanding + 0.12f);

        float safeNearSample =
            Mathf.Max(
                minimumSafeLandingDepth,
                climbTopForwardDistance - 0.25f
            );

        float safeMainSample =
            Mathf.Max(
                climbTopForwardDistance,
                safeNearSample + 0.10f
            );

        float[] forwardSamples =
        {
            safeNearSample,
            safeMainSample,
            safeMainSample + 0.20f,
            safeMainSample + 0.40f
        };

        bool foundTopSurface = false;
        RaycastHit bestGroundHit = default;

        foreach (float forwardDistance in forwardSamples)
        {
            Vector3 searchStart =
                transform.position +
                climbIntoPlatform * forwardDistance +
                Vector3.up * climbTopSearchHeight;

            RaycastHit[] hits = Physics.RaycastAll(
                searchStart,
                Vector3.down,
                climbTopSearchDownDistance,
                surfaceLayer,
                QueryTriggerInteraction.Ignore
            );

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Debug.DrawRay(
                searchStart,
                Vector3.down * climbTopSearchDownDistance,
                Color.cyan,
                2f
            );

            foreach (RaycastHit groundHit in hits)
            {
                if (groundHit.transform.GetComponentInParent<Ladder>() != null)
                    continue;

                if (groundHit.normal.y < 0.5f)
                    continue;

                bestGroundHit = groundHit;
                foundTopSurface = true;
                break;
            }

            if (foundTopSurface)
                break;
        }

        if (!foundTopSurface)
        {
            animator.SetFloat("ClimbSpeed", 0f);
            animator.SetBool("IsClimbing", true);
            animator.SetBool("OnSurface", false);

            useClimbTopRootMotion = false;
            isMountingLadder = false;
            isClimbingLadder = true;
            yield break;
        }

        // -------------------------------------------------------------
        // 2) TARGET ROOT TRÊN MẶT SÀN
        // -------------------------------------------------------------
        Vector3 targetRootPos = bestGroundHit.point;
        targetRootPos.y =
            bestGroundHit.point.y +
            climbTopGroundOffset -
            feetOffsetFromTransform;

        Quaternion targetRootRot = Quaternion.LookRotation(climbIntoPlatform);

        Debug.DrawLine(
            bestGroundHit.point,
            bestGroundHit.point + Vector3.up * 0.8f,
            Color.magenta,
            2f
        );

        // -------------------------------------------------------------
        // 3) CHẠY CLIMBTOTOP
        // -------------------------------------------------------------
        if (CC != null)
            CC.enabled = false;

        isClimbingLadder = true;
        animator.SetBool("IsClimbing", true);
        animator.SetBool("OnSurface", false);

        // QUAN TRONG: KHONG set ClimbSpeed = 0 o day.
        // Giu nguyen pose ClimbUp hien tai trong luc CrossFade de tranh Blend Tree
        // nhay qua ClimbIdle mot frame roi moi vao ClimbToTop.

        // KHÔNG bật/tắt applyRootMotion ở đây nữa.
        // Giữ luôn true để tránh Animator bị re-initialize.
        animator.applyRootMotion = true;
        useClimbTopRootMotion = true;

        if (string.IsNullOrEmpty(climbTopAnimationStateName))
        {
            useClimbTopRootMotion = false;

            if (CC != null)
                CC.enabled = true;

            transform.position = startPos;
            transform.rotation = startRot;

            isMountingLadder = false;
            isClimbingLadder = true;
            yield break;
        }

        // Chuyển trực tiếp từ pose ClimbUp hiện tại sang ClimbToTop.
        // Không ép Blend Tree về Idle trước CrossFade vì điều đó có thể tạo thêm 1 pose trung gian.
        // normalizedTimeOffset cho phép bỏ qua vài % đầu clip nếu frame đầu ClimbToTop
        // không khớp với pose cuối của ClimbUp.
        animator.CrossFade(
            climbTopAnimationStateName,
            Mathf.Max(0.001f, climbTopAnimationBlend),
            0,
            Mathf.Clamp01(climbTopStartNormalizedTime)
        );

        int climbTopFullPathHash =
            Animator.StringToHash(climbTopAnimationStateName);

        string climbTopShortName = climbTopAnimationStateName;
        int lastDot = climbTopShortName.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < climbTopShortName.Length - 1)
            climbTopShortName = climbTopShortName.Substring(lastDot + 1);

        int climbTopShortHash = Animator.StringToHash(climbTopShortName);

        // -------------------------------------------------------------
        // 4) CHỜ ANIMATOR VÀO STATE
        // -------------------------------------------------------------
        float enterTimer = 0f;
        float enterTimeout = Mathf.Max(0.1f, climbTopStateEnterTimeout);
        bool enteredClimbTopState = false;

        while (enterTimer < enterTimeout)
        {
            enterTimer += Time.deltaTime;

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            bool currentIsClimbTop =
                currentState.fullPathHash == climbTopFullPathHash ||
                currentState.shortNameHash == climbTopShortHash;

            if (currentIsClimbTop && !animator.IsInTransition(0))
            {
                enteredClimbTopState = true;
                break;
            }

            yield return null;
        }

        if (!enteredClimbTopState)
        {
            useClimbTopRootMotion = false;

            if (CC != null)
                CC.enabled = true;

            transform.position = startPos;
            transform.rotation = startRot;
            Physics.SyncTransforms();

            animator.SetBool("IsClimbing", true);
            animator.SetBool("OnSurface", false);
            animator.SetFloat("ClimbSpeed", 0f);

            isMountingLadder = false;
            isClimbingLadder = true;
            yield break;
        }

        // Da vao ClimbToTop hoan toan, luc nay moi reset Blend Tree leo thang.
        // Source ClimbUp khong con tham gia blend nua nen khong tao pose trung gian.
        animator.SetFloat("ClimbSpeed", 0f);

        // -------------------------------------------------------------
        // 5) MATCH TARGET
        // -------------------------------------------------------------
        float finishTime =
            Mathf.Clamp(climbTopFinishNormalizedTime, 0.90f, 0.995f);

        float matchStart =
            Mathf.Clamp(climbTopMatchStart, 0.05f, finishTime - 0.05f);

        float matchEnd =
            Mathf.Clamp(climbTopMatchEnd, matchStart + 0.02f, finishTime);

        // MatchEnd phải nằm sát FinishTime để không có đoạn Root Motion
        // tiếp tục đẩy Player sau khi đã đạt target.
        matchEnd = Mathf.Max(matchEnd, finishTime - 0.01f);

        MatchTargetWeightMask matchMask =
            new MatchTargetWeightMask(
                climbTopMatchPositionWeight,
                climbTopMatchRotationWeight
            );

        bool matchRequested = false;
        bool matchEverActive = false;
        bool manualFallback = false;
        Vector3 fallbackStartPos = Vector3.zero;
        Quaternion fallbackStartRot = Quaternion.identity;
        float fallbackStartTime = 0f;

        while (true)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            bool stillInClimbTop =
                stateInfo.fullPathHash == climbTopFullPathHash ||
                stateInfo.shortNameHash == climbTopShortHash;

            if (!stillInClimbTop)
                break;

            float normalizedTime = stateInfo.normalizedTime;

            // Queue đúng một lần, trước MatchStart.
            if (!matchRequested &&
                !animator.IsInTransition(0) &&
                normalizedTime < matchEnd - 0.01f)
            {
                float requestStart = Mathf.Max(matchStart, normalizedTime + 0.01f);
                requestStart = Mathf.Min(requestStart, matchEnd - 0.01f);

                animator.MatchTarget(
                    targetRootPos,
                    targetRootRot,
                    AvatarTarget.Root,
                    matchMask,
                    requestStart,
                    matchEnd
                );

                matchRequested = true;
            }

            if (animator.isMatchingTarget)
                matchEverActive = true;

            // Nếu qua MatchStart một chút mà Unity vẫn chưa kích hoạt MatchTarget,
            // chuyển sang fallback NGAY TRONG clip. Tắt root delta để tránh cộng đôi.
            if (!manualFallback &&
                matchRequested &&
                !matchEverActive &&
                normalizedTime >= matchStart + 0.08f)
            {
                manualFallback = true;
                useClimbTopRootMotion = false;
                fallbackStartPos = transform.position;
                fallbackStartRot = transform.rotation;
                fallbackStartTime = normalizedTime;

                Debug.LogWarning(
                    "ClimbToTop: Animator.MatchTarget did not become active. " +
                    "Using smooth in-animation fallback."
                );
            }

            if (manualFallback)
            {
                float t = Mathf.InverseLerp(
                    fallbackStartTime,
                    finishTime,
                    normalizedTime
                );

                t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

                transform.position = Vector3.Lerp(
                    fallbackStartPos,
                    targetRootPos,
                    t
                );

                transform.rotation = Quaternion.Slerp(
                    fallbackStartRot,
                    targetRootRot,
                    t
                );
            }

            if (normalizedTime >= finishTime)
                break;

            yield return null;
        }

        // Dừng nhận root delta trước khi chuyển Standing.
        useClimbTopRootMotion = false;

        // -------------------------------------------------------------
        // 6) CLIMBTOTOP -> STANDING
        // -------------------------------------------------------------
        Vector3 standingStartPos = transform.position;
        Quaternion standingStartRot = transform.rotation;

        isClimbingLadder = false;
        fallingSpeed = 0f;
        velocity = Vector3.zero;

        animator.SetFloat("ClimbSpeed", 0f);
        animator.SetBool("IsClimbing", false);
        animator.SetBool("OnSurface", true);

        if (!string.IsNullOrEmpty(climbTopStandingStateName))
        {
            animator.CrossFadeInFixedTime(
                climbTopStandingStateName,
                Mathf.Max(0.01f, climbTopStandingBlendDuration),
                0,
                0f
            );
        }

        // Chỉ căn sai số còn lại thật mượt. Không có teleport cuối animation.
        float remainingDistance =
            Vector3.Distance(standingStartPos, targetRootPos);

        float alignDuration = Mathf.Max(
            climbTopStandingAlignDuration,
            Mathf.Clamp(remainingDistance / 1.5f, 0.10f, 0.55f)
        );

        float alignElapsed = 0f;
        while (alignElapsed < alignDuration)
        {
            alignElapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(alignElapsed / alignDuration)
            );

            transform.position = Vector3.Lerp(
                standingStartPos,
                targetRootPos,
                t
            );

            transform.rotation = Quaternion.Slerp(
                standingStartRot,
                targetRootRot,
                t
            );

            yield return null;
        }

        transform.position = targetRootPos;
        transform.rotation = targetRootRot;
        requiredRotation = targetRootRot;
        Physics.SyncTransforms();

        if (CC != null)
        {
            CC.enabled = true;
            CC.Move(Vector3.down * 0.03f);
        }

        Physics.SyncTransforms();
        SurfaceCheck();
        animator.SetBool("OnSurface", true);

        float standingHold = Mathf.Max(0f, climbTopStandingHoldDuration);
        if (standingHold > 0f)
            yield return new WaitForSeconds(standingHold);
        else
            yield return null;

        // -------------------------------------------------------------
        // 7) STANDING -> BASIC MOVEMENT
        // -------------------------------------------------------------
        if (!string.IsNullOrEmpty(climbTopExitStateName))
        {
            animator.CrossFadeInFixedTime(
                climbTopExitStateName,
                Mathf.Max(0.01f, climbTopExitBlendDuration),
                0,
                0f
            );
        }

        requiredRotation = transform.rotation;
        velocity = Vector3.zero;
        moveDir = Vector3.zero;
        requiredMoveDir = Vector3.zero;

        isMountingLadder = false;
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
        useClimbTopRootMotion = false;

        if (grabDir.sqrMagnitude < 0.001f)
        {
            grabDir = -transform.forward;
            grabDir.y = 0f;
        }

        grabDir.y = 0f;
        grabDir.Normalize();
        lastGrabLadderDirection = grabDir;

        Quaternion targetRot = Quaternion.LookRotation(grabDir);

        // Update() dang return khi isMountingLadder = true,
        // nen cap nhat Animator truc tiep tai day.
        animator.SetBool("IsClimbing", true);
        animator.SetFloat("ClimbSpeed", 0f);

        if (CC != null)
            CC.enabled = false;

        // =============================================================
        // SNAP NGAY RA MAT NGOAI CUA THANG
        // =============================================================
        // Khi bam G, X/Z va Rotation duoc dat NGAY LAP TUC.
        // Khong con Lerp/Slerp tu vi tri tren mai sang mat ngoai,
        // nen se khong thay Player xoay tu tu quanh/cat qua thang nua.
        Vector3 outsideSnapPos = transform.position;
        outsideSnapPos.x = targetPos.x;
        outsideSnapPos.z = targetPos.z;

        transform.position = outsideSnapPos;
        transform.rotation = targetRot;
        requiredRotation = targetRot;

        Physics.SyncTransforms();

        // Chi ha theo truc Y mot doan ngan de vao dung vi tri bam thang.
        // X/Z va rotation da co dinh o MAT NGOAI tu frame dau tien.
        Vector3 verticalStartPos = transform.position;
        Vector3 verticalTargetPos = new Vector3(
            verticalStartPos.x,
            targetPos.y,
            verticalStartPos.z
        );

        float duration = Mathf.Max(0.01f, climbDownVerticalAlignDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            transform.position = Vector3.Lerp(
                verticalStartPos,
                verticalTargetPos,
                t
            );

            // Khoa rotation de Player luon quay mat vao thang.
            transform.rotation = targetRot;
            yield return null;
        }

        transform.position = verticalTargetPos;
        transform.rotation = targetRot;

        Physics.SyncTransforms();

        if (CC != null)
            CC.enabled = true;

        isMountingLadder = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.TransformPoint(surfaceCheckOffset), surfaceCheckRadius);

        // Preview tia bắt đầu thang: từ chân Player bắn THẲNG về phía trước.
        Vector3 topRayOrigin;
        if (Application.isPlaying && CC != null && CC.enabled)
        {
            Bounds ccBounds = CC.bounds;
            topRayOrigin = new Vector3(
                ccBounds.center.x,
                ccBounds.min.y + topLadderRayFootOffset,
                ccBounds.center.z
            );
        }
        else
        {
            topRayOrigin = transform.position + Vector3.up * topLadderRayFootOffset;
        }

        Vector3 topRayDirection = transform.forward;
        topRayDirection.y = 0f;
        if (topRayDirection.sqrMagnitude < 0.001f)
            topRayDirection = Vector3.forward;
        topRayDirection.Normalize();

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(topRayOrigin, topRayOrigin + topRayDirection * topLadderRayDistance);
        Gizmos.DrawSphere(topRayOrigin + topRayDirection * topLadderRayDistance, 0.04f);

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

        animator.applyRootMotion = true;

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