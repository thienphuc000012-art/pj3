using UnityEngine;
using Unity.Cinemachine;

public class MainCameraController : MonoBehaviour
{
    public CinemachineCamera vcam;
    public float rotationY;


    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {

        var state = vcam.State;

        var rotation = state.RawOrientation;

        var euler = rotation.eulerAngles;

        rotationY = euler.y;

        var roundedRotationY = Mathf.RoundToInt(rotationY);
    }

    public Quaternion flatRotation => Quaternion.Euler(0f, rotationY, 0f);
}
