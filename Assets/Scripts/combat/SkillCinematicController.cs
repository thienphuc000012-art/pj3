using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Gives a skill Timeline temporary ownership of the battle Brain.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SkillCinematicController : MonoBehaviour
{
    private const int ShotPriority = -100;
    private SkillCinematicRig activeRig;
    private PlayableDirector activeDirector;
    private Transform staging;
    private Transform attacker;
    private Transform target;
    private Vector3 attackerSlot;
    private Vector3 targetSlot;

    public bool IsPlaying => activeDirector != null;
    public event Action Stopped;

    public bool Play(SkillCinematicDefinition definition, CinemachineBrain brain, Transform attacker,
        Transform target, Vector3 attackerBattlePosition, Vector3 targetBattlePosition,
        ImpactCameraController impact = null)
    {
        Stop();
        if (definition == null || brain == null || attacker == null || target == null || !isActiveAndEnabled)
            return false;

        var prefab = definition.rigPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"'{definition.name}' has no Skill Cinematic Rig prefab.", definition);
            return false;
        }

        // Bind under an inactive parent so Play On Awake and the shot cameras cannot run first.
        if (staging == null)
        {
            var holder = new GameObject("Skill Timeline Staging");
            holder.SetActive(false);
            staging = holder.transform;
            staging.SetParent(transform, false);
        }

        var rig = Instantiate(prefab, staging);
        var director = rig.GetComponent<PlayableDirector>();
        if (director == null || rig.GetComponentInChildren<Camera>(true) != null ||
            !(director.playableAsset is TimelineAsset timeline) ||
            !BindCameraTracks(director, timeline, brain, rig.transform))
        {
            Debug.LogWarning($"'{definition.name}' needs a Timeline with Cinemachine Shot clips bound to cameras inside its rig prefab. Remove any Unity Camera components from the rig.", definition);
            Destroy(rig.gameObject);
            return false;
        }

        // The Brain chooses the normal battle camera between clips. Timeline shots override that selection.
        OutputChannels channel = (brain.ChannelMask & OutputChannels.Channel08) != 0
            ? OutputChannels.Channel08 : brain.ChannelMask;
        foreach (var shotCamera in rig.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
        {
            shotCamera.Priority = ShotPriority;
            shotCamera.OutputChannel = channel;
            if (impact != null && shotCamera is CinemachineCamera camera)
                impact.Attach(camera);
        }

        this.attacker = attacker;
        this.target = target;
        attackerSlot = attackerBattlePosition;
        targetSlot = targetBattlePosition;
        activeRig = rig;
        activeDirector = director;

        // X points right, Y up and Z toward the target from the attacker's slot.
        Vector3 forward = Vector3.ProjectOnPlane(targetSlot - attackerSlot, Vector3.up);
        if (forward.sqrMagnitude < .0001f)
            forward = Vector3.ProjectOnPlane(attacker.forward, Vector3.up);
        if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;

        var rigObject = rig.gameObject;
        rigObject.SetActive(false);
        rig.transform.SetParent(transform, false);
        rig.transform.SetPositionAndRotation((attackerSlot + targetSlot) * .5f,
            Quaternion.LookRotation(forward, Vector3.up));
        rig.UpdateAnchors(attacker, target, attackerSlot, targetSlot);

        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;
        director.initialTime = 0d;
        director.stopped += OnDirectorStopped;
        rigObject.SetActive(true);
        if (activeDirector != director) return false;
        director.enabled = true;
        director.time = 0d;
        director.Play();
        return IsPlaying;
    }

    private static bool BindCameraTracks(PlayableDirector director, TimelineAsset timeline,
        CinemachineBrain brain, Transform rigRoot)
    {
        bool hasShot = false;
        foreach (var outputTrack in timeline.GetOutputTracks())
        {
            if (!(outputTrack is CinemachineTrack cameraTrack) || cameraTrack.mutedInHierarchy) continue;
            director.SetGenericBinding(cameraTrack, brain);
            foreach (var clip in cameraTrack.GetClips())
            {
                if (!(clip.asset is CinemachineShot shot)) continue;
                var shotCamera = shot.VirtualCamera.Resolve(director);
                if (shotCamera == null || !shotCamera.transform.IsChildOf(rigRoot))
                    return false;
                hasShot = true;
            }
        }
        return hasShot;
    }

    private void LateUpdate()
    {
        if (activeRig != null)
            activeRig.UpdateAnchors(attacker, target, attackerSlot, targetSlot);
    }

    public void Stop() => ReleaseRig(true);

    private void OnDirectorStopped(PlayableDirector director)
    {
        if (director == activeDirector) ReleaseRig(false);
    }

    private void ReleaseRig(bool stopDirector)
    {
        var director = activeDirector;
        var rig = activeRig;
        activeDirector = null;
        activeRig = null;
        attacker = null;
        target = null;

        if (director != null)
        {
            director.stopped -= OnDirectorStopped;
            if (stopDirector) director.Stop();
        }
        if (rig != null)
        {
            // Natural stop raises the event before the graph finishes teardown.
            // Defer deactivation in that case so OnDisable cannot reenter Stop.
            if (stopDirector) rig.gameObject.SetActive(false);
            Destroy(rig.gameObject);
        }
        if (director != null || rig != null) Stopped?.Invoke();
    }

    private void OnDisable() => Stop();
}
