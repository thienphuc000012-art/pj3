using UnityEngine;
using UnityEngine.EventSystems;

public class EnviromentChecker : MonoBehaviour
{
    [Header("Parkour Obstacle Check")]
    public Vector3 rayOffset = new Vector3(0, 0.2f, 0);
    public float raylength = 0.9f;
    public float heightRayLength = 6f;

    // Giữ lại vì CheckLedge đang sử dụng nó.
    public LayerMask obstacleLayer;


    [Header("Check ledge")]
    [SerializeField] float ledgeRayLength = 11f;
    [SerializeField] float ledgeRayHeightThreshold = 0.76f;


    [Header("Climbing check")]
    [SerializeField] float climbingRayLength = 1.6f;
    [SerializeField] LayerMask climbingLayer;
    public int numberOfRays = 12;


    // ============================================================
    // PARKOUR OBSTACLE CHECK
    //
    // CHỈ Collider có Layer = "Obstacle" mới được bắt.
    //
    // Surface / Default / Ground / Ladder...
    // đều KHÔNG THỂ kích hoạt parkour.
    // ============================================================

    public ObstacleInfo checkObstacle()
    {
        ObstacleInfo hitData = new ObstacleInfo();

        Vector3 rayOrigin = transform.position + rayOffset;

        // Lấy index của Layer "Obstacle"
        int obstacleLayerIndex = LayerMask.NameToLayer("Obstacle");

        // Nếu project không tồn tại layer Obstacle
        if (obstacleLayerIndex == -1)
        {
            Debug.LogError(
                "PARKOUR ERROR: Không tìm thấy Layer tên 'Obstacle'. " +
                "Hãy tạo layer Obstacle trong Project Settings > Tags and Layers."
            );

            Debug.DrawRay(
                rayOrigin,
                transform.forward * raylength,
                Color.yellow
            );

            return hitData;
        }

        // Chỉ tạo Mask chứa duy nhất layer Obstacle
        int parkourObstacleMask = 1 << obstacleLayerIndex;


        // ========================================================
        // 1. RAYCAST PHÍA TRƯỚC
        // ========================================================

        bool forwardHit = Physics.Raycast(
            rayOrigin,
            transform.forward,
            out RaycastHit hit,
            raylength,
            parkourObstacleMask,
            QueryTriggerInteraction.Ignore
        );


        if (!forwardHit)
        {
            // Không gặp Obstacle
            Debug.DrawRay(
                rayOrigin,
                transform.forward * raylength,
                Color.green
            );

            return hitData;
        }


        // ========================================================
        // 2. DOUBLE CHECK LAYER CỦA COLLIDER
        // ========================================================

        if (hit.collider == null ||
            hit.collider.gameObject.layer != obstacleLayerIndex)
        {
            Debug.LogWarning(
                "PARKOUR REJECTED: " +
                (hit.collider != null ? hit.collider.name : "NULL") +
                " không thuộc Layer Obstacle."
            );

            return hitData;
        }


        hitData.hitFound = true;
        hitData.hitInfo = hit;


        Debug.DrawRay(
            rayOrigin,
            transform.forward * hit.distance,
            Color.red
        );


        Debug.Log(
            "PARKOUR FRONT HIT: " +
            hit.collider.name +
            " | Layer: " +
            LayerMask.LayerToName(hit.collider.gameObject.layer)
        );


        // ========================================================
        // 3. TÌM MẶT TRÊN CỦA OBSTACLE
        // ========================================================

        Vector3 heightOrigin =
            hit.point + Vector3.up * heightRayLength;


        bool heightHit = Physics.Raycast(
            heightOrigin,
            Vector3.down,
            out RaycastHit topHit,
            heightRayLength,
            parkourObstacleMask,
            QueryTriggerInteraction.Ignore
        );


        if (!heightHit)
        {
            // Có mặt trước nhưng không tìm được mặt trên
            hitData.hitFound = false;
            hitData.heightHitFound = false;

            Debug.DrawRay(
                heightOrigin,
                Vector3.down * heightRayLength,
                Color.yellow
            );

            return hitData;
        }


        // ========================================================
        // 4. DOUBLE CHECK MẶT TRÊN CŨNG PHẢI LÀ OBSTACLE
        // ========================================================

        if (topHit.collider == null ||
            topHit.collider.gameObject.layer != obstacleLayerIndex)
        {
            hitData.hitFound = false;
            hitData.heightHitFound = false;

            return hitData;
        }


        hitData.heightHitFound = true;
        hitData.heightHitInfo = topHit;


        Debug.DrawRay(
            heightOrigin,
            Vector3.down * topHit.distance,
            Color.blue
        );


        Debug.Log(
            "PARKOUR TOP HIT: " +
            topHit.collider.name +
            " | Layer: " +
            LayerMask.LayerToName(topHit.collider.gameObject.layer)
        );


        return hitData;
    }


    // ============================================================
    // LEDGE CHECK
    // Giữ nguyên logic cũ
    // ============================================================

    public bool CheckLedge(
        Vector3 movementDirection,
        out LedgeInfo ledgeInfo)
    {
        ledgeInfo = new LedgeInfo();

        if (movementDirection == Vector3.zero)
            return false;


        float ledgeOriginOffset = 0.5f;

        Vector3 legdeOrigin =
            transform.position +
            movementDirection * ledgeOriginOffset +
            Vector3.up;


        if (Physics.Raycast(
            legdeOrigin,
            Vector3.down,
            out RaycastHit hit,
            ledgeRayLength,
            obstacleLayer))
        {
            Debug.DrawRay(
                legdeOrigin,
                Vector3.down * ledgeRayLength,
                Color.blue
            );


            Vector3 surfaceRaycastOrigin =
                transform.position +
                movementDirection -
                new Vector3(0, 0.1f, 0);


            if (Physics.Raycast(
                surfaceRaycastOrigin,
                -movementDirection,
                out RaycastHit surfaceHit,
                2,
                obstacleLayer))
            {
                float ledgeHeight =
                    transform.position.y -
                    hit.point.y;


                if (ledgeHeight > ledgeRayHeightThreshold)
                {
                    ledgeInfo.angle =
                        Vector3.Angle(
                            transform.forward,
                            surfaceHit.normal
                        );

                    ledgeInfo.height = ledgeHeight;
                    ledgeInfo.surfaceHit = surfaceHit;

                    return true;
                }
            }
        }

        return false;
    }


    // ============================================================
    // CLIMB CHECK
    // Giữ nguyên logic cũ
    // ============================================================

    public bool CheckClimbing(
        Vector3 climbDirection,
        out RaycastHit climbInfo)
    {
        climbInfo = new RaycastHit();


        if (climbDirection == Vector3.zero)
            return false;


        Vector3 climbOrigin =
            transform.position +
            Vector3.up * 1.5f;


        Vector3 climbOffset =
            new Vector3(0, 0.19f, 0);


        for (int i = 0; i < numberOfRays; i++)
        {
            Vector3 origin =
                climbOrigin +
                climbOffset * i;


            Debug.DrawRay(
                origin,
                climbDirection,
                Color.red
            );


            if (Physics.Raycast(
                origin,
                climbDirection,
                out RaycastHit hit,
                climbingRayLength,
                climbingLayer))
            {
                climbInfo = hit;
                return true;
            }
        }


        return false;
    }


    // ============================================================
    // DROP CLIMB POINT CHECK
    // Giữ nguyên logic cũ
    // ============================================================

    [Header("Drop climb point check")]
    [SerializeField] float dropCheckDistance = 1f;
    [SerializeField] int dropCheckRayCount = 5;
    [SerializeField] float dropCheckWidthRange = 0.6f;


    public bool CheckDropClimbPoint(
        out RaycastHit DropHit)
    {
        DropHit = new RaycastHit();


        if (dropCheckRayCount <= 1)
        {
            Vector3 origin = transform.position;

            Debug.DrawRay(
                origin,
                transform.forward * dropCheckDistance,
                Color.yellow
            );


            if (Physics.Raycast(
                origin,
                transform.forward,
                out RaycastHit singleHit,
                dropCheckDistance,
                climbingLayer))
            {
                DropHit = singleHit;
                return true;
            }

            return false;
        }


        float step =
            dropCheckWidthRange /
            (dropCheckRayCount - 1);


        float startOffset =
            -dropCheckWidthRange * 0.5f;


        for (int i = 0;
             i < dropCheckRayCount;
             i++)
        {
            float widthOffset =
                startOffset +
                step * i;


            Vector3 origin =
                transform.position +
                transform.right * widthOffset;


            Debug.DrawRay(
                origin,
                transform.forward * dropCheckDistance,
                Color.yellow
            );


            if (Physics.Raycast(
                origin,
                transform.forward,
                out RaycastHit hit,
                dropCheckDistance,
                climbingLayer))
            {
                DropHit = hit;
                return true;
            }
        }


        return false;
    }
}


// ================================================================
// OBSTACLE INFO
// ================================================================

public struct ObstacleInfo
{
    public bool hitFound;
    public bool heightHitFound;

    public RaycastHit hitInfo;
    public RaycastHit heightHitInfo;
}


// ================================================================
// LEDGE INFO
// ================================================================

public struct LedgeInfo
{
    public float angle;
    public float height;

    public RaycastHit surfaceHit;
}