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

    private void Awake()
    {
        playerClimbScript = GetComponent<PlayerClimb>();
    }

    private void Update()
    {
        if (CampaignSession.InputBlocked) return;
        RefreshRoofLedge();
    }

    public bool RefreshRoofLedge()
    {
        if (playerClimbScript == null) playerClimbScript = GetComponent<PlayerClimb>();
        isRoofLedgeDetected = false;
        if (playerClimbScript == null || playerClimbScript.isClimbing || isDropingFromRoof) return false;
        for (int i = 0; i < rayAmount; i++)
        {
            Vector3 origin = transform.position + Vector3.up * .5f + transform.forward * rayOffset * i;
            if (!Physics.Raycast(origin, Vector3.down, out var down, rayLength, playerClimbScript.ledgeLayer, QueryTriggerInteraction.Ignore)) continue;
            if (!Physics.Raycast(down.point + transform.forward * .5f, -transform.forward, out var wall, 1f,
                playerClimbScript.ledgeLayer, QueryTriggerInteraction.Ignore)) continue;
            rayLedgeDwnHit = down; rayLedgeFwdHit = wall; isRoofLedgeDetected = true;
            return true;
        }
        return false;
    }

    public bool TryStartRoofDrop()
    {
        if (isDropingFromRoof || !RefreshRoofLedge()) return false;
        playerClimbScript.ConsumeClimbInput();
        StartCoroutine(DropToLedgeHang());
        return true;
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