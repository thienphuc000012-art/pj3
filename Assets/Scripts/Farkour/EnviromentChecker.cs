using UnityEngine;
using UnityEngine.EventSystems;

public class EnviromentChecker : MonoBehaviour
{
    public Vector3 rayOffset = new Vector3(0, 0.2f, 0);
    public float raylength = 0.9f;
    public float heightRayLength = 6f;
    public LayerMask obstacleLayer;

    [Header("Check ledge")]
    [SerializeField] float ledgeRayLength = 11f;
    [SerializeField] float ledgeRayHeightThreshold = 0.76f;

    [Header("Climbing check")]
    [SerializeField] float climbingRayLength = 1.6f;
    [SerializeField] LayerMask climbingLayer;
    public int numberOfRays = 12;

 

 
    public ObstacleInfo checkObstacle()
    {
        var hitData = new ObstacleInfo();

        var rayOrigin = transform.position + rayOffset;
        hitData.hitFound = Physics.Raycast(rayOrigin, transform.forward, out hitData.hitInfo, raylength, obstacleLayer);

        Debug.DrawRay(rayOrigin, transform.forward * raylength, (hitData.hitFound) ? Color.red : Color.green);

        if (hitData.hitFound)
        {
            var heightOrigin = hitData.hitInfo.point + Vector3.up * heightRayLength;
            hitData.heightHitFound = Physics.Raycast(heightOrigin, Vector3.down, out hitData.heightHitInfo, heightRayLength, obstacleLayer);

            Debug.DrawRay(heightOrigin, Vector3.down * heightRayLength, (hitData.heightHitFound) ? Color.blue : Color.green);
        }

        return hitData;
    }

    public bool CheckLedge(Vector3 movementDirection, out LedgeInfo ledgeInfo)
    {
        ledgeInfo = new LedgeInfo();
        if (movementDirection == Vector3.zero)

            return false;

        float ledgeOriginOffset = 0.5f;
        var legdeOrigin = transform.position + movementDirection * ledgeOriginOffset + Vector3.up;


        if (Physics.Raycast(legdeOrigin, Vector3.down, out RaycastHit hit, ledgeRayLength, obstacleLayer))
        {
            Debug.DrawRay(legdeOrigin, Vector3.down * ledgeRayLength, Color.blue);

            var surfaceRaycastOrigin = transform.position + movementDirection - new Vector3(0, 0.1f, 0);
            if(Physics.Raycast(surfaceRaycastOrigin, - movementDirection, out RaycastHit surfaceHit, 2, obstacleLayer))
            {
                 float ledgeHeight = transform.position.y - hit.point.y;
                if (ledgeHeight > ledgeRayHeightThreshold)
                {
                    ledgeInfo.angle =   Vector3.Angle(transform.forward, surfaceHit.normal);
                    ledgeInfo.height = ledgeHeight; 
                    ledgeInfo.surfaceHit = surfaceHit;
                    return true;
                }
            }
        }
        return false;
    }

    public bool CheckClimbing(Vector3 climbDirection, out RaycastHit climbInfo)
    {
        climbInfo = new RaycastHit();

        if(climbDirection == Vector3.zero)
            return false;

        var climbOrigin = transform.position  + Vector3.up * 1.5f;
        var climbOffset = new Vector3(0, 0.19f, 0);

        for (int i = 0; i < numberOfRays; i++)
        {
            Debug.DrawRay(climbOrigin + climbOffset * i, climbDirection, Color.red);
            if (Physics.Raycast(climbOrigin + climbOffset * i , climbDirection, out RaycastHit hit, climbingRayLength, climbingLayer))
            {
                climbInfo = hit;
                return true;
            }
        }

        return false;
    }

    [Header("Drop climb point check")]
    [SerializeField] float dropCheckDistance = 1f;
    [SerializeField] int dropCheckRayCount = 5;
    [SerializeField] float dropCheckWidthRange = 0.6f; // tổng phạm vi quét theo chiều ngang
    public bool CheckDropClimbPoint(out RaycastHit DropHit)
    {
        DropHit = new RaycastHit();
        // Quét nhiều tia ở các vị trí ngang khác nhau quanh vị trí chân player
        float step = dropCheckWidthRange / (dropCheckRayCount - 1);
        float startOffset = -dropCheckWidthRange * 0.5f;
        for (int i = 0; i < dropCheckRayCount; i++)
        {
            float widthOffset = startOffset + step * i;
            var origin = transform.position + transform.right * widthOffset;
            Debug.DrawRay(origin, transform.forward * dropCheckDistance, Color.yellow);
            if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, dropCheckDistance, climbingLayer))
            {
                DropHit = hit;
                return true;
            }
        }
        return false;
    }
}
public struct ObstacleInfo
{
    public bool hitFound;
    public bool heightHitFound;
    public RaycastHit hitInfo;
    public RaycastHit heightHitInfo;

}

public struct LedgeInfo
{
    public float angle; 
    public float height;
    public RaycastHit surfaceHit;
}
