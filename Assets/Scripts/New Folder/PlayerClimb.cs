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

    public int rayAmount = 10;
    public float rayLength = 0.5f;
    public float rayOffset = 0.15f;
    public float rayHeight = 1.7f;

    public RaycastHit rayLedgeForwardHit;
    public RaycastHit rayLedgeDownHit;

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
        if (playerScript.playerInAction)
            return;

        CheckingMainRay();
        Inputs();
        StateConditionsCheck();
        MatchTargetToLedge();

        // Chỉ kiểm tra Hop khi đang đu thang VÀ không trong quá trình Hop
        if (isClimbing && !isHopping)
        {
            HopUpDown();
        }
    }

    private void Inputs()
    {
        if (Input.GetKeyDown(KeyCode.C) && !roofLedgeDetection.isRoofLedgeDetected)
        {
            if (!isClimbing)
            {
                if (canGrabLedge && rayLedgeDownHit.point != Vector3.zero)
                {
                    Quaternion lookRot = Quaternion.LookRotation(-rayLedgeForwardHit.normal);
                    transform.rotation = lookRot;

                    StartCoroutine(GrabLedge());
                }
            }
            else
            {
                // Thả tay khỏi gờ
                if (verticalInp == 0 && !ledgeToRoofClimb.foundLedgeToRoofClimb && !isHopping)
                    StartCoroutine(DropLedge());
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
            Vector3 handPos = transform.forward * rayZHandCorrection + transform.up * rayYHandCorrection;
            animator.MatchTarget(rayLedgeDownHit.point + handPos, transform.rotation, AvatarTarget.RightHand, new MatchTargetWeightMask(new Vector3(0, 1, 1), 0), 0.36f, 0.57f);
        }

        if (animator.GetCurrentAnimatorStateInfo(0).IsName("droptofreehang") && !animator.IsInTransition(0))
        {
            Vector3 handDropPos = transform.forward * zDropToHangPos + transform.up * yDropToHangPos;
            animator.MatchTarget(roofLedgeDetection.rayLedgeFwdHit.point + handDropPos, transform.rotation, AvatarTarget.LeftHand, new MatchTargetWeightMask(new Vector3(0, 1, 1), 0), 0.65f, 0.71f);
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
        verticalInp = Input.GetAxisRaw("Vertical");

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
                    if (Input.GetKeyDown(KeyCode.C))
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
                    if (Input.GetKeyDown(KeyCode.C))
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
        playerState = PlayerState.ClimbingState;
        isClimbing = true;
        animator.CrossFade("idle to hang", 0.2f);
        yield return null;
    }

    public IEnumerator DropLedge()
    {
        animator.CrossFade("bracehangdrop", 0.2f);
        yield return new WaitForSeconds(0.5f);
        playerState = PlayerState.NormalState;
        isClimbing = false;
    }

    IEnumerator HopUp()
    {
        isHopping = true;

        animator.CrossFade("braced hang hop up", 0.2f);

        // Đợi animation thực hiện xong
        yield return new WaitForSeconds(0.6f);

        // FIX QUAN TRỌNG: Cập nhật thông tin gờ mới thành gờ chính sau khi nhảy xong
        rayLedgeDownHit = hopLedgeDownHit;
        rayLedgeForwardHit = hopLedgeForwardHit;

        isHopping = false;
    }

    IEnumerator HopDown()
    {
        isHopping = true;

        animator.CrossFade("brace hang drop", 0.2f);

        // Đợi animation thực hiện xong
        yield return new WaitForSeconds(0.6f);

        // FIX QUAN TRỌNG: Cập nhật thông tin gờ mới thành gờ chính sau khi nhảy xong
        rayLedgeDownHit = hopLedgeDownHit;
        rayLedgeForwardHit = hopLedgeForwardHit;

        isHopping = false;
    }
}