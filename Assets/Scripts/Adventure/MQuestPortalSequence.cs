using System;
using System.Collections;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>Scene m only: victory return -> enemy death/dissolve -> portal strike -> persistent unlock.</summary>
[DefaultExecutionOrder(-800)]
public sealed class MQuestPortalSequence : MonoBehaviour
{
    [Serializable] public sealed class EnemyShot
    {
        public WorldInteraction enemy;
        public AnimationClip deathClip;
        public Material dissolveMaterial;
        public Transform cameraPose;
    }
    public QuestPortalDefinition definition;
    public MapGameSetup mapSetup;
    public EnemyShot[] enemies = Array.Empty<EnemyShot>();
    public CinemachineCamera cinematicCamera;
    public Transform portalCameraPose, lightningAnchor, darkAnchor, npcDestination;
    public WorldInteraction transition, npc;
    public GameObject zapPrefab, darkPrefab, cinematicBars;
    public Image transitionFade;
    [Min(0)] public float cameraCutFadeSeconds = .18f;
    [Min(.1f)] public float portalStrikeSeconds = 1.8f;
    [Min(0)] public float unlockedRevealSeconds = 1.2f;
    [Min(0)] public float returnBlendSeconds = .6f;
    public Vector3 zapRotationEuler;
    [Tooltip("Kích thước sét đỏ, tính theo scale gốc của prefab.")]
    public Vector3 zapScale = Vector3.one * 3f;
    public Vector3 darkScale = Vector3.one;
    [Range(0, .95f)] public float dissolveStart = .35f;
    public bool persistProgress = true;
    CampaignSession session;
    GameObject dark, zap;
    bool ownsPresentation, ownsBlend;
    CinemachineCore.GetBlendOverrideDelegate previousBlend;
    PlayableGraph deathGraph;
    MapDeathDissolve dissolve;

    IEnumerator Start()
    {
        if (gameObject.scene.name != "m" || definition == null) yield break;
        while (isActiveAndEnabled && (CampaignSession.Instance?.Data == null || mapSetup == null || !mapSetup.StartupReady)) yield return null;
        session = CampaignSession.Instance;
        definition.Progress(session.Data, true);
        ApplyUnlockedState();
        while (isActiveAndEnabled && session != null && session.OnMap)
        {
            var state = definition.Progress(session.Data, false);
            string id = state?.defeated.Find(key => !state.presented.Contains(key));
            if (string.IsNullOrEmpty(id)) yield break;
            var shot = enemies.FirstOrDefault(e => e != null && e.enemy != null && e.enemy.Id == id);
            if (shot == null) { Debug.LogError("[Main Quest] Missing enemy presentation reference: " + id, this); yield break; }
            // Acquire before Loading disappears so gameplay cannot run for a frame between loading and the cutscene.
            while (isActiveAndEnabled && !session.TryBeginPresentation(this)) yield return null;
            ownsPresentation = true;
            while (LoadingScreen.IsLoading) yield return null;
            // Other map views finish their initialization after Loading closes.
            yield return null;
            yield return Present(shot);
        }
    }
    public Animator EnsureVisual(EnemyShot shot)
    {
        var point = shot.enemy; var patrol = point.GetComponent<EnemyEncounterBehaviour>();
        if (patrol != null) patrol.enabled = false;
        var agent = point.GetComponent<NavMeshAgent>(); if (agent != null) agent.enabled = false;
        point.gameObject.SetActive(true);
        var animator = patrol?.mapAnimator != null ? patrol.mapAnimator : point.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            var prefab = point.enemyPrefabs.FirstOrDefault(p => p != null);
            if (prefab == null) return null;
            var holder = new GameObject("Quest Death Model"); holder.SetActive(false); holder.transform.SetParent(point.transform, false);
            var model = Instantiate(prefab, holder.transform); model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            EnemyEncounterBehaviour.PrepareModel(model.gameObject);
            animator = model.animator != null ? model.animator : model.GetComponentInChildren<Animator>(true);
            model.gameObject.SetActive(true); holder.SetActive(true); if (patrol != null) patrol.mapAnimator = animator;
        }
        if (animator != null)
        {
            EnemyEncounterBehaviour.PrepareModel(animator.gameObject);
            if (patrol != null) patrol.mapAnimator = animator;
            animator.enabled = true; animator.applyRootMotion = false; animator.fireEvents = false; animator.speed = 1; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        foreach (var skin in point.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
        return animator;
    }
    public void BeginDeath(EnemyShot shot)
    {
        EndDeath(); var animator = EnsureVisual(shot);
        if (animator == null || shot.deathClip == null) throw new InvalidOperationException("Main Quest enemy is missing Animator/Die clip.");
        animator.Rebind(); animator.Update(0);
        deathGraph = PlayableGraph.Create("Main Quest map enemy death"); deathGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var clip = AnimationClipPlayable.Create(deathGraph, shot.deathClip); clip.SetApplyFootIK(false); clip.SetApplyPlayableIK(false);
        var output = AnimationPlayableOutput.Create(deathGraph, "Die", animator); output.SetSourcePlayable(clip); deathGraph.Play();
        dissolve = new MapDeathDissolve(animator.transform, shot.dissolveMaterial);
        SampleDeath(shot, 0);
    }
    public void SampleDeath(EnemyShot shot, float normalized)
    {
        if (deathGraph.IsValid())
        { deathGraph.GetRootPlayable(0).SetTime(Mathf.Clamp01(normalized) * shot.deathClip.length); deathGraph.Evaluate(0); }
        dissolve?.Sample(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(dissolveStart, 1, normalized)));
    }
    public void EndDeath()
    {
        if (deathGraph.IsValid()) deathGraph.Destroy(); dissolve?.Dispose(); dissolve = null;
    }
    IEnumerator Present(EnemyShot shot)
    {
        try
        {
            previousBlend = CinemachineCore.GetBlendOverride; CinemachineCore.GetBlendOverride = Blend; ownsBlend = true;
            if (cinematicBars != null) cinematicBars.SetActive(true);
            SetCamera(shot.cameraPose); cinematicCamera.Priority = 150; cinematicCamera.enabled = true;
            BeginDeath(shot);
            var unit = shot.enemy.enemyPrefabs.FirstOrDefault(p => p != null);
            GameAudio.Play(unit?.audioProfile?.die ?? GameAudio.Settings.enemy?.die, shot.enemy.transform.position, GameAudioBus.Voice);
            float elapsed = 0, duration = Mathf.Max(.1f, shot.deathClip.length);
            while (elapsed < duration) { SampleDeath(shot, elapsed / duration); elapsed += Time.unscaledDeltaTime; yield return null; }
            SampleDeath(shot, 1); yield return new WaitForSecondsRealtime(.2f);
            shot.enemy.gameObject.SetActive(false); EndDeath();
            yield return Fade(0, 1); SetCamera(portalCameraPose); yield return Fade(1, 0);
            zap = SpawnLightning();
            yield return new WaitForSecondsRealtime(portalStrikeSeconds);
            if (zap != null) Destroy(zap); zap = null;
            var state = definition.Progress(session.Data, true); if (!state.presented.Contains(shot.enemy.Id)) state.presented.Add(shot.enemy.Id);
            ApplyUnlockedState(); Save();
            if (definition.IsUnlocked(session.Data)) yield return new WaitForSecondsRealtime(unlockedRevealSeconds);
            session.MapPlayer?.MCC?.SnapCameraTarget(); cinematicCamera.enabled = false;
            if (returnBlendSeconds > 0) yield return new WaitForSecondsRealtime(returnBlendSeconds);
        }
        finally { Restore(); }
    }
    public void ApplyUnlockedState()
    {
        bool open = definition != null && definition.IsUnlocked(session?.Data ?? CampaignSession.Instance?.Data);
        if (transition != null) { transition.enabled = open; if (open && !transition.gameObject.activeSelf) transition.gameObject.SetActive(true); }
        if (open && npc != null && npcDestination != null)
        {
            var agent = npc.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(npcDestination.position);
            else npc.transform.position = npcDestination.position;
            npc.transform.rotation = npcDestination.rotation;
        }
        if (open && dark == null && darkPrefab != null && darkAnchor != null)
        {
            dark = Instantiate(darkPrefab, darkAnchor.position, darkAnchor.rotation, transform); dark.transform.localScale = darkScale;
            ConfigureVfx(dark, true); foreach (var particles in dark.GetComponentsInChildren<ParticleSystem>(true)) particles.Play();
        }
        var owner = session ?? CampaignSession.Instance;
        if (owner != null && owner.OnMap) owner.RefreshWorld(gameObject.scene);
    }
    static void ConfigureVfx(GameObject root, bool loop)
    {
        foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
        { var main = particles.main; main.loop = loop; main.useUnscaledTime = true; main.stopAction = ParticleSystemStopAction.None; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
        foreach (var light in root.GetComponentsInChildren<Light>(true)) light.shadows = LightShadows.None;
        GameAudioSourceBus.Bind(root);
    }
    GameObject SpawnLightning()
    {
        if (zapPrefab == null || lightningAnchor == null) return null;
        var effect = Instantiate(zapPrefab, lightningAnchor.position, Quaternion.Euler(zapRotationEuler)); effect.transform.localScale = zapScale;
        ConfigureVfx(effect, false); foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true)) particles.Play();
        return effect;
    }
    void SetCamera(Transform pose) { if (pose != null && cinematicCamera != null) cinematicCamera.transform.SetPositionAndRotation(pose.position, pose.rotation); }
    IEnumerator Fade(float from, float to)
    {
        if (transitionFade == null || cameraCutFadeSeconds <= 0) yield break;
        for (float t = 0; t < cameraCutFadeSeconds; t += Time.unscaledDeltaTime)
        { var color = transitionFade.color; color.a = Mathf.Lerp(from, to, t / cameraCutFadeSeconds); transitionFade.color = color; yield return null; }
        var final = transitionFade.color; final.a = to; transitionFade.color = final;
    }
    CinemachineBlendDefinition Blend(ICinemachineCamera from, ICinemachineCamera to, CinemachineBlendDefinition original, UnityEngine.Object owner)
    {
        if (ReferenceEquals(to, cinematicCamera)) return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
        if (ReferenceEquals(from, cinematicCamera)) return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, returnBlendSeconds);
        return previousBlend != null ? previousBlend(from, to, original, owner) : original;
    }
    void Save() { if (persistProgress && Application.isPlaying && session?.Data != null) session.Save(false); }
    public void Restore()
    {
        EndDeath(); if (zap != null) Destroy(zap); zap = null;
        if (cinematicCamera != null) { cinematicCamera.enabled = false; cinematicCamera.Priority = -100; }
        if (cinematicBars != null) cinematicBars.SetActive(false);
        if (transitionFade != null) { var color = transitionFade.color; color.a = 0; transitionFade.color = color; }
        if (ownsBlend) { if (CinemachineCore.GetBlendOverride == Blend) CinemachineCore.GetBlendOverride = previousBlend; else CinemachineCore.GetBlendOverride -= Blend; ownsBlend = false; }
        if (ownsPresentation) session?.EndPresentation(this); ownsPresentation = false;
    }
    void OnDisable() { StopAllCoroutines(); Restore(); }
}
