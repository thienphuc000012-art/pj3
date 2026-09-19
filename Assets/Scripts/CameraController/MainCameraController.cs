using UnityEngine;
using Unity.Cinemachine;

[DefaultExecutionOrder(-100)]
public class MainCameraController : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]

    [SerializeField]
    private CinemachineCamera vcam;

    [Tooltip("Object Player chính - object có CharacterController")]
    [SerializeField]
    private Transform playerTarget;

    [Tooltip("CameraTarget dùng cho Cinemachine")]
    [SerializeField]
    private Transform cameraTarget;


    // ============================================================
    // CAMERA TARGET POSITION
    // ============================================================

    [Header("Camera Target Position")]

    [Tooltip("Độ cao CameraTarget tính từ chân Player")]
    [SerializeField]
    private float targetHeight = 1.4f;

    [Tooltip("Độ mượt theo chiều ngang")]
    [SerializeField]
    private float horizontalSmoothTime = 0.025f;

    [Tooltip("Độ mượt khi Player lên xuống cầu thang")]
    [SerializeField]
    private float verticalSmoothTime = 0.12f;

    [Tooltip("Giới hạn tốc độ CameraTarget đuổi theo Player theo chiều Y")]
    [SerializeField]
    private float maxVerticalFollowSpeed = 15f;


    // ============================================================
    // MOUSE LOOK
    // ============================================================

    [Header("Mouse Look")]

    [SerializeField]
    private float mouseSensitivityX = 2.5f;

    [SerializeField]
    private float mouseSensitivityY = 2.0f;


    // ============================================================
    // VERTICAL ROTATION
    // ============================================================

    [Header("Vertical Camera Limit")]

    [SerializeField]
    private float minPitch = -35f;

    [SerializeField]
    private float maxPitch = 65f;


    // ============================================================
    // ROTATION SMOOTHING
    // ============================================================

    [Header("Camera Rotation Smoothing")]

    [SerializeField]
    private bool smoothCameraRotation = true;

    [SerializeField]
    private float rotationSmoothSpeed = 20f;


    // ============================================================
    // CURSOR
    // ============================================================

    [Header("Cursor")]

    [SerializeField]
    private bool lockCursorOnStart = true;


    // ============================================================
    // PUBLIC VALUES
    // ============================================================

    public float rotationY { get; private set; }


    // ============================================================
    // PRIVATE VARIABLES
    // ============================================================

    private float yaw;
    private float pitch;

    private bool cursorLocked;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;

    private Quaternion targetRotation;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        if (playerTarget == null)
        {
            Debug.LogError(
                "MainCameraController: Chưa gán Player Target!"
            );

            enabled = false;
            return;
        }


        if (cameraTarget == null)
        {
            Debug.LogError(
                "MainCameraController: Chưa gán Camera Target!"
            );

            enabled = false;
            return;
        }


        // --------------------------------------------------------
        // RẤT QUAN TRỌNG
        //
        // CameraTarget KHÔNG được tiếp tục là con của Player.
        //
        // Nếu là child:
        //
        // Player bước lên bậc
        //      ↓
        // CameraTarget bị kéo lên ngay lập tức
        //      ↓
        // camera giật.
        //
        // Detach nó ra khỏi Player để chúng ta tự smoothing position.
        // --------------------------------------------------------

        cameraTarget.SetParent(null, true);
    }


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        // Đặt CameraTarget đúng vị trí ngay frame đầu.
        cameraTarget.position =
            playerTarget.position +
            Vector3.up * targetHeight;


        // Lấy rotation hiện tại.
        Vector3 startRotation =
            cameraTarget.eulerAngles;

        yaw = startRotation.y;
        pitch = startRotation.x;


        if (pitch > 180f)
        {
            pitch -= 360f;
        }


        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );


        targetRotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );


        cameraTarget.rotation =
            targetRotation;


        rotationY = yaw;


        if (lockCursorOnStart)
        {
            LockCursor();
        }
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (CampaignSession.InputBlocked) return;
        HandleCursor();


        if (!cursorLocked)
        {
            return;
        }


        HandleMouseInput();
    }


    // ============================================================
    // LATE UPDATE
    // ============================================================

    private void LateUpdate()
    {
        if (playerTarget == null ||
            cameraTarget == null)
        {
            return;
        }


        // --------------------------------------------------------
        // 1. SMOOTH POSITION
        // --------------------------------------------------------

        UpdateCameraTargetPosition();


        // --------------------------------------------------------
        // 2. SMOOTH ROTATION
        // --------------------------------------------------------

        UpdateCameraTargetRotation();
    }


    // ============================================================
    // POSITION FOLLOW
    // ============================================================

    private void UpdateCameraTargetPosition()
    {
        Vector3 playerPosition =
            playerTarget.position;


        Vector3 currentPosition =
            cameraTarget.position;


        // ========================================================
        // HORIZONTAL
        // ========================================================

        Vector3 currentHorizontal =
            new Vector3(
                currentPosition.x,
                0f,
                currentPosition.z
            );


        Vector3 targetHorizontal =
            new Vector3(
                playerPosition.x,
                0f,
                playerPosition.z
            );


        Vector3 newHorizontal;


        if (horizontalSmoothTime <= 0f)
        {
            newHorizontal = targetHorizontal;
        }
        else
        {
            newHorizontal =
                Vector3.SmoothDamp(
                    currentHorizontal,
                    targetHorizontal,
                    ref horizontalVelocity,
                    horizontalSmoothTime
                );
        }


        // ========================================================
        // VERTICAL
        //
        // Đây là phần quan trọng nhất để chống giật cầu thang.
        // ========================================================

        float desiredY =
            playerPosition.y +
            targetHeight;


        float newY;


        if (verticalSmoothTime <= 0f)
        {
            newY = desiredY;
        }
        else
        {
            newY =
                Mathf.SmoothDamp(
                    currentPosition.y,
                    desiredY,
                    ref verticalVelocity,
                    verticalSmoothTime,
                    maxVerticalFollowSpeed
                );
        }


        // ========================================================
        // APPLY
        // ========================================================

        cameraTarget.position =
            new Vector3(
                newHorizontal.x,
                newY,
                newHorizontal.z
            );
    }


    // ============================================================
    // ROTATION
    // ============================================================

    private void UpdateCameraTargetRotation()
    {
        targetRotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );


        if (smoothCameraRotation)
        {
            // Exponential smoothing ổn định hơn theo framerate.
            float smoothFactor =
                1f -
                Mathf.Exp(
                    -rotationSmoothSpeed *
                    Time.deltaTime
                );


            cameraTarget.rotation =
                Quaternion.Slerp(
                    cameraTarget.rotation,
                    targetRotation,
                    smoothFactor
                );
        }
        else
        {
            cameraTarget.rotation =
                targetRotation;
        }


        // Dùng góc camera THỰC TẾ sau smoothing
        // cho Player movement.
        rotationY =
            cameraTarget.eulerAngles.y;
    }


    // ============================================================
    // MOUSE INPUT
    // ============================================================

    private void HandleMouseInput()
    {
        float mouseX =
            Input.GetAxisRaw("Mouse X");

        float mouseY =
            Input.GetAxisRaw("Mouse Y");


        yaw +=
            mouseX *
            mouseSensitivityX;


        pitch -=
            mouseY *
            mouseSensitivityY;


        pitch =
            Mathf.Clamp(
                pitch,
                minPitch,
                maxPitch
            );


        // Không để yaw tăng vô hạn.
        if (yaw > 360f)
        {
            yaw -= 360f;
        }

        else if (yaw < -360f)
        {
            yaw += 360f;
        }
    }


    // ============================================================
    // CURSOR
    // ============================================================

    private void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }


        if (!cursorLocked &&
            Input.GetMouseButtonDown(0))
        {
            LockCursor();
        }
    }


    private void LockCursor()
    {
        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;

        cursorLocked = true;
    }


    private void UnlockCursor()
    {
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        cursorLocked = false;
    }


    // ============================================================
    // PLAYER MOVEMENT
    //
    // PlayerScript của bạn đang dùng:
    //
    // requiredMoveDir = MCC.flatRotation * movementInput;
    //
    // ============================================================

    public Quaternion flatRotation
    {
        get
        {
            return Quaternion.Euler(
                0f,
                rotationY,
                0f
            );
        }
    }


    // ============================================================
    // PUBLIC FUNCTIONS
    // ============================================================

    public void SetCameraRotation(
        float newYaw,
        float newPitch
    )
    {
        yaw = newYaw;


        pitch =
            Mathf.Clamp(
                newPitch,
                minPitch,
                maxPitch
            );


        targetRotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );


        if (cameraTarget != null)
        {
            cameraTarget.rotation =
                targetRotation;
        }


        rotationY = yaw;
    }


    public void SetCursorLocked(bool locked)
    {
        if (locked)
        {
            LockCursor();
        }
        else
        {
            UnlockCursor();
        }
    }


    public float GetYaw()
    {
        return yaw;
    }


    public float GetPitch()
    {
        return pitch;
    }


    // ============================================================
    // SNAP TARGET
    //
    // Dùng sau teleport / respawn để camera không từ từ bay tới.
    // ============================================================

    public void SnapCameraTarget()
    {
        if (playerTarget == null ||
            cameraTarget == null)
        {
            return;
        }


        horizontalVelocity =
            Vector3.zero;

        verticalVelocity = 0f;


        cameraTarget.position =
            playerTarget.position +
            Vector3.up * targetHeight;


        cameraTarget.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );
    }
}
