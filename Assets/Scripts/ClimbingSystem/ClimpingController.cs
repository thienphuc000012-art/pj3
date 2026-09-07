using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ClimpingController : MonoBehaviour
{
    EnviromentChecker ec;

    public PlayerScript playerScript;

    ClimbingPoint currentClimbPoint;

    public float InOutValue;
    public float UpDownValue;
    public float LeftRightValue;

    void Awake()
    {
        ec = GetComponent<EnviromentChecker>();
    }
    void Update()
    {
        if (!playerScript.playerHanging)

        {
            if (Input.GetButton("Jump") && !playerScript.playerInAction)
            {
                if (ec.CheckClimbing(transform.forward, out RaycastHit climbInfo))
                {
                    currentClimbPoint = climbInfo.transform.GetComponent<ClimbingPoint>();
                    playerScript.SetControl(false);
                    InOutValue = -0.23f;   // ra ngoài
                    UpDownValue = -0.09f;   // nâng lên
                    LeftRightValue = 0.15f;
                    StartCoroutine(ClimbToLedge("idleToClimb", climbInfo.transform, 0.40f, 0.54f, playerHandOffset: new Vector3(InOutValue, UpDownValue, LeftRightValue)));
                }
            }
            if (Input.GetButton("leave") && !playerScript.playerInAction)
            {
                if (ec.CheckDropClimbPoint(out RaycastHit DropHit))
                {
                    currentClimbPoint = GetNearestClimbingPoint(DropHit.transform, DropHit.point);
                    playerScript.SetControl(false);
                    InOutValue = 0.23f;   // ra ngoài
                    UpDownValue = -0.44f;   // nâng lên
                    LeftRightValue = 0.25f;
                    StartCoroutine(ClimbToLedge("droptohang", currentClimbPoint.transform, 0.41f, 0.54f, playerHandOffset: new Vector3(InOutValue, UpDownValue, LeftRightValue)));

                }
            }
        }
        else
        {
            if (Input.GetButton("leave") && !playerScript.playerInAction)
            {
                StartCoroutine(JumpFromWall());
                return;
            }
            float horizontal = Mathf.Round(Input.GetAxisRaw("Horizontal"));
            float vertical = Mathf.Round(Input.GetAxisRaw("Vertical"));

            var inputDirection = new Vector2(horizontal, vertical);

            if (playerScript.playerInAction || inputDirection == Vector2.zero) return;
            //climb to top
            if (currentClimbPoint.MountPoint && inputDirection.y == 1)
            {
                StartCoroutine(ClimbToTop());
                return;
            }
            //ledge to ledge parkour action
            var neigbour = currentClimbPoint.GetNeighbour(inputDirection);
            if (neigbour == null) return;

            if (neigbour.connectionType == ConnectionType.Jump && Input.GetButton("Jump"))
            {

                currentClimbPoint = neigbour.climbingPoint;
                if (neigbour.pointDirection.y == 1)
                {
                    InOutValue = -0.1f;   // ra ngoài
                    UpDownValue = -0.05f;   // nâng lên
                    LeftRightValue = 0.25f;
                    StartCoroutine(ClimbToLedge("ClimbUp", currentClimbPoint.transform, 0.34f, 0.64f, playerHandOffset: new Vector3(InOutValue, UpDownValue, LeftRightValue)));
                }
                else if (neigbour.pointDirection.y == -1)
                {
                    InOutValue = -0.2f;   // ra ngoài
                    UpDownValue = -0.05f;   // nâng lên
                    LeftRightValue = 0.25f;
                    StartCoroutine(ClimbToLedge("ClimbDown", currentClimbPoint.transform, 0.31f, 0.68f, playerHandOffset: new Vector3(InOutValue, UpDownValue, LeftRightValue)));
                }
                else if (neigbour.pointDirection.x == 1)
                {

                    StartCoroutine(ClimbToLedge("climbright", currentClimbPoint.transform, 0.2f, 0.51f));
                }
                else if (neigbour.pointDirection.x == -1)
                {
                    InOutValue = -0.1f;   // ra ngoài
                    UpDownValue = -0.04f;   // nâng lên
                    LeftRightValue = 0.25f;
                    StartCoroutine(ClimbToLedge("Climbleft", currentClimbPoint.transform, 0.2f, 0.51f, playerHandOffset: new Vector3(InOutValue, UpDownValue, LeftRightValue)));
                }
            }
            else if (neigbour.connectionType == ConnectionType.Move)
            {
                currentClimbPoint = neigbour.climbingPoint;


                if (neigbour.pointDirection.x == 1)
                {
                    InOutValue = -0.2f;   // ra ngoài
                    UpDownValue = -0.03f;   // nâng lên
                    LeftRightValue = 0.25f;
                    StartCoroutine(ClimbToLedge("shimmyRight", currentClimbPoint.transform, 0f, 0.30f, playerHandOffset: new Vector3(InOutValue, UpDownValue, LeftRightValue)));
                }
                if (neigbour.pointDirection.x == -1)
                {
                    InOutValue = -0.2f;   // ra ngoài
                    UpDownValue = -0.03f;   // nâng lên
                    LeftRightValue = 0.25f;
                    StartCoroutine(ClimbToLedge("shimmyLeft", currentClimbPoint.transform, 0f, 0.30f, AvatarTarget.LeftHand, playerHandOffset: new Vector3(InOutValue, UpDownValue, LeftRightValue)));
                }
            }

        }
    }

    IEnumerator ClimbToLedge(string animationName, Transform ledgePoint, float compareStartTime, float compareEndTime, AvatarTarget hand = AvatarTarget.RightHand, Vector3? playerHandOffset = null)
    {
        var compareParams = new CompareTargetParameter()
        {
            position = SetHandPosition(ledgePoint, hand, playerHandOffset, ledgePoint.forward), // hoặc surfaceHit.normal
            bodyPart = hand,
            positionWeight = Vector3.one,
            startTime = compareStartTime,
            endTime = compareEndTime
        };


        var requiredRot = Quaternion.LookRotation(-ledgePoint.forward);

        yield return playerScript.PerformAction(animationName, compareParams, requiredRot, true);

        playerScript.playerHanging = true;
    }
    Vector3 SetHandPosition(Transform ledge, AvatarTarget hand, Vector3? playerhandOffset, Vector3 surfaceNormal)
    {
        var offsetValue = (playerhandOffset != null) ? playerhandOffset.Value : new Vector3(InOutValue, UpDownValue, LeftRightValue);
        var handDirection = (hand == AvatarTarget.RightHand) ? ledge.right : -ledge.right;

        return ledge.position
               + surfaceNormal * offsetValue.x   // đẩy ra ngoài theo normal
               + Vector3.up * offsetValue.y      // nâng lên
               - handDirection * offsetValue.z;  // dịch sang trái/phải
    }


    IEnumerator JumpFromWall()
    {
        playerScript.playerHanging = false;
        yield return playerScript.PerformAction("jumpformwall");
        playerScript.ResetRequiredRotation();
        playerScript.SetControl(true);
    }
    IEnumerator ClimbToTop()
    {
        playerScript.playerHanging = false;
        yield return playerScript.PerformAction("climbtotop");
        playerScript.EnableCC(true);

        yield return new WaitForSeconds(0.5f);
        playerScript.ResetRequiredRotation();
        playerScript.SetControl(true);
    }

    ClimbingPoint GetNearestClimbingPoint(Transform dropClimbPoint, Vector3 hitPoint)
    {
        var points = dropClimbPoint.GetComponentsInChildren<ClimbingPoint>();

        ClimbingPoint nearestPoint = null;
        float nearestPointDistance = Mathf.Infinity;

        foreach (var point in points)
        {
            float distance = Vector3.Distance(point.transform.position, hitPoint);
            if (distance < nearestPointDistance)
            {
                nearestPoint = point;
                nearestPointDistance = distance;
            }
        }
        return nearestPoint;
    }
}
