using UnityEngine;


[CreateAssetMenu(menuName = "farkour menu/create new farkour action")]
public class NewFakourAction : ScriptableObject
{
    [Header("checking obstacle height")]

    [SerializeField] string animationName;
    [SerializeField] string barrierTag;
    [SerializeField] float minimumHeight;
    [SerializeField] float maximumHeight;

    [Header("rotating player towards obstacle")]
    [SerializeField] bool lookAtObstacle;
    [SerializeField] float parkourActionDelay;
    public Quaternion RequiredRotation { get; set; }

    [Header("target matching")]
    [SerializeField] bool allowTargetMatching = true;
    [SerializeField] AvatarTarget compareBodyPart;
    [SerializeField] float compareStartTime;
    [SerializeField] float compareEndTime;
    [SerializeField] Vector3 comparePositionWeight = new Vector3(0, 1, 0);

    public Vector3 ComparePosition { get; set; }

    public bool checkIfAvailable(ObstacleInfo hitData, Transform player)
    {
        if(!string.IsNullOrEmpty(barrierTag) && hitData.hitInfo.transform.tag != barrierTag)
        { return false; }

        float checkHeight = hitData.heightHitInfo.point.y - player.position.y;
        Debug.Log("CheckHeight: " + checkHeight);

        if (checkHeight < minimumHeight || checkHeight > maximumHeight)
        { return false; }
       
        if(lookAtObstacle)
        {
            RequiredRotation = Quaternion.LookRotation(-hitData.hitInfo.normal);
        }
        if (allowTargetMatching)
        {
            float radius = player.GetComponent<CharacterController>()?.radius ?? 0.3f;
            ComparePosition = hitData.heightHitInfo.point + hitData.hitInfo.normal * radius;
        }

        return true;
        
    }

    public string AnimationName => animationName;
    public bool LookAtObstacle => lookAtObstacle; 

    public float ParkourActionDelay => parkourActionDelay;
    public bool AllowTargetMatching => allowTargetMatching;
    public AvatarTarget CompareBodyPart => compareBodyPart;
    public float CompareStartTime => compareStartTime;
    public float CompareEndTime => compareEndTime;

    public Vector3 ComparePositionWeight => comparePositionWeight;
}
