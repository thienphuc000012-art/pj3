using System.Collections;
using UnityEngine;

public class RoofLedgeDetection : MonoBehaviour
{
    PlayerClimb playerClimbScript;

    public bool isRoofLedgeDetected;

    public int rayAmount = 10;

    public float rayLength = 0.5f;
    public float rayOffset = 0.15f;
    public RaycastHit rayLedgeFwdHit;
    public RaycastHit rayLedgeDwnHit;

    private void Start()
    {
        playerClimbScript = GetComponent<PlayerClimb>();
    }

    private void Update()
    {
        if (!playerClimbScript.isClimbing)
        {
            for (int i = 0; i < rayAmount; i++)
            {
                Vector3 rayPos = transform.position + Vector3.up * 0.5f + transform.forward * rayOffset * i;

                if (Physics.Raycast(rayPos, Vector3.down, out rayLedgeDwnHit, rayLength, playerClimbScript.ledgeLayer))
                {
                    isRoofLedgeDetected = true;

                    // Dùng transform.forward của Player để bắn ray chuẩn xác hơn
                    if (Physics.Raycast(rayLedgeDwnHit.point + transform.forward * 0.5f, -transform.forward, out rayLedgeFwdHit, 1f, playerClimbScript.ledgeLayer))
                    {
                        if (Input.GetKeyDown(KeyCode.C) && rayLedgeFwdHit.point != Vector3.zero)
                        {
                            StartCoroutine(DropToLedgeHang());
                        }
                    }
                    break;
                }
                else
                {
                    isRoofLedgeDetected = false;
                }
            }
        }
        else
        {
            isRoofLedgeDetected = false;
        }
    }

    public bool isDropingFromRoof;

    IEnumerator DropToLedgeHang()
    {
        isDropingFromRoof = true;

        // FIX QUAN TRỌNG: Cập nhật dữ liệu gờ mái nhà sang cho PlayerClimb
        // Giúp PlayerClimb biết điểm Y chính xác để bắn tia HopDown
        playerClimbScript.rayLedgeDownHit = rayLedgeDwnHit;
        playerClimbScript.rayLedgeForwardHit = rayLedgeFwdHit;

        // Xoay nhân vật quay mặt vào gờ tường NGAY LẬP TỨC trước khi chạy Animation/MatchTarget
        if (rayLedgeFwdHit.normal != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(-rayLedgeFwdHit.normal);
            transform.rotation = lookRot;
        }

        playerClimbScript.animator.CrossFade("droptofreehang", 0.2f);
        playerClimbScript.isClimbing = true;
        playerClimbScript.playerState = PlayerState.ClimbingState;

        yield return new WaitForSeconds(1.2f);

        isDropingFromRoof = false;
    }

    private void OnDrawGizmos()
    {
        if (rayLedgeFwdHit.point != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(rayLedgeFwdHit.point, 0.05f);
        }
    }
}