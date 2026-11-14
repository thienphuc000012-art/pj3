using System;
using System.Collections;
using UnityEngine;

public enum PlayerState { NormalState, ClimbingState }
public enum HangingLedgeAction { None, HopUp, HopDown, ClimbToRoof }

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
    [Tooltip("Hand height relative to the destination ledge in metres. Lower values lower the hanging player.")]
    public float upHopUpPos = -0.0434f;
    public float forwardHopUpPos = -0.05f;

    [Header("Hop Down Offset")]
    public float upHopDownPos = -0.1f;
    public float forwardHopDownPos = -0.05f;

    [Header("Hop Down Detection")]
    [Tooltip("Maximum vertical distance to a lower ledge, in metres. Independent of Hop Up's ray count.")]
    [Min(0.1f)] public float hopDownMaxDrop = 2.5f;
    [Tooltip("Vertical spacing between the downward search's horizontal wall probes.")]
    [Min(0.025f)] public float hopDownProbeStep = 0.1f;

    [Space(5)]
    float verticalInp;
    public float rayHopOffset = 0.1f;
    public float rayHopLength = 1f;
    public int hopRayAmount = 7;
    public float rayVerticalGap = 0.3f; // Khoảng cách bắt đầu kiểm tra gờ trên/dưới
    [Tooltip("Start hop probes outside protruding upper ledges. The forward reach from the current ledge remains Ray Hop Length.")]
    [Min(0.05f)] public float hopProbeOutset = 0.8f;

    [Tooltip("Keeps a C press briefly while the previous hop finishes blending into the hanging pose.")]
    [Min(0f)] public float climbInputBufferSeconds = 0.2f;
    bool hasClimbRequest;
    float climbRequestExpires;
    float climbRequestDirection;

    RaycastHit hopLedgeForwardHit;
    RaycastHit hopLedgeDownHit;

    private void Awake()
    {
        playerScript = GetComponent<PlayerScript>();
        if (animator == null) animator = GetComponent<Animator>();
        roofLedgeDetection = GetComponent<RoofLedgeDetection>();
        ledgeToRoofClimb = GetComponent<LedgeToRoofClimb>();
    }

    private void Update()
    {
        if (CampaignSession.InputBlocked) { hasClimbRequest = false; return; }
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
        bool wasClimbing = isClimbing;

        CheckingMainRay();
        Inputs();
        StateConditionsCheck();
        MatchTargetToLedge();

        // One owner consumes C. Never let RoofClimb and Hop inspect stale flags in separate Updates.
        // The C used to grab the first ledge cannot also request a hop.
        if (wasClimbing && Input.GetKeyDown(KeyCode.C) && !isEnteringLedge && !roofClimbInputLocked)
        {
            hasClimbRequest = true;
            climbRequestDirection = verticalInp;
            climbRequestExpires = Time.time + climbInputBufferSeconds;
        }
        if (hasClimbRequest && Time.time > climbRequestExpires) hasClimbRequest = false;
        if (hasClimbRequest && CanStartLedgeAction)
        {
            hasClimbRequest = false;
            HangingLedgeAction action = FindClimbAction(climbRequestDirection, out var wall, out var top);
            if (action == HangingLedgeAction.ClimbToRoof) ledgeToRoofClimb.TryBeginRoofClimb();
            else if (action == HangingLedgeAction.HopUp || action == HangingLedgeAction.HopDown)
            {
                hopLedgeForwardHit = wall; hopLedgeDownHit = top;
                StartCoroutine(action == HangingLedgeAction.HopUp ? HopUp() : HopDown());
            }
        }
    }

    public bool CanStartLedgeAction => isClimbing && HasStableLedge && !isHopping && !isEnteringLedge &&
        !roofClimbInputLocked && roofLedgeDetection != null && !roofLedgeDetection.isDropingFromRoof &&
        ledgeToRoofClimb != null && !ledgeToRoofClimb.IsClimbingToRoof && animator != null && !animator.IsInTransition(0);

    // ShimmyController moves the root and ledge anchor together. Blend-tree root motion must not add a second displacement.
    public bool UsesManualHangingMovement => CanStartLedgeAction && animator.GetCurrentAnimatorStateInfo(0).IsName("ledgeMovement");

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
            // PlayerScript.OnAnimatorMove already ignores root motion while normal movement owns control.
            // Toggling applyRootMotion here reinitializes the Animator when grabbing a ledge.
            animator.applyRootMotion = true;
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
        if (isHopping && animator.GetCurrentAnimatorStateInfo(0).IsName("braced hang hop up") && !animator.IsInTransition(0) &&
            !animator.isMatchingTarget && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.59f)
        {
            Vector3 handHopUpPos = StableLedgeForward * forwardHopUpPos + Vector3.up * upHopUpPos;
            animator.MatchTarget(hopLedgeDownHit.point + handHopUpPos, transform.rotation, AvatarTarget.LeftHand, new MatchTargetWeightMask(Vector3.one, 0), 0.39f, 0.59f);
        }

        // 3. Hop Down Target Match
        if (isHopping && animator.GetCurrentAnimatorStateInfo(0).IsName("brace hang drop") && !animator.IsInTransition(0) &&
            !animator.isMatchingTarget && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.56f)
        {
            Vector3 handHopDownPos = StableLedgeForward * forwardHopDownPos + Vector3.up * upHopDownPos;
            animator.MatchTarget(hopLedgeDownHit.point + handHopDownPos, transform.rotation, AvatarTarget.LeftHand, new MatchTargetWeightMask(Vector3.one, 0), 0.31f, 0.56f);
        }
    }

    /// <summary>Resolve fresh geometry, independently of W being held or newly pressed.</summary>
    public HangingLedgeAction FindClimbAction(float direction, out RaycastHit wall, out RaycastHit top)
    {
        wall = default; top = default;
        if (!HasStableLedge) return HangingLedgeAction.None;
        if (direction < -0.1f)
            return TryFindHopTarget(-1, out wall, out top) ? HangingLedgeAction.HopDown : HangingLedgeAction.None;

        // Reach the next handhold before attempting to stand on a narrow intermediate climbing strip.
        if (TryFindHopTarget(1, out wall, out top))
            return direction > 0.1f ? HangingLedgeAction.HopUp : HangingLedgeAction.None;
        return ledgeToRoofClimb != null && ledgeToRoofClimb.RefreshRoofTarget()
            ? HangingLedgeAction.ClimbToRoof : HangingLedgeAction.None;
    }

    public bool TryFindHopTarget(int direction, out RaycastHit wall, out RaycastHit top)
    {
        wall = default; top = default;
        if (!HasStableLedge) return false;
        Vector3 forward = StableLedgeForward.normalized;
        // Both XZ and Y come from the committed ledge, not an in-flight root-motion pose.
        Vector3 origin = StableLedgeTopPoint - forward * hopProbeOutset;
        int sign = direction < 0 ? -1 : 1;
        float downStart = Mathf.Min(Mathf.Max(0.05f, rayVerticalGap), Mathf.Max(0.1f, hopDownMaxDrop));
        float downStep = Mathf.Max(0.025f, hopDownProbeStep);
        // Search a little below the maximum ledge height so the ray meets its vertical face,
        // then reject any top surface beyond Max Drop. This also covers thin ledges at the limit.
        float downEnd = Mathf.Max(0.1f, hopDownMaxDrop) + 0.5f;
        int count = sign < 0 ? Mathf.CeilToInt((downEnd - downStart) / downStep) + 1 : hopRayAmount;
        for (int i = 0; i < count; i++)
        {
            float height = sign < 0 ? Mathf.Min(downStart + downStep * i, downEnd) : rayVerticalGap + rayHopOffset * i;
            Vector3 rayPosition = origin + Vector3.up * sign * height;
            float probeLength = hopProbeOutset + rayHopLength;
            Debug.DrawRay(rayPosition, forward * probeLength, sign < 0 ? Color.cyan : Color.green);
            if (!Physics.Raycast(rayPosition, forward, out var candidateWall, probeLength, ledgeLayer, QueryTriggerInteraction.Ignore)) continue;
            if (Mathf.Abs(candidateWall.normal.y) > 0.5f) continue;
            // Move just inside the edge so an exact-on-the-face ray cannot miss the top.
            Vector3 downOrigin = candidateWall.point + forward * 0.025f + Vector3.up * 0.5f;
            if (!Physics.Raycast(downOrigin, Vector3.down, out var candidateTop, 0.7f, ledgeLayer, QueryTriggerInteraction.Ignore)) continue;
            if (candidateTop.normal.y < 0.6f || sign * (candidateTop.point.y - StableLedgeTopPoint.y) < 0.1f) continue;
            if (sign < 0 && StableLedgeTopPoint.y - candidateTop.point.y > Mathf.Max(0.1f, hopDownMaxDrop) + 0.001f) continue;
            wall = candidateWall; top = candidateTop;
            return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!HasStableLedge) return;
        Vector3 origin = StableLedgeTopPoint - StableLedgeForward * hopProbeOutset;
        Vector3 lowestLedge = origin - Vector3.up * Mathf.Max(0.1f, hopDownMaxDrop);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, lowestLedge);
        Gizmos.DrawLine(lowestLedge, lowestLedge + StableLedgeForward * (hopProbeOutset + rayHopLength));
    }

    IEnumerator WaitForLedgePose(string actionState)
    {
        // Wait through the entire outgoing blend, not just to 95% of the source clip.
        // Previously a new C could be accepted while the old hop was still blending/root-moving.
        float deadline = Time.time + 1f;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(actionState))
        {
            if (Time.time >= deadline) yield break;
            yield return null;
        }
        while (animator.GetCurrentAnimatorStateInfo(0).IsName(actionState) || animator.IsInTransition(0))
        {
            if (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f) break;
            yield return null;
        }
    }

    void AlignHangingHand(AvatarTarget hand, Vector3 top, float verticalOffset, float depthOffset)
    {
        if (!animator.isHuman) return;
        Transform bone = animator.GetBoneTransform(hand == AvatarTarget.RightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        if (bone == null) return;
        Vector3 target = top + Vector3.up * verticalOffset + StableLedgeForward * depthOffset;
        transform.position += target - bone.position;
        Physics.SyncTransforms();
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

        yield return WaitForLedgePose("idle to hang");
        AlignHangingHand(AvatarTarget.RightHand, StableLedgeTopPoint, rayYHandCorrection, rayZHandCorrection);

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

        yield return WaitForLedgePose("braced hang hop up");
        SetStableLedge(targetForwardHit, targetDownHit);
        AlignHangingHand(AvatarTarget.LeftHand, targetDownHit.point, upHopUpPos, forwardHopUpPos);

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

        yield return WaitForLedgePose("brace hang drop");
        SetStableLedge(targetForwardHit, targetDownHit);
        AlignHangingHand(AvatarTarget.LeftHand, targetDownHit.point, upHopDownPos, forwardHopDownPos);

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
        hasClimbRequest = false;
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
        hasClimbRequest = false;

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
