using System.Collections;
using UnityEngine;

public class LedgeToRoofClimb : MonoBehaviour
{
    PlayerClimb playerClimb;
    ShimmyController shimmyController;
    RoofLedgeDetection roofLedgeDetection;

    public LayerMask ledgeGroundLayer;

    public float rayHeight = 1.8f;

    RaycastHit ledgeToClimbHit;

    public bool foundLedgeToRoofClimb;

    public GameObject climbPointObjPrefab;
    public GameObject climbPointObj;

    bool isClimbingToRoof = false;

    private void Start()
    {
        playerClimb = GetComponent<PlayerClimb>();
        shimmyController = GetComponent<ShimmyController>();
        roofLedgeDetection = GetComponent<RoofLedgeDetection>();
    }

    private void Update()
    {
        if (playerClimb.isClimbing && !roofLedgeDetection.isDropingFromRoof && !isClimbingToRoof)
        {
            Vector3 rayStartForward = transform.position + new Vector3(0, rayHeight, 0);
            Debug.DrawRay(rayStartForward, transform.forward, Color.red);

            if (Physics.Raycast(rayStartForward, transform.forward, 1f, ledgeGroundLayer))
            {
                foundLedgeToRoofClimb = false;
            }
            else
            {
                Vector3 rayStartDown = shimmyController.ledgeHit.point + new Vector3(0, 0.7f, 0);
                Debug.DrawRay(rayStartDown, Vector3.down, Color.blue);

                if (Physics.Raycast(rayStartDown, Vector3.down, out ledgeToClimbHit, 1f, ledgeGroundLayer))
                {
                    foundLedgeToRoofClimb = true;

                    if (Input.GetKeyDown(KeyCode.C) && !Input.GetKey(KeyCode.S))
                    {
                        isClimbingToRoof = true;

                        // SỬA LỖI 1: Đẩy điểm Target lùi sâu vào trong mái nhà (forward) khoảng 0.3f tới 0.5f 
                        // để đảm bảo khi kết thúc animation, nhân vật đứng hẳn trên mái nhà, không chênh vênh ở mép.
                        Vector3 safeRoofPosition = ledgeToClimbHit.point + (transform.forward * 0.4f);

                        climbPointObj = Instantiate(climbPointObjPrefab, safeRoofPosition, Quaternion.identity);
                        StartCoroutine(LedgeToClimb());
                    }
                }
                else
                {
                    foundLedgeToRoofClimb = false;
                }
            }
        }
        else if (!playerClimb.isClimbing)
        {
            foundLedgeToRoofClimb = false;
        }

        // SỬA LỖI 2: Đổi Weight Mask từ (0,1,1) thành (1,1,1) để tính toán cả trục X, Y và Z.
        if (climbPointObj != null && playerClimb.animator.GetCurrentAnimatorStateInfo(0).IsName("climbuproof") && !playerClimb.animator.IsInTransition(0))
        {
            playerClimb.animator.MatchTarget(climbPointObj.transform.position, transform.rotation, AvatarTarget.RightFoot, new MatchTargetWeightMask(new Vector3(1, 1, 1), 0), 0.41f, 0.87f);
        }
    }


    IEnumerator LedgeToClimb()
    {
        playerClimb.animator.CrossFade("climbuproof", 0.2f);

        yield return new WaitForSeconds(1.2f);

        if (climbPointObj != null)
        {
            Destroy(climbPointObj);
            climbPointObj = null;
        }

        playerClimb.isClimbing = false;
        playerClimb.playerState = PlayerState.NormalState;

        isClimbingToRoof = false;
    }
}