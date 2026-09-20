using UnityEngine;


[CreateAssetMenu(
    menuName = "farkour menu/create new farkour action"
)]
public class NewFakourAction : ScriptableObject
{
    [Header("checking obstacle height")]

    [SerializeField]
    string animationName;

    [SerializeField]
    string barrierTag;

    [SerializeField]
    float minimumHeight;

    [SerializeField]
    float maximumHeight;


    [Header("rotating player towards obstacle")]

    [SerializeField]
    bool lookAtObstacle;

    [SerializeField]
    float parkourActionDelay;


    public Quaternion RequiredRotation
    {
        get;
        set;
    }


    [Header("target matching")]

    [SerializeField]
    bool allowTargetMatching = true;

    [SerializeField]
    AvatarTarget compareBodyPart;

    [SerializeField]
    float compareStartTime;

    [SerializeField]
    float compareEndTime;

    [SerializeField]
    Vector3 comparePositionWeight =
        new Vector3(0, 1, 0);


    public Vector3 ComparePosition
    {
        get;
        set;
    }


    // ============================================================
    // CHECK PARKOUR ACTION
    // ============================================================

    public bool checkIfAvailable(
        ObstacleInfo hitData,
        Transform player)
    {
        // ========================================================
        // 1. PHẢI CÓ HIT PHÍA TRƯỚC
        // ========================================================

        if (!hitData.hitFound)
            return false;


        // ========================================================
        // 2. PHẢI TÌM ĐƯỢC MẶT TRÊN
        // ========================================================

        if (!hitData.heightHitFound)
            return false;


        if (hitData.hitInfo.collider == null)
            return false;


        if (hitData.heightHitInfo.collider == null)
            return false;


        // ========================================================
        // 3. CHỈ CHO LAYER OBSTACLE
        // ========================================================

        int obstacleLayer =
            LayerMask.NameToLayer("Obstacle");


        if (obstacleLayer == -1)
        {
            Debug.LogError(
                "Không tồn tại Layer 'Obstacle'."
            );

            return false;
        }


        int frontLayer =
            hitData.hitInfo.collider.gameObject.layer;


        int topLayer =
            hitData.heightHitInfo.collider.gameObject.layer;


        if (frontLayer != obstacleLayer)
        {
            Debug.LogWarning(
                "PARKOUR ACTION REJECTED: Front collider " +
                hitData.hitInfo.collider.name +
                " có Layer = " +
                LayerMask.LayerToName(frontLayer)
            );

            return false;
        }


        if (topLayer != obstacleLayer)
        {
            Debug.LogWarning(
                "PARKOUR ACTION REJECTED: Top collider " +
                hitData.heightHitInfo.collider.name +
                " có Layer = " +
                LayerMask.LayerToName(topLayer)
            );

            return false;
        }


        // ========================================================
        // 4. KIỂM TRA TAG NẾU CÓ
        // ========================================================

        if (!string.IsNullOrEmpty(barrierTag))
        {
            if (!hitData.hitInfo.collider.CompareTag(barrierTag))
            {
                return false;
            }
        }


        // ========================================================
        // 5. KIỂM TRA CHIỀU CAO
        // ========================================================

        float checkHeight =
            hitData.heightHitInfo.point.y -
            player.position.y;


        Debug.Log(
            "Parkour CheckHeight: " +
            checkHeight
        );


        if (checkHeight < minimumHeight ||
            checkHeight > maximumHeight)
        {
            return false;
        }


        // ========================================================
        // 6. XOAY PLAYER VỀ OBSTACLE
        // ========================================================

        if (lookAtObstacle)
        {
            Vector3 lookDirection =
                -hitData.hitInfo.normal;


            lookDirection.y = 0f;


            if (lookDirection.sqrMagnitude > 0.001f)
            {
                RequiredRotation =
                    Quaternion.LookRotation(
                        lookDirection.normalized
                    );
            }
        }


        // ========================================================
        // 7. MATCH TARGET
        // ========================================================

        if (allowTargetMatching)
        {
            CharacterController controller =
                player.GetComponent<CharacterController>();


            float radius =
                controller != null
                    ? controller.radius
                    : 0.3f;


            ComparePosition =
                hitData.heightHitInfo.point +
                hitData.hitInfo.normal * radius;
        }


        return true;
    }


    // ============================================================
    // PUBLIC PROPERTIES
    // ============================================================

    public string AnimationName
        => animationName;


    public bool LookAtObstacle
        => lookAtObstacle;


    public float ParkourActionDelay
        => parkourActionDelay;


    public bool AllowTargetMatching
        => allowTargetMatching;


    public AvatarTarget CompareBodyPart
        => compareBodyPart;


    public float CompareStartTime
        => compareStartTime;


    public float CompareEndTime
        => compareEndTime;


    public Vector3 ComparePositionWeight
        => comparePositionWeight;
}