using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public enum WorldInteractionKind { Pickup, Chest, Discovery, RestPoint, Encounter, MapTransition, DialogueNPC }

public class WorldInteraction : MonoBehaviour
{
    public string persistentId;
    [HideInInspector] public string savedWorldId;
    [Tooltip("Optional short ID used by quest assets to point the compass at this location.")]
    public string questMarkerId;
    public string displayName = "Tương tác";
    public WorldInteractionKind kind;
    [Header("NPC Dialogue")]
    public AdventureStoryDialogue dialogue;
    [Header("NPC Presentation")]
    [Tooltip("Optional 3D character prefab displayed at this dialogue point.")]
    public GameObject dialogueNpcModelPrefab;
    public Vector3 dialogueNpcModelPosition;
    public Vector3 dialogueNpcModelRotation;
    public Vector3 dialogueNpcModelScale = Vector3.one;
    [Tooltip("Fallback face height above the NPC model pivot when no humanoid Head bone is available.")]
    [Min(0.1f)] public float dialogueFaceHeight = 1.6f;
    [Tooltip("Animator state to play while this NPC is speaking, for example Base Layer.Talking.")]
    public string dialogueTalkAnimationState = "Base Layer.Talking";
    [Header("Map Transition")]
    [Tooltip("Tên scene đích phải có trong Campaign Config > Additional Map Scenes và Build Settings.")]
    public string targetMapScene;
    [Tooltip("Vị trí spawn của player trong scene đích (tọa độ world của scene đích).")]
    public Vector3 targetMapPosition;
    public Vector3 targetMapEuler;
    [Min(0.5f)] public float range = 2.5f;
    [Min(0)] public int experience = 25;
    public List<ItemStack> rewards = new List<ItemStack>();
    [Header("Pickup 3D Model")]
    public GameObject pickupModelPrefab;
    public Vector3 pickupModelPosition = Vector3.zero;
    public Vector3 pickupModelRotation = Vector3.zero;
    public Vector3 pickupModelScale = Vector3.one;

    [Header("Chest Opening")]
    [Tooltip("Clip open trong thư mục Free Treasure Chest. Clip được phát trên Animator của model rương.")]
    public AnimationClip chestOpenAnimation;
    public string chestPlayerAnimationState = "Base Layer.openchest";
    [Range(0f, 1f), Tooltip("Mốc trong animation openchest của player để rương bắt đầu mở.")]
    public float chestOpenMoment = 0.5f;
    [Tooltip("Dùng Animation Event AnimEvent_OpenChest trên clip openchest thay cho mốc phần trăm.")]
    public bool chestUseAnimationEvent;
    [Tooltip("Chỉ cho phép tương tác ở phía trước rương theo hướng trục Z xanh dương (forward) của Transform.")]
    public bool requireFrontAccess = true;
    [Range(10f, 170f), Tooltip("Góc nửa hình nón phía trước được phép mở rương.")]
    public float chestFrontAccessHalfAngle = 70f;
    [Min(0.1f)] public float chestOpenDuration = 1.2f;
    [Tooltip("Đường dẫn nắp rương bên trong model. Gold Chest mặc định dùng Top_Lod.")]
    public string chestLidPath = "Top_Lod";
    [Tooltip("Bật để Gold Chest vẫn mở đúng nắp khi clip open của asset chưa có curve.")]
    public bool chestAnimateLidFallback = true;
    public Vector3 chestLidOpenRotation = new Vector3(-105f, 0f, 0f);

    // Fallback for custom chest controllers authored before the clip field existed.
    public string chestOpenState = "Base Layer.open";
    public string chestClosedState = "Base Layer.closed";
    public string pickupAnimationState = "Base Layer.pickupitem";
    [Range(0f, 1f), Tooltip("Mốc tay nhặt item trong animation: 0.5 là giữa clip.")]
    public float pickupMoment = 0.5f;
    [Tooltip("Dùng event AnimEvent_PickupItem trên clip thay cho mốc phần trăm.")]
    public bool pickupUseAnimationEvent;
    [Min(0.1f)] public float pickupAnimationTimeout = 10f;
    [HideInInspector] public GameObject pickupModelInstance;
    [HideInInspector] public GameObject pickupModelSource;
    [HideInInspector] public GameObject dialogueNpcModelInstance;
    [HideInInspector] public GameObject dialogueNpcModelSource;
    private Quaternion dialogueNpcOriginalWorldRotation;
    private bool dialogueNpcOriginalRotationCaptured;

    private PlayableGraph chestAnimationGraph;
    private AnimationClipPlayable chestAnimationPlayable;
    private Animator chestAnimator;
    private Transform chestLid;
    private Quaternion chestLidClosedRotation;
    private bool chestLidPoseCaptured;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (kind == WorldInteractionKind.DialogueNPC)
        {
            EnsureDialogueNpcModel();
            return;
        }
        if (kind != WorldInteractionKind.Pickup && kind != WorldInteractionKind.Chest) return;
        if (pickupModelInstance != null && pickupModelSource != pickupModelPrefab)
        {
            StopChestAnimation();
            pickupModelInstance.SetActive(false);
            Destroy(pickupModelInstance);
            pickupModelInstance = null;
            chestLid = null;
            chestLidPoseCaptured = false;
        }
        if (pickupModelInstance == null && pickupModelPrefab != null &&
            pickupModelPrefab.GetComponentInChildren<WorldInteraction>(true) == null)
        {
            pickupModelInstance = Instantiate(pickupModelPrefab, transform);
            pickupModelSource = pickupModelPrefab;
            pickupModelInstance.name = kind == WorldInteractionKind.Chest ? "Chest Model" : "Pickup Model";
            chestLid = null;
            chestLidPoseCaptured = false;
        }
        ApplyPickupModelTransform();
        if (kind == WorldInteractionKind.Chest)
        {
            if (IsConsumed)
            {
                gameObject.SetActive(false);
                return;
            }
            StopChestAnimation();
            SetChestOpenProgress(0f);
        }
    }

    void OnDisable() => StopChestAnimation();
    void OnDestroy() => StopChestAnimation();

    private void EnsureDialogueNpcModel()
    {
        if (dialogueNpcModelInstance != null && dialogueNpcModelSource != dialogueNpcModelPrefab)
        {
            Destroy(dialogueNpcModelInstance);
            dialogueNpcModelInstance = null;
            dialogueNpcOriginalRotationCaptured = false;
        }

        if (dialogueNpcModelInstance == null && dialogueNpcModelPrefab != null &&
            dialogueNpcModelPrefab.GetComponentInChildren<WorldInteraction>(true) == null)
        {
            dialogueNpcModelInstance = Instantiate(dialogueNpcModelPrefab, transform);
            dialogueNpcModelInstance.name = "Dialogue NPC Model";
            dialogueNpcModelSource = dialogueNpcModelPrefab;
        }

        ApplyDialogueNpcModelTransform();
    }

    public void ApplyDialogueNpcModelTransform()
    {
        if (dialogueNpcModelInstance == null) return;
        dialogueNpcModelInstance.transform.localPosition = dialogueNpcModelPosition;
        dialogueNpcModelInstance.transform.localRotation = Quaternion.Euler(dialogueNpcModelRotation);
        dialogueNpcModelInstance.transform.localScale = dialogueNpcModelScale;
    }

    public Animator DialogueNpcAnimator
    {
        get
        {
            var root = dialogueNpcModelInstance != null ? dialogueNpcModelInstance.transform : transform;
            return root != null ? root.GetComponentInChildren<Animator>(true) : null;
        }
    }

    public Transform DialogueNpcRoot => dialogueNpcModelInstance != null ? dialogueNpcModelInstance.transform : transform;

    public Transform DialogueNpcHead
    {
        get
        {
            var animator = DialogueNpcAnimator;
            return animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        }
    }

    public Vector3 DialogueNpcFacePosition
    {
        get
        {
            var head = DialogueNpcHead;
            if (head != null) return head.position;
            var root = DialogueNpcRoot;
            return root != null ? root.TransformPoint(Vector3.up * dialogueFaceHeight) : transform.position + Vector3.up * dialogueFaceHeight;
        }
    }

    public void FaceDialoguePlayer(Vector3 playerPosition)
    {
        var root = DialogueNpcRoot;
        if (root == null) return;
        if (!dialogueNpcOriginalRotationCaptured)
        {
            dialogueNpcOriginalWorldRotation = root.rotation;
            dialogueNpcOriginalRotationCaptured = true;
        }
        Vector3 direction = playerPosition - root.position;
        direction.y = 0f;
        Vector3 forward = root.forward;
        forward.y = 0f;
        if (direction.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f)
            root.rotation = Quaternion.FromToRotation(forward.normalized, direction.normalized) * root.rotation;
    }

    public void RestoreDialogueNpcFacing()
    {
        if (dialogueNpcOriginalRotationCaptured && DialogueNpcRoot != null)
            DialogueNpcRoot.rotation = dialogueNpcOriginalWorldRotation;
        dialogueNpcOriginalRotationCaptured = false;
    }

    public float PlayChestOpen()
    {
        StopChestAnimation();
        SetChestOpenProgress(0f);

        float duration = Mathf.Max(0.1f, chestOpenDuration);
        var animator = FindChestAnimator();
        if (chestOpenAnimation != null)
        {
            duration = Mathf.Max(duration, chestOpenAnimation.length);
            if (animator != null) PlayChestClip(animator, 0f);
            else Debug.LogWarning("Chest: model rương chưa có Animator để phát clip open.", this);
            return duration;
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            int stateHash = Animator.StringToHash(chestOpenState);
            if (animator.HasState(0, stateHash))
            {
                animator.Play(stateHash, 0, 0f);
                animator.Update(0f);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                return Mathf.Max(duration, state.length / Mathf.Max(0.01f, animator.speed * Mathf.Abs(state.speed * state.speedMultiplier)));
            }
        }

        Debug.LogWarning("Chest: chưa gán clip open và cũng không có state open trên model rương.", this);
        return duration;
    }

    public void SetChestOpenProgress(float normalizedProgress)
    {
        if (!chestAnimateLidFallback) return;
        var lid = FindChestLid();
        if (lid == null) return;
        if (!chestLidPoseCaptured)
        {
            chestLidClosedRotation = lid.localRotation;
            chestLidPoseCaptured = true;
        }

        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedProgress));
        lid.localRotation = chestLidClosedRotation * Quaternion.Euler(chestLidOpenRotation * t);
    }

    public void FinishChestOpen()
    {
        if (chestAnimationGraph.IsValid())
        {
            chestAnimationPlayable.SetTime(chestOpenAnimation != null ? chestOpenAnimation.length : 0d);
            chestAnimationPlayable.SetSpeed(0d);
            chestAnimationGraph.Evaluate(0f);
        }
        SetChestOpenProgress(1f);
    }

    private Animator FindChestAnimator()
    {
        if (chestAnimator != null) return chestAnimator;
        var root = pickupModelInstance != null ? pickupModelInstance.transform : transform;
        chestAnimator = root.GetComponentInChildren<Animator>(true);
        return chestAnimator;
    }

    private Transform FindChestLid()
    {
        if (chestLid != null) return chestLid;
        var root = pickupModelInstance != null ? pickupModelInstance.transform : transform;
        if (root == null || string.IsNullOrWhiteSpace(chestLidPath)) return null;
        chestLid = root.Find(chestLidPath);
        if (chestLid != null) return chestLid;

        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == chestLidPath) return chestLid = child;
        return null;
    }

    private void PlayChestClip(Animator animator, float startTime)
    {
        if (chestOpenAnimation == null || animator == null) return;
        chestAnimator = animator;
        chestAnimationGraph = PlayableGraph.Create(name + " Chest Open");
        var output = AnimationPlayableOutput.Create(chestAnimationGraph, "Chest Open", animator);
        chestAnimationPlayable = AnimationClipPlayable.Create(chestAnimationGraph, chestOpenAnimation);
        chestAnimationPlayable.SetTime(Mathf.Max(0f, startTime));
        output.SetSourcePlayable(chestAnimationPlayable);
        chestAnimationGraph.Play();
    }

    private void StopChestAnimation()
    {
        if (chestAnimationGraph.IsValid()) chestAnimationGraph.Destroy();
        chestAnimator = null;
    }

    public void ApplyPickupModelTransform()
    {
        if (pickupModelInstance == null) return;
        pickupModelInstance.transform.localPosition = pickupModelPosition;
        pickupModelInstance.transform.localRotation = Quaternion.Euler(pickupModelRotation);
        pickupModelInstance.transform.localScale = pickupModelScale;
    }
    public List<BattleUnit> enemyPrefabs = new List<BattleUnit>();
    [Header("Combat Scene Setup")]
    [Tooltip("ID của BattleEncounterSetup trong scene combat. Ví dụ: goblin_01, boss_dragon_01.")]
    public string combatSetupId = "default";
    public bool IsConsumed => CampaignSession.Instance != null && CampaignSession.Instance.Data != null && CampaignSession.Instance.Data.collected.Contains(Id);
    // A duplicated prefab must not share collection/defeat state with another instance.
    public string Id => string.IsNullOrEmpty(savedWorldId) ? gameObject.scene.name + ":" + persistentId + ":" + HierarchyPath(transform) : savedWorldId;
    private static string HierarchyPath(Transform t) => t.parent == null ? t.name + "#" + t.GetSiblingIndex() : HierarchyPath(t.parent) + "/" + t.name + "#" + t.GetSiblingIndex();
    void OnValidate() { if (string.IsNullOrEmpty(persistentId)) persistentId = Guid.NewGuid().ToString("N"); }

    public bool IsPlayerInFront(Transform player)
    {
        if (!requireFrontAccess || player == null) return true;
        Vector3 directionToPlayer = player.position - transform.position;
        directionToPlayer.y = 0f;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (directionToPlayer.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f) return false;
        float minDot = Mathf.Cos(Mathf.Clamp(chestFrontAccessHalfAngle, 10f, 170f) * Mathf.Deg2Rad);
        return Vector3.Dot(forward.normalized, directionToPlayer.normalized) >= minDot;
    }

    public void Interact()
    {
        var session = CampaignSession.Instance;
        if (session == null || session.Busy || IsConsumed) return;
        if (kind == WorldInteractionKind.RestPoint) { session.OpenRest(this); return; }
        if (kind == WorldInteractionKind.Encounter) { session.BeginEncounter(this); return; }
        if (kind == WorldInteractionKind.MapTransition) { session.TravelToMap(this); return; }
        if (kind == WorldInteractionKind.DialogueNPC) { if (session.IsNear(this)) session.BeginDialogue(this); return; }
        if (!session.IsNear(this)) return;
        if (kind == WorldInteractionKind.Pickup || kind == WorldInteractionKind.Chest) { session.BeginItemPickup(this); return; }
        CompleteCollection();
    }

    public void CompleteCollection(bool requireRange = true)
    {
        var session = CampaignSession.Instance;
        if (session == null || IsConsumed || requireRange && !session.IsNear(this)) return;
        foreach (var reward in rewards) session.AddItem(reward.id, reward.count);
        session.GainXP(experience);
        session.Data.collected.Add(Id);
        session.Notify(displayName + " • +" + experience + " EXP");
        session.Save();
        gameObject.SetActive(false);
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == WorldInteractionKind.Encounter ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
