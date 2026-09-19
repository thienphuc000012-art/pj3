using System;
using System.Collections;
using UnityEngine;

public enum PlayerState { NormalState, ClimbingState }

public class PlayerClimb : MonoBehaviour
{
    PlayerScript playerScript;
    RoofLedgeDetection roofLedgeDetection;
    LedgeToRoofClimb ledgeToRoofClimb;
    public Animator animator;

    [Header("Climbing")]
    public PlayerState playerState;
    public bool isClimbing;
    public bool isHopping; // Khóa trạng thái khi đang thực hiện Hop
    public bool canGrabLedge;

    // Khóa lúc vừa bám gờ từ dưới đất.
    // Ngăn cùng một lần nhấn C bị dùng tiếp cho Hop Up / Roof Climb trong cùng frame.
    bool isEnteringLedge;
    public bool IsEnteringLedge => isEnteringLedge;

    // Khóa tuyệt đối input của toàn bộ hệ thống climb khi đang LedgeToClimb.
    bool roofClimbInputLocked;
    public bool IsRoofClimbInputLocked => roofClimbInputLocked;

    public int rayAmount = 10;
    public float rayLength = 0.5f;
    public float rayOffset = 0.15f;
    public float rayHeight = 1.7f;

    public RaycastHit rayLedgeForwardHit;
    public RaycastHit rayLedgeDownHit;

    // Stable ledge anchor: KHÔNG phụ thuộc transform/root motion hiện tại.
    // Đây là nguồn chuẩn cho DropToLedgeHang, Shimmy và LedgeToClimb.
    public bool HasStableLedge { get; private set; }
    public Vector3 StableLedgeTopPoint { get; private set; }
    public Vector3 StableLedgeWallPoint { get; private set; }
    public Vector3 StableLedgeWallNormal { get; private set; }

    public Vector3 StableLedgeForward
    {
        get
        {
            if (!HasStableLedge || StableLedgeWallNormal.sqrMagnitude < 0.0001f)
                return transform.forward;

            return -StableLedgeWallNormal;
        }
    }

    public LayerMask ledgeLayer;

    [Space(5)]
    public float rayYHandCorrection;
    public float rayZHandCorrection;
    [Space(5)]
    public float yDropToHangPos = -0.1f;
    public float zDropToHangPos = -0.05f;

    [Header("Hop Up Offset")]
    public float upHopUpPos = -0.1f;
    public float forwardHopUpPos = -0.05f;

    [Header("Hop Down Offset")]
    public float upHopDownPos = -0.1f;
    public float forwardHopDownPos = -0.05f;

    [Space(5)]
    float verticalInp;
    public float rayHopOffset = 0.1f;
    public float rayHopLength = 1f;
    public int hopRayAmount = 7;
    public float rayVerticalGap = 0.3f; // Khoảng cách bắt đầu kiểm tra gờ trên/dưới

    RaycastHit hopLedgeForwardHit;
    RaycastHit hopLedgeDownHit;

    private void Start()
    {
        playerState = PlayerState.NormalState;
        playerScript = GetComponent<PlayerScript>();
        animator = GetComponent<Animator>();
        roofLedgeDetection = GetComponent<RoofLedgeDetection>();
        ledgeToRoofClimb = GetComponent<LedgeToRoofClimb>();
    }

    private void Update()
    {
        if (CampaignSession.InputBlocked) return;
        // Khi đang LedgeToClimb: KHÔNG đọc bất kỳ input climb nào.
        // Đồng thời giữ CharacterController và player control bị khóa trong toàn bộ animation.
        if (roofClimbInputLocked)
        {
            verticalInp = 0f;
            canGrabLedge = false;
            isClimbing = true;
            playerState = PlayerState.ClimbingState;

            animator.SetFloat("movementvalue", 0f);
            animator.applyRootMotion = true;

            playerScript.playerHanging = true;
            playerScript.SetControl(false);
            return;
        }

        if (playerScript.playerInAction)
            return;

        // QUAN TRỌNG: đọc input TRƯỚC Inputs() để tránh dùng verticalInp của frame trước.
        verticalInp = Input.GetAxisRaw("Vertical");

        CheckingMainRay();
        Inputs();
        StateConditionsCheck();
        MatchTargetToLedge();

        // Hop Down vẫn phải được kiểm tra ngay cả khi phía trên có thể LedgeToRoofClimb.
        // Chỉ Hop Up mới nhường ưu tiên cho Roof Climb (được chặn bên trong HopUpRayCheck).
        // Khóa trong lúc vừa GrabLedge hoặc đang DropToLedgeHang để không dùng lại cùng input C.
        if (isClimbing && !isHopping && !isEnteringLedge &&
            !roofLedgeDetection.isDropingFromRoof &&
            !ledgeToRoofClimb.IsClimbingToRoof)
        {
            HopUpDown();
        }
    }

    private void Inputs()
    {
        // C = Bám gờ
        if (Input.GetKeyDown(KeyCode.C) && !roofLedgeDetection.isRoofLedgeDetected)
        {
            if (!isClimbing)
            {
                if (canGrabLedge && rayLedgeDownHit.point != Vector3.zero)
                {
                    // Chụp anchor trước khi rotation/root motion làm transform thay đổi.
                    SetStableLedge(rayLedgeForwardHit, rayLedgeDownHit);

                    Quaternion lookRot = Quaternion.LookRotation(StableLedgeForward);
                    transform.rotation = lookRot;

                    StartCoroutine(GrabLedge());
                }
            }
        }

        // F = Thả khỏi gờ
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (isClimbing)
            {
                if (verticalInp == 0 &&
                    !ledgeToRoofClimb.foundLedgeToRoofClimb &&
                    !isHopping &&
                    !isEnteringLedge &&
                    !roofLedgeDetection.isDropingFromRoof)
                {
                    StartCoroutine(DropLedge());
                }
            }
        }
    }

    private void CheckingMainRay()
    {
        if (!isClimbing && playerScript.onSurface)
        {
            for (int i = 0; i < rayAmount; i++)
            {
                Vector3 rayPosition = transform.position + Vector3.up * rayHeight + Vector3.up * rayOffset * i;

                if (Physics.Raycast(rayPosition, transform.forward, out rayLedgeForwardHit, rayLength, ledgeLayer, QueryTriggerInteraction.Ignore))
                {
                    canGrabLedge = true;
                    Physics.Raycast(rayLedgeForwardHit.point + Vector3.up * 0.5f, Vector3.down, out rayLedgeDownHit, 0.7f, ledgeLayer);
                    return;
                }
                else
                {
                    canGrabLedge = false;
                }
            }
        }
    }

    private void StateConditionsCheck()
    {
        if (playerState == PlayerState.NormalState)
        {
            animator.applyRootMotion = false;
            playerScript.EnableCC(true);
            playerScript.HasPlayerControl = true;
            playerScript.playerHanging = false;
        }
        else if (playerState == PlayerState.ClimbingState)
        {
            animator.applyRootMotion = true;
            playerScript.EnableCC(false);
            playerScript.HasPlayerControl = false;
            playerScript.playerHanging = true;
        }
    }

    private void MatchTargetToLedge()
    {
        // 1. Lần đầu bám gờ
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("idle to hang") && !animator.IsInTransition(0))
        {
            Vector3 ledgeForward = HasStableLedge ? StableLedgeForward : transform.forward;
            Vector3 targetPoint = HasStableLedge ? StableLedgeTopPoint : rayLedgeDownHit.point;
            Quaternion targetRotation = Quaternion.LookRotation(ledgeForward);

            Vector3 handPos = ledgeForward * rayZHandCorrection + Vector3.up * rayYHandCorrection;
            animator.MatchTarget(targetPoint + handPos, targetRotation, AvatarTarget.RightHand, new MatchTargetWeightMask(Vector3.one, 0), 0.36f, 0.57f);
        }

        if (animator.GetCurrentAnimatorStateInfo(0).IsName("droptofreehang") && !animator.IsInTransition(0))
        {
            // KHÔNG lấy transform.forward/rayLedgeFwdHit đang biến động theo animation.
            // Dùng snapshot của gờ đã chụp trước khi bắt đầu drop.
            Vector3 ledgeForward = HasStableLedge ? StableLedgeForward : transform.forward;
            Vector3 targetPoint = HasStableLedge ? StableLedgeWallPoint : roofLedgeDetection.rayLedgeFwdHit.point;
            Quaternion targetRotation = Quaternion.LookRotation(ledgeForward);

            Vector3 handDropPos = ledgeForward * zDropToHangPos + Vector3.up * yDropToHangPos;
            animator.MatchTarget(targetPoint + handDropPos, targetRotation, AvatarTarget.LeftHand, new MatchTargetWeightMask(Vector3.one, 0), 0.65f, 0.71f);
        }

        // 2. Hop Up Target Match
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("braced hang hop up") && !animator.IsInTransition(0))
        {
            Vector3 handHopUpPos = transform.forward * forwardHopUpPos + transform.up * upHopUpPos;
            animator.MatchTarget(hopLedgeDownHit.point + handHopUpPos, transform.rotation, AvatarTarget.LeftHand, new MatchTargetWeightMask(new Vector3(0, 1, 1), 0), 0.39f, 0.59f);
        }

        // 3. Hop Down Target Match
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("brace hang drop") && !animator.IsInTransition(0))
        {
            Vector3 handHopDownPos = transform.forward * forwardHopDownPos + transform.up * upHopDownPos;
            animator.MatchTarget(hopLedgeDownHit.point + handHopDownPos, transform.rotation, AvatarTarget.LeftHand, new MatchTargetWeightMask(new Vector3(0, 1, 1), 0), 0.31f, 0.56f);
        }
    }

    private void HopUpDown()
    {
        if (verticalInp < -0.1f)
        {
            HopDownRayCheck();
        }
        else if (verticalInp > 0.1f)
        {
            HopUpRayCheck();
        }
    }

    private void HopUpRayCheck()
    {
        // FIX: Lấy gốc Y theo chuẩn điểm bám gờ hiện tại (rayLedgeDownHit.point.y) để không bị biến động bởi Root Motion
        Vector3 baseHangPosition = new Vector3(transform.position.x, rayLedgeDownHit.point.y, transform.position.z);

        for (int i = 0; i < hopRayAmount; i++)
        {
            Vector3 rayPosition = baseHangPosition + Vector3.up * rayVerticalGap + Vector3.up * rayHopOffset * i;
            Debug.DrawRay(rayPosition, transform.forward * rayHopLength, Color.green);

            // FIX: Sử dụng hopLedgeForwardHit riêng biệt, không đè lên rayLedgeForwardHit
            if (Physics.Raycast(rayPosition, transform.forward, out hopLedgeForwardHit, rayHopLength, ledgeLayer, QueryTriggerInteraction.Ignore))
            {
                Debug.DrawRay(hopLedgeForwardHit.point + Vector3.up * 0.5f, Vector3.down * 0.7f, Color.green);

                if (Physics.Raycast(hopLedgeForwardHit.point + Vector3.up * 0.5f, Vector3.down, out hopLedgeDownHit, 0.7f, ledgeLayer))
                {
                    if (Input.GetKeyDown(KeyCode.C) && !isHopping &&
                        !ledgeToRoofClimb.foundLedgeToRoofClimb &&
                        !ledgeToRoofClimb.IsClimbingToRoof)
                    {
                        StartCoroutine(HopUp());
                    }
                }
                break;
            }
        }
    }

    private void HopDownRayCheck()
    {
        // FIX: Lấy gốc Y theo chuẩn điểm bám gờ hiện tại (rayLedgeDownHit.point.y)
        Vector3 baseHangPosition = new Vector3(transform.position.x, rayLedgeDownHit.point.y, transform.position.z);

        for (int i = 0; i < hopRayAmount; i++)
        {
            Vector3 rayPosition = baseHangPosition - Vector3.up * rayVerticalGap - Vector3.up * rayHopOffset * i;
            Debug.DrawRay(rayPosition, transform.forward * rayHopLength, Color.green);

            if (Physics.Raycast(rayPosition, transform.forward, out hopLedgeForwardHit, rayHopLength, ledgeLayer, QueryTriggerInteraction.Ignore))
            {
                Debug.DrawRay(hopLedgeForwardHit.point + Vector3.up * 0.5f, Vector3.down * 0.7f, Color.green);

                if (Physics.Raycast(hopLedgeForwardHit.point + Vector3.up * 0.5f, Vector3.down, out hopLedgeDownHit, 0.7f, ledgeLayer))
                {
                    if (Input.GetKeyDown(KeyCode.C) && !isHopping &&
                        !ledgeToRoofClimb.IsClimbingToRoof)
                    {
                        StartCoroutine(HopDown());
                    }
                }
                break;
            }
        }
    }

    IEnumerator GrabLedge()
    {
        // Khóa NGAY trước yield đầu tiên. Vì StartCoroutine chạy tới yield đầu tiên ngay lập tức,
        // các script Update chạy sau trong cùng frame sẽ thấy IsEnteringLedge = true.
        if (roofClimbInputLocked || isEnteringLedge || isHopping || ledgeToRoofClimb.IsClimbingToRoof)
            yield break;

        isEnteringLedge = true;
        playerState = PlayerState.ClimbingState;
        isClimbing = true;
        animator.CrossFade("idle to hang", 0.2f);

        // Không mở khóa theo timer cứng. Đợi Animator thật sự hoàn tất phần vào trạng thái treo.
        yield return null;
        while (animator.IsInTransition(0))
            yield return null;

        while (animator.GetCurrentAnimatorStateInfo(0).IsName("idle to hang") &&
               animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.90f)
        {
            yield return null;
        }

        isEnteringLedge = false;
    }

    public IEnumerator DropLedge()
    {
        if (roofClimbInputLocked || ledgeToRoofClimb.IsClimbingToRoof)
            yield break;

        animator.CrossFade("bracehangdrop", 0.2f);
        yield return new WaitForSeconds(0.5f);
        playerState = PlayerState.NormalState;
        isClimbing = false;
        ClearStableLedge();
    }

    IEnumerator HopUp()
    {
        // Khóa ngay lập tức để không có action thứ hai chen vào cùng frame.
        if (roofClimbInputLocked || isHopping || isEnteringLedge || roofLedgeDetection.isDropingFromRoof ||
            ledgeToRoofClimb.IsClimbingToRoof)
            yield break;

        isHopping = true;

        // Chụp lại hit target tại thời điểm bắt đầu Hop.
        // Tránh raycast frame sau ghi đè target trong lúc animation đang chạy.
        RaycastHit targetDownHit = hopLedgeDownHit;
        RaycastHit targetForwardHit = hopLedgeForwardHit;

        animator.CrossFade("braced hang hop up", 0.2f);

        // Đợi animation đi vào state Hop Up.
        yield return null;
        while (animator.IsInTransition(0))
            yield return null;

        // Đợi gần hết animation thay vì phụ thuộc hoàn toàn vào WaitForSeconds cố định.
        while (animator.GetCurrentAnimatorStateInfo(0).IsName("braced hang hop up") &&
               animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f)
        {
            yield return null;
        }

        SetStableLedge(targetForwardHit, targetDownHit);

        isHopping = false;
    }

    IEnumerator HopDown()
    {
        if (roofClimbInputLocked || isHopping || isEnteringLedge || roofLedgeDetection.isDropingFromRoof ||
            ledgeToRoofClimb.IsClimbingToRoof)
            yield break;

        isHopping = true;

        RaycastHit targetDownHit = hopLedgeDownHit;
        RaycastHit targetForwardHit = hopLedgeForwardHit;

        animator.CrossFade("brace hang drop", 0.2f);

        yield return null;
        while (animator.IsInTransition(0))
            yield return null;

        while (animator.GetCurrentAnimatorStateInfo(0).IsName("brace hang drop") &&
               animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f)
        {
            yield return null;
        }

        SetStableLedge(targetForwardHit, targetDownHit);

        isHopping = false;
    }

    // Ghi lại gờ hiện tại bằng world-space data cố định.
    // Không lấy lại từ transform trong khi animation đang chạy.
    public void SetStableLedge(RaycastHit forwardHit, RaycastHit downHit)
    {
        if (forwardHit.collider == null)
            return;

        rayLedgeForwardHit = forwardHit;
        if (downHit.collider != null)
            rayLedgeDownHit = downHit;

        Vector3 wallNormal = forwardHit.normal;
        wallNormal.y = 0f;

        if (wallNormal.sqrMagnitude < 0.0001f)
            wallNormal = -transform.forward;

        wallNormal.Normalize();

        StableLedgeWallPoint = forwardHit.point;
        StableLedgeWallNormal = wallNormal;

        if (downHit.collider != null)
        {
            StableLedgeTopPoint = downHit.point;
        }
        else if (HasStableLedge)
        {
            // Giữ Y của top cũ nhưng cập nhật XZ theo wall hit mới.
            StableLedgeTopPoint = new Vector3(
                forwardHit.point.x,
                StableLedgeTopPoint.y,
                forwardHit.point.z
            );
        }
        else
        {
            StableLedgeTopPoint = forwardHit.point;
        }

        HasStableLedge = true;
    }

    // Shimmy gọi hàm này sau khi ray chạm tường.
    // Tìm lại top của CÙNG gờ và cập nhật anchor theo vị trí ngang mới.
    public void UpdateStableLedgeFromWallHit(RaycastHit wallHit)
    {
        if (wallHit.collider == null)
            return;

        Vector3 wallNormal = wallHit.normal;
        wallNormal.y = 0f;

        if (wallNormal.sqrMagnitude < 0.0001f)
            return;

        wallNormal.Normalize();

        Vector3 topProbe =
            wallHit.point
            - wallNormal * 0.08f
            + Vector3.up * 0.8f;

        if (Physics.Raycast(
            topProbe,
            Vector3.down,
            out RaycastHit topHit,
            1.5f,
            ledgeLayer,
            QueryTriggerInteraction.Ignore))
        {
            SetStableLedge(wallHit, topHit);
        }
        else
        {
            rayLedgeForwardHit = wallHit;
            StableLedgeWallPoint = wallHit.point;
            StableLedgeWallNormal = wallNormal;

            if (HasStableLedge)
            {
                StableLedgeTopPoint = new Vector3(
                    wallHit.point.x,
                    StableLedgeTopPoint.y,
                    wallHit.point.z
                );
            }
            else
            {
                StableLedgeTopPoint = wallHit.point;
                HasStableLedge = true;
            }
        }
    }

    public void ClearStableLedge()
    {
        HasStableLedge = false;
        StableLedgeTopPoint = Vector3.zero;
        StableLedgeWallPoint = Vector3.zero;
        StableLedgeWallNormal = Vector3.zero;
    }

    // RoofLedgeDetection gọi NGAY khi DropToLedgeHang bắt đầu.
    // Nhờ vậy CC/root motion bị khóa ngay trong chính frame nhận input.
    public void BeginDropToLedgeHang(RaycastHit forwardHit, RaycastHit downHit)
    {
        SetStableLedge(forwardHit, downHit);

        isEnteringLedge = true;
        isHopping = false;
        canGrabLedge = false;
        verticalInp = 0f;

        isClimbing = true;
        playerState = PlayerState.ClimbingState;

        animator.SetFloat("movementvalue", 0f);
        playerScript.playerHanging = true;
        playerScript.SetControl(false);

        if (StableLedgeForward.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(StableLedgeForward);
    }

    public void EndDropToLedgeHang()
    {
        isEnteringLedge = false;
        animator.SetFloat("movementvalue", 0f);
    }

    // Gọi NGAY khi bắt đầu LedgeToClimb.
    // StopAllCoroutines() hủy Grab/Hop/Drop cũ của PlayerClimb để coroutine cũ
    // không thể vài frame sau bật NormalState và làm nhân vật rơi xuống.
    public void BeginRoofClimbInputLock()
    {
        if (roofClimbInputLocked)
            return;

        roofClimbInputLocked = true;

        StopAllCoroutines();

        isEnteringLedge = false;
        isHopping = false;
        canGrabLedge = false;
        verticalInp = 0f;

        isClimbing = true;
        playerState = PlayerState.ClimbingState;

        animator.SetFloat("movementvalue", 0f);
        animator.applyRootMotion = true;

        playerScript.playerHanging = true;
        playerScript.SetControl(false);
    }

    // Chỉ gọi sau khi animation LedgeToClimb đã hoàn tất và player đã ở vị trí an toàn trên mái.
    public void EndRoofClimbInputLock()
    {
        isEnteringLedge = false;
        isHopping = false;
        canGrabLedge = false;
        verticalInp = 0f;

        isClimbing = false;
        playerState = PlayerState.NormalState;

        playerScript.playerHanging = false;
        playerScript.SetControl(true);

        roofClimbInputLocked = false;
        ClearStableLedge();
    }

}