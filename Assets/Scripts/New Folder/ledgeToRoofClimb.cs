using System.Collections;
using UnityEngine;

public class LedgeToRoofClimb : MonoBehaviour
{
    PlayerClimb playerClimb;
    RoofLedgeDetection roofLedgeDetection;

    public LayerMask ledgeGroundLayer;

    [Header("Roof Climb Detection")]
    public float rayHeight = 1.8f;
    public float roofInset = 0.45f;         // Đưa ray vào BÊN TRONG mái, tránh bắn đúng mép collider.
    public float roofProbeHeight = 0.8f;
    public float roofProbeDistance = 1.5f;

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

    private void Start()
    {
        playerClimb = GetComponent<PlayerClimb>();
        roofLedgeDetection = GetComponent<RoofLedgeDetection>();
    }

    private void Update()
    {
        if (playerClimb.isClimbing &&
            !playerClimb.isHopping &&
            !playerClimb.IsEnteringLedge &&
            !roofLedgeDetection.isDropingFromRoof &&
            !isClimbingToRoof)
        {
            // FIX QUAN TRỌNG:
            // Không dùng shimmyController.ledgeHit hoặc transform.forward làm nguồn chuẩn.
            // droptofreehang có Root Motion nên cả hai có thể chưa cập nhật đồng bộ trong cùng frame.
            if (!playerClimb.HasStableLedge)
            {
                foundLedgeToRoofClimb = false;
                hasRoofRootTarget = false;
            }
            else
            {
                Vector3 ledgeForward = playerClimb.StableLedgeForward;
                ledgeForward.y = 0f;

                if (ledgeForward.sqrMagnitude < 0.0001f)
                {
                    foundLedgeToRoofClimb = false;
                    hasRoofRootTarget = false;
                    return;
                }

                ledgeForward.Normalize();
                Vector3 stableTop = playerClimb.StableLedgeTopPoint;

                // Origin XZ khóa theo mép gờ; Y vẫn theo chiều cao nhân vật đang treo.
                // Nhờ vậy Root Motion của droptofreehang không làm tia đỏ trượt ngang khỏi gờ.
                Vector3 rayStartForward = new Vector3(
                    stableTop.x,
                    transform.position.y + rayHeight,
                    stableTop.z
                ) - ledgeForward * 0.15f;

                Debug.DrawRay(rayStartForward, ledgeForward, Color.red);

                // Có vật cản phía trong mái/ở trước đầu thì không cho leo.
                if (Physics.Raycast(
                    rayStartForward,
                    ledgeForward,
                    1f,
                    ledgeGroundLayer,
                    QueryTriggerInteraction.Ignore))
                {
                    foundLedgeToRoofClimb = false;
                    hasRoofRootTarget = false;
                }
                else
                {
                    // Tia xanh xuất phát từ STABLE TOP POINT và hướng vào mái bằng STABLE NORMAL.
                    // Không còn phụ thuộc model bị lệch sau droptofreehang.
                    Vector3 rayStartDown =
                        stableTop +
                        ledgeForward * roofInset +
                        Vector3.up * roofProbeHeight;

                    Debug.DrawRay(rayStartDown, Vector3.down * roofProbeDistance, Color.blue);

                    if (Physics.Raycast(
                        rayStartDown,
                        Vector3.down,
                        out ledgeToClimbHit,
                        roofProbeDistance,
                        ledgeGroundLayer,
                        QueryTriggerInteraction.Ignore))
                    {
                        foundLedgeToRoofClimb = true;

                        roofRootTarget = GetStandingRootPosition(ledgeToClimbHit.point);
                        roofTargetRotation = Quaternion.LookRotation(ledgeForward);
                        hasRoofRootTarget = true;

                        if (Input.GetKeyDown(KeyCode.C) && !Input.GetKey(KeyCode.S))
                        {
                            isClimbingToRoof = true;

                            playerClimb.BeginRoofClimbInputLock();
                            playerClimb.animator.SetFloat("movementvalue", 0f);

                            if (climbPointObjPrefab != null)
                            {
                                climbPointObj = Instantiate(
                                    climbPointObjPrefab,
                                    roofRootTarget,
                                    roofTargetRotation
                                );
                            }

                            StartCoroutine(LedgeToClimb());
                        }
                    }
                    else
                    {
                        foundLedgeToRoofClimb = false;
                        hasRoofRootTarget = false;
                    }
                }
            }
        }
        else if (!playerClimb.isClimbing || playerClimb.IsEnteringLedge || roofLedgeDetection.isDropingFromRoof)
        {
            foundLedgeToRoofClimb = false;
            hasRoofRootTarget = false;
        }

        // FIX CHÍNH:
        // Match ROOT tới vị trí đứng trên mái thay vì Match RightFoot tới mặt sàn.
        // RightFoot có offset theo pose/clip; khi GrabLedge hoặc DropToLedgeHang làm root lệch,
        // Unity sẽ bù offset chân bằng cách kéo cả root xuống dưới mái.
        AnimatorStateInfo state = playerClimb.animator.GetCurrentAnimatorStateInfo(0);
        if (hasRoofRootTarget &&
            state.IsName("climbuproof") &&
            state.normalizedTime < matchEndTime &&
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

        while (
            playerClimb.animator.GetCurrentAnimatorStateInfo(0).IsName("climbuproof") &&
            playerClimb.animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.98f)
        {
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
