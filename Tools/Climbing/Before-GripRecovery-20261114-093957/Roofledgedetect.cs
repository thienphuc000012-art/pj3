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
        if (CampaignSession.InputBlocked) return;
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
        if (isDropingFromRoof)
            yield break;

        isDropingFromRoof = true;

        // Chụp hit NGAY tại frame bắt đầu drop.
        // Từ đây về sau KHÔNG dùng transform hiện tại để suy ra lại vị trí gờ.
        RaycastHit stableForwardHit = rayLedgeFwdHit;
        RaycastHit stableDownHit = rayLedgeDwnHit;

        if (stableForwardHit.collider == null || stableDownHit.collider == null)
        {
            isDropingFromRoof = false;
            yield break;
        }

        // Khóa CC/control + lưu stable ledge anchor ngay lập tức.
        playerClimbScript.BeginDropToLedgeHang(stableForwardHit, stableDownHit);

        playerClimbScript.animator.CrossFade("droptofreehang", 0.2f);

        // Không dùng WaitForSeconds(1.2f): clip/transition có thể dài ngắn khác nhau.
        // Chỉ mở lại hệ thống ledge khi droptofreehang thực sự gần hoàn tất.
        yield return null;

        while (playerClimbScript.animator.IsInTransition(0))
            yield return null;

        while (playerClimbScript.animator.GetCurrentAnimatorStateInfo(0).IsName("droptofreehang") &&
               playerClimbScript.animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f)
        {
            yield return null;
        }

        playerClimbScript.EndDropToLedgeHang();
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