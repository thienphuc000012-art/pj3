using UnityEngine;
using Unity.Cinemachine;

// Presentation only: checkpoint, healing and menu actions remain in CampaignSession.
[DefaultExecutionOrder(10000)]
public sealed class RestPointPresentation : MonoBehaviour
{
    public GameObject flames;
    public Transform cameraPose;
    public Transform cameraLookAt;
    public string sittingState = "Base Layer.Sitting";
    [Range(25, 75)] public float fieldOfView = 48;
    public Vector3 cameraOffset = new Vector3(2.5f, 1.5f, 2.5f);
    Animator animator;
    Camera restCamera;
    CinemachineBrain brain;
    bool brainEnabled, active;
    AnimatorUpdateMode updateMode;
    AnimatorCullingMode cullingMode;
    int oldState;
    float oldTime, oldFov;
    Vector3 oldCameraPosition;
    Quaternion oldCameraRotation, oldPlayerRotation;
    Transform player;

    void Awake() { if (flames != null) flames.SetActive(false); }
    public void Begin(PlayerScript mapPlayer)
    {
        if (active || mapPlayer == null) return;
        active = true;
        player = mapPlayer.transform;
        oldPlayerRotation = player.rotation;
        Vector3 facing = transform.position - player.position; facing.y = 0;
        if (facing.sqrMagnitude > .01f) player.rotation = Quaternion.LookRotation(facing);
        animator = mapPlayer.animator;
        if (animator != null)
        {
            updateMode = animator.updateMode; cullingMode = animator.cullingMode;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            oldState = state.fullPathHash; oldTime = state.normalizedTime;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.HasState(0, Animator.StringToHash(sittingState))) animator.Play(sittingState, 0, 0);
            else Debug.LogWarning("Rest point: Animator is missing " + sittingState, mapPlayer);
        }
        if (flames != null)
        {
            foreach (var ps in flames.GetComponentsInChildren<ParticleSystem>(true))
            { var main = ps.main; main.useUnscaledTime = true; }
            flames.SetActive(true);
            foreach (var ps in flames.GetComponentsInChildren<ParticleSystem>()) ps.Play();
        }
        restCamera = Camera.main;
        if (restCamera != null)
        {
            oldCameraPosition = restCamera.transform.position; oldCameraRotation = restCamera.transform.rotation; oldFov = restCamera.fieldOfView;
            brain = restCamera.GetComponent<CinemachineBrain>();
            if (brain != null) { brainEnabled = brain.enabled; brain.enabled = false; }
        }
    }
    void LateUpdate()
    {
        if (!active) return;
        var session = CampaignSession.Instance;
        if (session == null || session.RestPoint == null || session.RestPoint.gameObject != gameObject || session.Menu != AdventureMenu.Rest)
        { End(); return; }
        if (restCamera == null || player == null) return;
        Vector3 position = cameraPose != null ? cameraPose.position : player.position + player.rotation * cameraOffset;
        Vector3 target = cameraLookAt != null ? cameraLookAt.position : Vector3.Lerp(player.position, transform.position, .35f) + Vector3.up * .7f;
        restCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        restCamera.fieldOfView = fieldOfView;
    }
    public void End()
    {
        if (!active) return;
        active = false;
        if (flames != null) flames.SetActive(false);
        if (player != null) player.rotation = oldPlayerRotation;
        if (animator != null)
        {
            animator.updateMode = updateMode; animator.cullingMode = cullingMode;
            if (oldState != 0) animator.Play(oldState, 0, oldTime);
        }
        if (restCamera != null)
        { restCamera.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation); restCamera.fieldOfView = oldFov; }
        if (brain != null) brain.enabled = brainEnabled;
    }
    void OnDisable() { End(); }
}
