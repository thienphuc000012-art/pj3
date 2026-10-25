using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public class CampaignSession : MonoBehaviour
{
    public static CampaignSession Instance { get; private set; }
    public static bool InputBlocked => MapChunkStreamer.MovementBlocked || (Instance != null && (Instance.Busy || Instance.Menu != AdventureMenu.None));
    public CampaignConfig Config { get; private set; }
    public CampaignSave Data { get; private set; }
    public PlayerScript MapPlayer { get; private set; }
    public bool Busy { get; private set; }
    public AdventureMenu Menu { get; private set; }
    public WorldInteraction RestPoint { get; private set; }
    public WorldInteraction Nearest { get; private set; }
    public string Message { get; private set; }
    public float MessageUntil { get; private set; }
    public bool InEncounter => encounter != null;
    public int ResultExperience => encounter?.xp ?? 0;
    public List<ItemStack> ResultRewards => encounter?.rewards ?? new List<ItemStack>();
    public string CurrentMapScene => Data != null && !string.IsNullOrEmpty(Data.currentMapScene)
        ? Data.currentMapScene
        : Config != null ? Config.mapScene : null;
    public bool OnMap => Config != null && Config.IsMapScene(SceneManager.GetActiveScene().name);
    public bool CanStartEncounter => OnMap && !Busy && Menu == AdventureMenu.None && Time.unscaledTime >= encounterAllowedAt;
    public bool CanUpgrade => OnMap && RestPoint != null && IsNear(RestPoint) && !Busy;
    public void BeginItemPickup(WorldInteraction point)
    {
        if (Busy || Menu != AdventureMenu.None || !OnMap || MapPlayer == null || point == null ||
            (point.kind != WorldInteractionKind.Pickup && point.kind != WorldInteractionKind.Chest) || point.IsConsumed || !IsNear(point)) return;
        Busy = true;
        Nearest = null;
        StartCoroutine(PlayItemPickup(point));
    }

    private IEnumerator PlayItemPickup(WorldInteraction point)
    {
        var player = MapPlayer;
        var animator = player != null ? player.animator : null;
        int oldState = 0;
        var oldCulling = animator != null ? animator.cullingMode : AnimatorCullingMode.AlwaysAnimate;
        bool oldApplyRootMotion = animator != null && animator.applyRootMotion;
        bool played = false;
        bool collected = false;
        bool chest = point.kind == WorldInteractionKind.Chest;
        bool opening = false;
        float chestStartedAt = 0f;
        float chestFinishedAt = 0f;
        bool useAnimationEvent = chest ? point.chestUseAnimationEvent : point.pickupUseAnimationEvent;
        float interactionMoment = chest ? point.chestOpenMoment : point.pickupMoment;
        PickupAnimationEvents receiver = null;
        Action collect = () =>
        {
            if (collected || !OnMap || MapPlayer != player || point == null || !point.isActiveAndEnabled || point.IsConsumed) return;
            if (chest)
            {
                if (!opening)
                {
                    opening = true;
                    chestStartedAt = Time.time;
                    chestFinishedAt = chestStartedAt + point.PlayChestOpen();
                }
                return;
            }
            point.CompleteCollection();
            collected = point.IsConsumed;
        };
        try
        {
            if (chest && player != null) player.FaceTowards(point.transform.position);
            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                string stateName = chest ? point.chestPlayerAnimationState : point.pickupAnimationState;
                if (string.IsNullOrWhiteSpace(stateName)) stateName = chest ? "Base Layer.openchest" : "Base Layer.pickupitem";
                int stateHash = Animator.StringToHash(stateName);
                if (!animator.HasState(0, stateHash)) stateHash = Animator.StringToHash(chest ? "openchest" : "pickupitem");
                if (animator.HasState(0, stateHash))
                {
                    oldState = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    if (chest) animator.applyRootMotion = false;
                    animator.SetFloat("movementvalue", 0f);
                    receiver = animator.GetComponent<PickupAnimationEvents>();
                    if (receiver == null) receiver = animator.gameObject.AddComponent<PickupAnimationEvents>();
                    receiver.OnPickupMoment = () =>
                    {
                        if (chest || !point.pickupUseAnimationEvent) return;
                        var active = animator.GetCurrentAnimatorStateInfo(0);
                        if (active.fullPathHash == stateHash || active.shortNameHash == stateHash) collect();
                    };
                    receiver.OnChestOpenMoment = () =>
                    {
                        if (!chest || !point.chestUseAnimationEvent) return;
                        var active = animator.GetCurrentAnimatorStateInfo(0);
                        if (active.fullPathHash == stateHash || active.shortNameHash == stateHash) collect();
                    };
                    animator.Play(stateHash, 0, 0f);
                    played = true;
                    float deadline = Time.unscaledTime + Mathf.Max(0.1f, point.pickupAnimationTimeout);
                    bool entered = false;
                    yield return null;
                    while (point != null && (collected || point.isActiveAndEnabled) && player != null && animator != null && animator.isActiveAndEnabled && OnMap)
                    {
                        var state = animator.GetCurrentAnimatorStateInfo(0);
                        bool picking = state.fullPathHash == stateHash || state.shortNameHash == stateHash;
                        if (picking)
                        {
                            entered = true;
                            if (!useAnimationEvent && state.normalizedTime >= Mathf.Clamp01(interactionMoment)) collect();
                            if (state.normalizedTime >= 1f) break;
                        }
                        else if (entered) break;
                        if (Time.unscaledTime >= deadline) break;
                        yield return null;
                    }
                }
                else Debug.LogWarning("Pickup/Chest: không tìm thấy state tương tác trong Animator của player. Hãy lưu PlayerController2.", point);
            }
            if (!played) collect(); // Preserve pickups on characters without a pickup animation.
            if (chest && opening)
            {
                while (Time.time < chestFinishedAt && OnMap && point != null && point.isActiveAndEnabled && MapPlayer == player)
                {
                    point.SetChestOpenProgress(Mathf.InverseLerp(chestStartedAt, chestFinishedAt, Time.time));
                    yield return null;
                }
                if (OnMap && point != null && point.isActiveAndEnabled && MapPlayer == player)
                {
                    point.FinishChestOpen();
                    point.CompleteCollection(requireRange: false);
                    collected = point.IsConsumed;
                }
            }
            if (!collected && point != null && useAnimationEvent)
                Debug.LogWarning(chest
                    ? "Chest: chưa nhận event AnimEvent_OpenChest; rương vẫn ở map để mở lại."
                    : "Item Pickup: chưa nhận event AnimEvent_PickupItem; item vẫn ở map để nhặt lại.", point);
        }
        finally
        {
            if (receiver != null) { receiver.OnPickupMoment = null; receiver.OnChestOpenMoment = null; }
            if (animator != null && played)
            {
                animator.cullingMode = oldCulling;
                if (chest) animator.applyRootMotion = oldApplyRootMotion;
                int locomotion = Animator.StringToHash("Base Layer.Basic Movement");
                if (animator.HasState(0, locomotion)) animator.CrossFadeInFixedTime(locomotion, 0.1f);
                else if (oldState != 0 && animator.HasState(0, oldState)) animator.CrossFadeInFixedTime(oldState, 0.1f);
            }
            Busy = false;
        }
    }
    private WorldInteraction[] worldPoints = new WorldInteraction[0];
    private EncounterData encounter;
    private float previousTimeScale = 1;
    private CursorLockMode previousCursor;
    private bool previousCursorVisible;
    private string SavePath => Path.Combine(Application.persistentDataPath, "adventure-save-v1.json");
    private float encounterAllowedAt;
    private class EncounterData
    {
        public string id;
        public int xp;
        public List<ItemStack> rewards;
        public List<BattleUnit> enemies;
        public string combatSetupId;
        public bool playerInitiated;
    }

    private sealed class EncounterAnimationProgress
    {
        public Animator animator;
        public string stateName;
        public int stateHash;
        public int layer;
        public float startedAt;
        public float fallbackDuration;
        public bool entered;
        public bool finished;
    }

    public static CampaignSession Ensure(CampaignConfig config, bool startNewGame = false)
    {
        if (Instance != null)
        {
            if (config != null) Instance.Config = config;
            return Instance;
        }
        var obj = new GameObject("Campaign Session");
        var session = obj.AddComponent<CampaignSession>();
        session.Config = config;
        session.LoadOrCreate(startNewGame);
        obj.AddComponent<AdventureUI>();
        return session;
    }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    void OnDestroy() { if (Instance == this) { Instance = null; Time.timeScale = previousTimeScale; } }
    void LoadOrCreate(bool startNewGame = false)
    {
        if (startNewGame && File.Exists(SavePath))
        {
            try
            {
                string backup = SavePath + ".before-new-game-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".bak";
                File.Copy(SavePath, backup);
                Debug.Log("Đã sao lưu tiến trình trước khi chơi mới: " + backup);
            }
            catch (Exception e)
            {
                startNewGame = false;
                Debug.LogError("Không thể sao lưu tiến trình; tiếp tục bản lưu hiện tại: " + e.Message);
            }
        }
        if (!startNewGame && File.Exists(SavePath))
        {
            try
            {
                var loaded = JsonUtility.FromJson<CampaignSave>(File.ReadAllText(SavePath));
                if (loaded != null && (loaded.version == 1 || loaded.version == 2) && loaded.party != null && loaded.party.Count > 0 &&
                    loaded.inventory != null && loaded.collected != null &&
                    loaded.party.All(p => p != null && p.prefabIndex >= 0 && p.prefabIndex < Config.startingParty.Count && Config.startingParty[p.prefabIndex] != null))
                    Data = loaded;
            }
            catch (Exception e) { Debug.LogWarning("Could not read adventure save: " + e.Message); }
        }
        if (Data != null) { Data.MigrateSkillUnlocks();
            if (Data.unlockedRestPoints == null) Data.unlockedRestPoints = new List<UnlockedRestPoint>();
            return; }
        Data = new CampaignSave();
        for (int i = 0; i < Config.startingParty.Count; i++)
            if (Config.startingParty[i] != null)
                Data.party.Add(new PartyMemberProgress { prefabIndex = i, hp = Config.startingParty[i].maxHP });
    }
    public void BindMap(PlayerScript player)
    {
        MapPlayer = player;
        Busy = false;
        string sceneName = SceneManager.GetActiveScene().name;
        bool positionBelongsToThisMap = Data.hasPosition &&
            (string.IsNullOrEmpty(Data.currentMapScene) || Data.currentMapScene == sceneName);
        if (positionBelongsToThisMap) Teleport(Data.position, Data.rotation);
        else
        {
            Data.currentMapScene = sceneName;
            Data.hasPosition = false;
            CapturePosition();
        }
        if (!Data.hasCheckpoint)
        {
            Data.checkpoint = Data.position;
            Data.checkpointRotation = Data.rotation;
            Data.checkpointMapScene = sceneName;
            Data.hasCheckpoint = true;
        }
        encounterAllowedAt = Time.unscaledTime + 2;
        if (!string.IsNullOrEmpty(Message)) MessageUntil = Time.unscaledTime + 5;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    public void RefreshWorld()
    {
        worldPoints = FindObjectsByType<WorldInteraction>(FindObjectsSortMode.None);
        foreach (var point in worldPoints)
        {
            if (point.IsConsumed) point.gameObject.SetActive(false);
            if (point.kind == WorldInteractionKind.RestPoint)
            {
                var known = Data.unlockedRestPoints.Find(x => x != null && x.id == point.Id);
                if (known != null) known.displayName = point.displayName;
            }
        }
    }
    public BattleUnit Template(PartyMemberProgress member) => Config.startingParty[member.prefabIndex];
    public int MaxHP(PartyMemberProgress member) => Template(member).maxHP + member.hpBonus;
    public bool IsNear(WorldInteraction point)
    {
        if (MapPlayer == null || point == null || !point.gameObject.activeInHierarchy) return false;
        var patrol = point.GetComponent<EnemyEncounterBehaviour>();
        if (point.kind == WorldInteractionKind.Encounter && patrol != null && patrol.enabled)
            return patrol.CanInteract(MapPlayer.transform, point.range);
        if (Vector3.Distance(MapPlayer.transform.position, point.transform.position) > point.range) return false;
        return point.kind != WorldInteractionKind.Chest || point.IsPlayerInFront(MapPlayer.transform);
    }
    void Update()
    {
        if (!OnMap || MapPlayer == null || Busy || MapChunkStreamer.MovementBlocked) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { if (!GetComponent<AdventureUI>().HandleBack()) SetMenu(AdventureMenu.None); return; }
        if (Input.GetKeyDown(KeyCode.P)) SetMenu(Menu == AdventureMenu.None ? AdventureMenu.Main : AdventureMenu.None);
        if (Menu != AdventureMenu.None) return;
        Nearest = null;
        float best = float.MaxValue;
        foreach (var point in worldPoints)
        {
            if (point == null || point.IsConsumed || !IsNear(point)) continue;
            if (point.kind == WorldInteractionKind.Discovery) { point.Interact(); continue; }
            float distance = (point.transform.position - MapPlayer.transform.position).sqrMagnitude;
            if (distance < best) { Nearest = point; best = distance; }
        }
        if (Nearest != null && (Input.GetKeyDown(KeyCode.E) || (Nearest.kind == WorldInteractionKind.Encounter && Input.GetMouseButtonDown(0))))
            Nearest.Interact();
    }
    public void SetMenu(AdventureMenu menu)
    {
        if (Busy || !OnMap) return;
        if (Menu == AdventureMenu.Rest && menu != AdventureMenu.Rest)
        {
            RestPoint?.GetComponent<RestPointPresentation>()?.End();
            RestPoint = null;
        }
        if (Menu == AdventureMenu.None && menu != AdventureMenu.None)
        {
            previousTimeScale = Time.timeScale;
            previousCursor = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Menu != AdventureMenu.None && menu == AdventureMenu.None)
        {
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursor;
            Cursor.visible = previousCursorVisible;
            RestPoint = null;
        }
        Menu = menu;
    }
    public void Notify(string message) { Message = message; MessageUntil = Time.unscaledTime + 5; }
    public void CapturePosition()
    {
        if (MapPlayer == null || !OnMap) return;
        Data.currentMapScene = SceneManager.GetActiveScene().name;
        Data.position = MapPlayer.transform.position;
        Data.rotation = MapPlayer.transform.rotation;
        Data.hasPosition = true;
    }
    void Teleport(Vector3 position, Quaternion rotation)
    {
        if (MapPlayer == null) return;
        var controller = MapPlayer.GetComponent<CharacterController>();
        bool enabledBefore = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        MapPlayer.transform.SetPositionAndRotation(position, rotation);
        if (controller != null) controller.enabled = enabledBefore;
    }
    public bool Save(bool capturePosition = true)
    {
        if (capturePosition) CapturePosition();
        try
        {
            string temp = SavePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(Data, true));
            if (File.Exists(SavePath)) File.Replace(temp, SavePath, SavePath + ".bak");
            else File.Move(temp, SavePath);
            return true;
        }
        catch (Exception e) { Debug.LogError("Adventure save failed: " + e.Message); Notify("Không lưu được game. Kiểm tra quyền ghi dữ liệu."); return false; }
    }
    public int Count(string id) => Data.Count(id);
    public void AddItem(string id, int amount)
    {
        if (amount <= 0 || Config.FindItem(id) == null) return;
        Data.AddItem(id, amount);
    }
    public bool Consume(string id, int amount)
    {
        return Data.Consume(id, amount);
    }
    public void GainXP(int amount) { foreach (var member in Data.party) member.GainXP(amount); }
    public void Swap(int from, int to)
    {
        if (!OnMap || Busy || from < 0 || to < 0 || from >= Data.party.Count || to >= Data.party.Count) return;
        var temp = Data.party[from]; Data.party[from] = Data.party[to]; Data.party[to] = temp;
        Save();
    }
    public void UseItem(string id, int memberIndex)
    {
        if (!OnMap || Busy || memberIndex < 0 || memberIndex >= Data.party.Count) return;
        var item = Config.FindItem(id);
        var member = Data.party[memberIndex];
        if (item == null || item.mapHeal <= 0 || member.hp <= 0 || member.hp >= MaxHP(member)) return;
        if (!Consume(id, 1)) return;
        member.hp = Mathf.Min(MaxHP(member), member.hp + item.mapHeal);
        Notify("Đã dùng " + item.displayName); Save();
    }
    public void OpenRest(WorldInteraction rest)
    {
        if (Busy || Menu != AdventureMenu.None || rest == null || rest.kind != WorldInteractionKind.RestPoint || !IsNear(rest)) return;
        var unlocked = Data.unlockedRestPoints.Find(x => x.id == rest.Id);
        if (unlocked == null) { unlocked = new UnlockedRestPoint { id = rest.Id }; Data.unlockedRestPoints.Add(unlocked); }
        unlocked.displayName = rest.displayName;
        unlocked.sceneName = SceneManager.GetActiveScene().name;
        // Record the player's reachable standing position, never the campfire's collider center.
        unlocked.position = MapPlayer.transform.position;
        unlocked.rotation = MapPlayer.transform.rotation;
        Save();
        RestPoint = rest;
        SetMenu(AdventureMenu.Rest);
        rest.GetComponent<RestPointPresentation>()?.Begin(MapPlayer);
    }
    public bool FastTravel(string id)
    {
        if (!CanUpgrade || Menu != AdventureMenu.Rest || LoadingScreen.IsLoading || RestPoint.Id == id) return false;
        var destination = Data.unlockedRestPoints.Find(x => x != null && x.id == id);
        if (destination == null) return false;
        string destinationScene = !string.IsNullOrEmpty(destination.sceneName)
            ? destination.sceneName
            : Config.mapScene;
        if (!Config.IsMapScene(destinationScene)) destinationScene = Config.mapScene;
        if (!Application.CanStreamedLevelBeLoaded(destinationScene) || !Application.CanStreamedLevelBeLoaded(LoadingScreen.SceneName))
        { Notify("Thiếu scene map hoặc Loading trong Build Settings."); return false; }
        var oldPosition = Data.position; var oldRotation = Data.rotation; var oldMap = Data.currentMapScene; bool hadPosition = Data.hasPosition;
        Data.currentMapScene = destinationScene;
        Data.position = destination.position; Data.rotation = destination.rotation; Data.hasPosition = true;
        if (!Save(false)) { Data.currentMapScene = oldMap; Data.position = oldPosition; Data.rotation = oldRotation; Data.hasPosition = hadPosition; return false; }
        SetMenu(AdventureMenu.None);
        if (!LoadingScreen.Load(destinationScene))
        { Data.currentMapScene = oldMap; Data.position = oldPosition; Data.rotation = oldRotation; Data.hasPosition = hadPosition; Save(false); Notify("Không thể dịch chuyển."); return false; }
        Busy = true; Nearest = null;
        Notify("Đã đến " + destination.displayName);
        return true;
    }

    public bool TravelToMap(WorldInteraction portal)
    {
        if (!OnMap || Busy || Menu != AdventureMenu.None || portal == null ||
            portal.kind != WorldInteractionKind.MapTransition || !IsNear(portal) || LoadingScreen.IsLoading)
            return false;

        string targetScene = portal.targetMapScene != null ? portal.targetMapScene.Trim() : string.Empty;
        if (string.IsNullOrEmpty(targetScene) || Config == null || !Config.IsMapScene(targetScene) ||
            !Application.CanStreamedLevelBeLoaded(targetScene) || !Application.CanStreamedLevelBeLoaded(LoadingScreen.SceneName))
        {
            Notify("Scene đích phải có trong Campaign Config, Additional Map Scenes và Build Settings.");
            return false;
        }

        CapturePosition();
        string previousMap = Data.currentMapScene;
        Vector3 previousPosition = Data.position;
        Quaternion previousRotation = Data.rotation;
        bool hadPosition = Data.hasPosition;
        Data.currentMapScene = targetScene;
        Data.position = portal.targetMapPosition;
        Data.rotation = Quaternion.Euler(portal.targetMapEuler);
        Data.hasPosition = true;
        if (!Save(false))
        {
            Data.currentMapScene = previousMap;
            Data.position = previousPosition;
            Data.rotation = previousRotation;
            Data.hasPosition = hadPosition;
            return false;
        }

        Nearest = null;
        if (!LoadingScreen.Load(targetScene))
        {
            Data.currentMapScene = previousMap;
            Data.position = previousPosition;
            Data.rotation = previousRotation;
            Data.hasPosition = hadPosition;
            Save(false);
            Notify("Không thể chuyển scene.");
            return false;
        }

        SetMenu(AdventureMenu.None);
        Busy = true;
        return true;
    }

    public void RestAndSave()
    {
        if (!CanUpgrade) return;
        CapturePosition();
        Data.checkpoint = Data.position; Data.checkpointRotation = Data.rotation;
        Data.checkpointMapScene = Data.currentMapScene; Data.hasCheckpoint = true;
        foreach (var p in Data.party) p.hp = MaxHP(p);
        if (Save()) Notify("Đã hồi đầy máu và lưu game.");
    }
    public bool ConfirmStats(int memberIndex, int[] points)
    {
        if (!CanUpgrade || memberIndex < 0 || memberIndex >= Data.party.Count) return false;
        var p = Data.party[memberIndex];
        if (!p.TryAllocateStats(points, Template(p).baseCrit)) return false;
        Save(); Notify("Đã áp dụng điểm thuộc tính."); return true;
    }
    public bool IsSkillUnlocked(PartyMemberProgress p, ActionData skill)
    {
        if (skill == null || !Template(p).characterSkills.Contains(skill)) return false;
        var rule = Config.SkillRule(skill);
        return p.Rank(skill) > 0 || (rule != null ? rule.initiallyUnlocked : Template(p).characterSkills.FirstOrDefault(x => x != null) == skill);
    }
    public int SkillCost(ActionData skill) => Mathf.Max(1, Config.SkillRule(skill)?.cost ?? 1);
    public ActionData[] Prerequisites(PartyMemberProgress p, ActionData skill)
    {
        return Config.UnlockPrerequisites(skill);
    }
    public string UnlockBlockedReason(PartyMemberProgress p, ActionData skill)
    {
        if (skill == null || !Template(p).characterSkills.Contains(skill)) return "Kỹ năng không thuộc nhân vật này.";
        if (IsSkillUnlocked(p, skill)) return "Đã học kỹ năng này. Đổi kỹ năng mang theo trong menu Party.";
        if (!CanUpgrade) return "Cần đứng tại điểm nghỉ để học kỹ năng.";
        var missing = Prerequisites(p, skill).Where(x => !IsSkillUnlocked(p, x)).ToArray();
        if (missing.Length > 0) return "Cần học trước: " + string.Join(", ", missing.Select(x => x.actionName));
        if (p.skillPoints < SkillCost(skill)) return "Cần " + SkillCost(skill) + " điểm; đang có " + p.skillPoints + ".";
        return "";
    }
    public bool CanUnlockSkill(PartyMemberProgress p, ActionData skill) => string.IsNullOrEmpty(UnlockBlockedReason(p, skill));
    public void UnlockSkill(int memberIndex, ActionData skill)
    {
        if (memberIndex < 0 || memberIndex >= Data.party.Count) return;
        var p = Data.party[memberIndex];
        var reason = UnlockBlockedReason(p, skill);
        if (!string.IsNullOrEmpty(reason)) { Notify(reason); return; }
        if (!p.TryUnlock(skill, SkillCost(skill))) return;
        EnsureLoadout(p); Save(); Notify("Đã mở khóa " + skill.actionName);
    }
    public int SkillSlots => Mathf.Max(1, Config.equippedSkillSlots);
    public ActionData[] LearnedSkills(PartyMemberProgress p) => Template(p).characterSkills.Where(x => x != null && IsSkillUnlocked(p, x)).Distinct().ToArray();
    public void EnsureLoadout(PartyMemberProgress p)
    {
        bool initializedNow = !p.loadoutInitialized;
        var known = LearnedSkills(p);
        if (!p.loadoutInitialized)
        {
            p.equippedSkills = known.Take(SkillSlots).Select(x => x.name).ToList(); p.loadoutInitialized = true;
        }
        if (p.equippedSkills == null) p.equippedSkills = new List<string>();
        while (p.equippedSkills.Count < SkillSlots) p.equippedSkills.Add("");
        var seen = new HashSet<string>();
        for (int i = 0; i < p.equippedSkills.Count; i++)
            if (i >= SkillSlots || !known.Any(x => x.name == p.equippedSkills[i]) || !seen.Add(p.equippedSkills[i])) p.equippedSkills[i] = "";
        if (p.FillEmptySkillSlots(known.Select(x => x.name).ToArray(), SkillSlots) || initializedNow) Save();
    }
    public void EquipSkill(int memberIndex, int slot, ActionData skill)
    {
        if (!OnMap || Busy || Menu != AdventureMenu.Party || memberIndex < 0 || memberIndex >= Data.party.Count) return;
        var p = Data.party[memberIndex]; EnsureLoadout(p);
        if (skill != null && !IsSkillUnlocked(p, skill)) return;
        if (p.TryEquip(slot, skill?.name, LearnedSkills(p).Select(x => x.name).ToArray(), SkillSlots)) Save();
    }
    public List<ActionData> EquippedSkills(PartyMemberProgress p)
    {
        EnsureLoadout(p); var known = LearnedSkills(p);
        return p.equippedSkills.Take(SkillSlots).Select(id => known.FirstOrDefault(x => x.name == id)).Where(x => x != null).ToList();
    }
    public bool CanCraft(CraftRecipe recipe)
    {
        return CanUpgrade && recipe != null && Config.recipes.Contains(recipe) && recipe.output != null && recipe.output.count > 0 &&
            Config.FindItem(recipe.output.id) != null && Data.HasIngredients(recipe);
    }
    public void Craft(CraftRecipe recipe)
    {
        if (!CanCraft(recipe)) return;
        Data.TryCraft(recipe); Notify("Đã chế tạo " + recipe.displayName); Save();
    }
    public void BeginEncounter(WorldInteraction point) => BeginEncounter(point, true);

    public void BeginEncounter(WorldInteraction point, bool playerInitiated)
    {
        if (Busy || Menu != AdventureMenu.None || !IsNear(point) || Time.unscaledTime < encounterAllowedAt || point.IsConsumed) return;
        if (point.enemyPrefabs.Count == 0 || point.enemyPrefabs.Any(x => x == null) || !Data.party.Any(p => p.hp > 0))
        { Notify("Cần party còn sống và prefab quái hợp lệ."); return; }
        if (!Application.CanStreamedLevelBeLoaded(Config.battleScene)) { Notify("Chưa thêm combattest vào Build Settings."); return; }
        CapturePosition();
        encounter = new EncounterData
        {
            id = point.Id,
            xp = point.experience,
            rewards = point.rewards.Select(x => new ItemStack(x.id, x.count)).ToList(),
            enemies = new List<BattleUnit>(point.enemyPrefabs),
            combatSetupId = point.combatSetupId,
            playerInitiated = playerInitiated
        };
        Busy = true;
        StartCoroutine(PlayEncounterTransition(point, playerInitiated));
    }

    private IEnumerator PlayEncounterTransition(WorldInteraction point, bool playerInitiated)
    {
        PlayerScript player = MapPlayer;
        Animator playerAnimator = player != null ? player.animator : null;
        EnemyEncounterBehaviour patrol = point != null ? point.GetComponent<EnemyEncounterBehaviour>() : null;
        Animator enemyAnimator = patrol != null ? patrol.mapAnimator : point != null ? point.GetComponentInChildren<Animator>(true) : null;
        Transform playerTransform = player != null ? player.transform : null;
        Transform enemyTransform = point != null ? point.transform : null;
        Vector3 playerStartPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
        Vector3 enemyStartPosition = enemyTransform != null ? enemyTransform.position : Vector3.zero;
        Quaternion playerStartRotation = playerTransform != null ? playerTransform.rotation : Quaternion.identity;
        Quaternion enemyStartRotation = enemyTransform != null ? enemyTransform.rotation : Quaternion.identity;
        int playerStartState = playerAnimator != null && playerAnimator.layerCount > 0 ? playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash : 0;
        float playerStartNormalizedTime = playerAnimator != null && playerAnimator.layerCount > 0 ? playerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f;
        int enemyStartState = enemyAnimator != null && enemyAnimator.layerCount > 0 ? enemyAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash : 0;
        float enemyStartNormalizedTime = enemyAnimator != null && enemyAnimator.layerCount > 0 ? enemyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f;
        AnimatorCullingMode playerCulling = playerAnimator != null ? playerAnimator.cullingMode : AnimatorCullingMode.AlwaysAnimate;
        AnimatorCullingMode enemyCulling = enemyAnimator != null ? enemyAnimator.cullingMode : AnimatorCullingMode.AlwaysAnimate;
        float playerAnimatorSpeed = playerAnimator != null ? playerAnimator.speed : 1f;
        float enemyAnimatorSpeed = enemyAnimator != null ? enemyAnimator.speed : 1f;
        float encounterPreviousTimeScale = Time.timeScale;
        bool loadingStarted = false;
        GameObject blackHole = null;
        EncounterAnimationProgress playerAction = null;
        EncounterAnimationProgress enemyAction = null;

        try
        {
            if (playerTransform != null && enemyTransform != null)
            {
                FaceMapActor(playerTransform, enemyStartPosition);
                FaceMapActor(enemyTransform, playerStartPosition);
            }
            if (playerAnimator != null) playerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (enemyAnimator != null) enemyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (playerInitiated)
            {
                playerAction = StartEncounterAnimation(playerAnimator, Config.mapPlayerAttackTrigger, Config.mapPlayerAttackState, .7f);
                enemyAction = StartEncounterAnimation(enemyAnimator, Config.mapEnemyHitTrigger, Config.mapEnemyHitState, .7f);
            }
            else
            {
                playerAction = StartEncounterAnimation(playerAnimator, Config.mapPlayerHitTrigger, Config.mapPlayerHitState, .7f);
                if (patrol != null)
                {
                    enemyAction = StartEncounterAnimation(patrol.mapAnimator, "", patrol.EncounterAttackState, .7f);
                }
                else enemyAction = new EncounterAnimationProgress { finished = true };
            }

            float animationDeadline = Time.unscaledTime + 3.5f;
            bool playerAnimationFinished = false;
            bool enemyAnimationFinished = false;
            while (Time.unscaledTime < animationDeadline && (!playerAnimationFinished || !enemyAnimationFinished))
            {
                playerAnimationFinished = UpdateEncounterAnimation(playerAction);
                enemyAnimationFinished = UpdateEncounterAnimation(enemyAction);
                if (playerAnimationFinished && enemyAnimationFinished) break;
                yield return null;
            }

            if (playerTransform == null || enemyTransform == null || !OnMap)
                yield break;

            // Hold the final frame of each one-shot reaction while the encounter VFX plays.
            if (playerAnimator != null) playerAnimator.speed = 0f;
            if (enemyAnimator != null) enemyAnimator.speed = 0f;
            Time.timeScale = 0f;

            Camera viewCamera = Camera.main;
            if (Config.encounterBlackHoleVfxPrefab != null)
            {
                if (viewCamera != null)
                {
                    const float portalBaseDiameter = 7.57f;
                    float cameraDistance = Mathf.Max(2.5f, viewCamera.nearClipPlane + .5f);
                    float screenHeight = viewCamera.orthographic
                        ? viewCamera.orthographicSize * 2f
                        : cameraDistance * 2f * Mathf.Tan(viewCamera.fieldOfView * .5f * Mathf.Deg2Rad);
                    float screenWidth = screenHeight * viewCamera.aspect;
                    float screenFillScale = Mathf.Max(screenHeight, screenWidth) * 1.15f / portalBaseDiameter;
                    blackHole = Instantiate(Config.encounterBlackHoleVfxPrefab, viewCamera.transform, false);
                    blackHole.transform.localPosition = Vector3.forward * cameraDistance;
                    blackHole.transform.localRotation = Quaternion.identity;
                    blackHole.transform.localScale = Vector3.one * Mathf.Max(.1f, Config.encounterVfxScale, screenFillScale);
                }
                else
                {
                    Vector3 midpoint = (playerTransform.position + enemyTransform.position) * .5f + Vector3.up * .75f;
                    blackHole = Instantiate(Config.encounterBlackHoleVfxPrefab, midpoint, Quaternion.identity);
                    blackHole.transform.localScale = Vector3.one * Mathf.Max(.1f, Config.encounterVfxScale);
                }
                blackHole.SetActive(false);
                PrepareEncounterVfxForUnscaledTime(blackHole);
                blackHole.SetActive(true);
                foreach (ParticleSystem particles in blackHole.GetComponentsInChildren<ParticleSystem>(true))
                    if (particles != null && !particles.isPlaying) particles.Play(true);
            }

            // The world remains frozen; only the camera-facing encounter VFX advances.
            float vfxDuration = Mathf.Max(0f, Config.encounterVfxDuration);
            if (vfxDuration > 0f) yield return new WaitForSecondsRealtime(vfxDuration);

            if (!LoadingScreen.Load(Config.battleScene))
            {
                encounter = null;
                Notify(LoadingScreen.Error);
                yield break;
            }
            loadingStarted = true;
        }
        finally
        {
            if (!loadingStarted)
            {
                if (blackHole != null) Destroy(blackHole);
                if (playerTransform != null)
                {
                    playerTransform.rotation = playerStartRotation;
                }
                if (enemyTransform != null)
                {
                    enemyTransform.rotation = enemyStartRotation;
                }
                if (playerAnimator != null && playerStartState != 0 && playerAnimator.HasState(0, playerStartState))
                    playerAnimator.Play(playerStartState, 0, playerStartNormalizedTime);
                if (enemyAnimator != null && enemyStartState != 0 && enemyAnimator.HasState(0, enemyStartState))
                    enemyAnimator.Play(enemyStartState, 0, enemyStartNormalizedTime);
                if (playerAnimator != null) playerAnimator.speed = playerAnimatorSpeed;
                if (enemyAnimator != null) enemyAnimator.speed = enemyAnimatorSpeed;
                if (playerAnimator != null) playerAnimator.cullingMode = playerCulling;
                if (enemyAnimator != null) enemyAnimator.cullingMode = enemyCulling;
                Time.timeScale = encounterPreviousTimeScale;
                if (OnMap)
                {
                    encounter = null;
                    Busy = false;
                }
            }
        }
    }

    private static void FaceMapActor(Transform actor, Vector3 target)
    {
        if (actor == null) return;
        Vector3 direction = target - actor.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > .001f) actor.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static void PrepareEncounterVfxForUnscaledTime(GameObject effect)
    {
        if (effect == null) return;

        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.useUnscaledTime = true;
            main.playOnAwake = false;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        foreach (Animator animator in effect.GetComponentsInChildren<Animator>(true))
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    private static EncounterAnimationProgress StartEncounterAnimation(Animator animator, string triggerName, string stateName, float fallbackDuration)
    {
        var progress = new EncounterAnimationProgress
        {
            animator = animator,
            stateName = string.IsNullOrWhiteSpace(stateName) ? "" : stateName.Substring(stateName.LastIndexOf('.') + 1),
            stateHash = string.IsNullOrWhiteSpace(stateName) ? 0 : Animator.StringToHash(stateName.Substring(stateName.LastIndexOf('.') + 1)),
            startedAt = Time.unscaledTime,
            fallbackDuration = fallbackDuration,
            finished = animator == null || !animator.isActiveAndEnabled
        };
        if (progress.finished) return progress;

        bool hasTrigger = !string.IsNullOrWhiteSpace(triggerName) && animator.parameters.Any(x => x.name == triggerName && x.type == AnimatorControllerParameterType.Trigger);
        if (TryFindAnimatorState(animator, progress.stateName, out _, out int reactionLayer))
        {
            progress.stateHash = Animator.StringToHash(progress.stateName);
            progress.layer = reactionLayer;
        }
        if (hasTrigger)
        {
            animator.ResetTrigger(triggerName);
            animator.SetTrigger(triggerName);
            return progress;
        }

        if (TryFindAnimatorState(animator, progress.stateName, out int stateHash, out int layer))
        {
            progress.stateHash = Animator.StringToHash(progress.stateName);
            progress.layer = layer;
            progress.stateName = animator.GetLayerName(layer) + "." + progress.stateName;
            animator.Play(stateHash, layer, 0f);
        }
        else progress.finished = true;
        return progress;
    }

    private static bool TryFindAnimatorState(Animator animator, string shortName, out int stateHash, out int layer)
    {
        stateHash = 0; layer = 0;
        if (animator == null || string.IsNullOrEmpty(shortName)) return false;
        for (int i = 0; i < animator.layerCount; i++)
        {
            int fullHash = Animator.StringToHash(animator.GetLayerName(i) + "." + shortName);
            int shortHash = Animator.StringToHash(shortName);
            if (animator.HasState(i, fullHash)) { stateHash = fullHash; layer = i; return true; }
            if (animator.HasState(i, shortHash)) { stateHash = shortHash; layer = i; return true; }
        }
        return false;
    }

    private static bool UpdateEncounterAnimation(EncounterAnimationProgress progress)
    {
        if (progress == null || progress.finished) return true;
        if (progress.animator == null || !progress.animator.isActiveAndEnabled) { progress.finished = true; return true; }
        float elapsed = Time.unscaledTime - progress.startedAt;
        int layer = Mathf.Clamp(progress.layer, 0, Mathf.Max(0, progress.animator.layerCount - 1));
        AnimatorStateInfo state = progress.animator.GetCurrentAnimatorStateInfo(layer);
        bool inExpectedState = !string.IsNullOrEmpty(progress.stateName) &&
            (state.IsName(progress.stateName) || state.shortNameHash == progress.stateHash);
        if (inExpectedState)
        {
            progress.entered = true;
            if (!progress.animator.IsInTransition(layer) && state.normalizedTime >= .98f) progress.finished = true;
        }
        else if (progress.entered && !progress.animator.IsInTransition(layer)) progress.finished = true;
        else if (!progress.entered && elapsed >= progress.fallbackDuration + .35f) progress.finished = true;
        if (elapsed >= 3.5f) progress.finished = true;
        return progress.finished;
    }
    public void PrepareBattle(CombatManager manager)
    {
        if (encounter == null) return;

        BattleEncounterSetup battleSetup = FindBattleEncounterSetup(encounter.combatSetupId, manager);
        manager.activeEncounterSetup = battleSetup;
        if (battleSetup != null) battleSetup.ApplyBattleMap();

        var oldPlayers = manager.playerParty.ToArray();
        var oldEnemies = manager.enemyParty.ToArray();
        // Include inactive test characters that are not in the manager's roster.
        foreach (var unit in FindObjectsByType<BattleUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (unit.gameObject.scene == manager.gameObject.scene) unit.gameObject.SetActive(false);
        manager.playerParty.Clear(); manager.enemyParty.Clear(); manager.allUnitsTimeline.Clear();
        manager.playerSlots = ExpandSlots(manager.playerSlots, Data.party.Count, manager.transform, "Party slot");

        manager.enemySlots = ExpandSlots(manager.enemySlots, encounter.enemies.Count, manager.transform, "Enemy slot");

        // Preset ghi vị trí spawn vào enemySlots trước khi spawn, vì
        // CombatManager.SetupPositions() sẽ tiếp tục lấy vị trí từ chính các slot này.
        // CombatManager tính melee position từ target; mỗi Enemy lấy offset riêng trong setup.
        ApplyEnemySpawnSetup(
            battleSetup,
            manager.enemySlots,
            encounter.enemies.Count
        );

        for (int i = 0; i < Data.party.Count; i++)
        {
            var member = Data.party[i];
            var unit = Instantiate(Template(member), manager.playerSlots[i].position, manager.playerSlots[i].rotation);
            unit.gameObject.SetActive(true); unit.isPlayer = true;
            if (battleSetup != null) battleSetup.ApplyStartingEffects(unit, true, i);
            unit.maxHP = MaxHP(member); unit.baseAtk += member.atkBonus; unit.baseDef += member.defBonus;
            unit.baseCrit += member.critBonus; unit.baseCritDamage += member.critDamageBonus;
            unit.SetPersistentHP(member.hp);
            unit.characterSkills = EquippedSkills(member);
            manager.playerParty.Add(unit);
        }
        for (int i = 0; i < encounter.enemies.Count; i++)
        {
            var unit = Instantiate(encounter.enemies[i], manager.enemySlots[i].position, manager.enemySlots[i].rotation);
            unit.gameObject.SetActive(true); unit.isPlayer = false; unit.SetPersistentHP(unit.maxHP);
            if (battleSetup != null) battleSetup.ApplyStartingEffects(unit, false, i);
            manager.enemyParty.Add(unit);
        }
        int initiativeBonus = Mathf.Max(0, Config != null ? Config.initiativeSpeedBonus : 100);
        if (initiativeBonus > 0)
        {
            List<BattleUnit> advantagedSide = encounter.playerInitiated ? manager.playerParty : manager.enemyParty;
            foreach (BattleUnit unit in advantagedSide)
                if (unit != null) unit.speed += initiativeBonus;
        }
        RebindCameras(oldPlayers, oldEnemies, manager);

        if (battleSetup != null && CameraManager.Instance != null)
            CameraManager.Instance.ApplyEncounterSetup(battleSetup, manager.enemyParty);

        RefreshBattleItems(manager);
        Busy = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    static BattleEncounterSetup FindBattleEncounterSetup(string setupId, CombatManager manager)
    {
        if (manager == null || string.IsNullOrWhiteSpace(setupId))
            return null;

        BattleEncounterSetup fallback = null;

        foreach (var setup in FindObjectsByType<BattleEncounterSetup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (setup == null || setup.gameObject.scene != manager.gameObject.scene)
                continue;

            if (fallback == null && setup.setupId == "default")
                fallback = setup;

            if (string.Equals(setup.setupId, setupId, StringComparison.Ordinal))
            {
                Debug.Log("[Combat Setup] Loaded preset: " + setup.setupId, setup);
                return setup;
            }
        }

        if (fallback != null)
        {
            Debug.LogWarning(
                "[Combat Setup] Không tìm thấy preset '" + setupId + "'. Dùng preset default.",
                fallback
            );
            return fallback;
        }

        Debug.LogWarning(
            "[Combat Setup] Không tìm thấy BattleEncounterSetup có ID '" + setupId + "' trong scene combat. Dùng enemySlots/camera mặc định."
        );
        return null;
    }

    static void ApplyEnemySpawnSetup(
        BattleEncounterSetup setup,
        Transform[] enemySlots,
        int enemyCount)
    {
        if (setup == null || enemySlots == null)
            return;

        for (int i = 0; i < enemyCount && i < enemySlots.Length; i++)
        {
            EnemyCombatSetup enemySetup = setup.GetEnemySetup(i);

            if (enemySetup == null ||
                enemySetup.spawnPoint == null ||
                enemySlots[i] == null)
            {
                continue;
            }

            enemySlots[i].SetPositionAndRotation(
                enemySetup.spawnPoint.position,
                enemySetup.spawnPoint.rotation
            );
        }
    }

    static Transform[] ExpandSlots(Transform[] slots, int count, Transform parent, string label)
    {
        var list = slots == null ? new List<Transform>() : new List<Transform>(slots);
        for (int i = 0; i < count; i++)
        {
            if (i < list.Count && list[i] != null) continue;
            var slot = new GameObject(label + " " + (i + 1)).transform; slot.SetParent(parent);
            slot.position = list.Count > 0 && list[0] != null ? list[0].position + Vector3.right * (i * 2.5f) : parent.position + Vector3.right * (i * 2.5f);
            slot.rotation = list.Count > 0 && list[0] != null ? list[0].rotation : Quaternion.identity;
            if (i < list.Count) list[i] = slot; else list.Add(slot);
        }
        return list.ToArray();
    }
    static void RebindCameras(BattleUnit[] oldPlayers, BattleUnit[] oldEnemies, CombatManager manager)
    {
        foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            cam.Follow = RemapTarget(cam.Follow, oldPlayers, manager.playerParty, oldEnemies, manager.enemyParty);
            cam.LookAt = RemapTarget(cam.LookAt, oldPlayers, manager.playerParty, oldEnemies, manager.enemyParty);
        }
        var cameras = CameraManager.Instance;
        if (cameras != null)
        {
            ExpandCameras(cameras.playerPartyCams, manager.playerParty);
            ExpandCameras(cameras.playerHitCams, manager.playerParty);
            ExpandCameras(cameras.playerTargetCams, manager.playerParty);
            ExpandCameras(cameras.playerMenuCams, manager.playerParty);
            ExpandCameras(cameras.playerActionCams, manager.playerParty);
            ExpandCameras(cameras.enemyTargetCams, manager.enemyParty);
        }
    }
    static void ExpandCameras(List<CinemachineCamera> cameras, List<BattleUnit> units)
    {
        if (cameras == null || cameras.Count == 0 || cameras[0] == null) return;
        while (cameras.Count < units.Count)
        {
            int index = cameras.Count;
            var camera = Instantiate(cameras[0], cameras[0].transform.parent);
            camera.name = cameras[0].name + " " + (index + 1);
            camera.transform.position += units[index].transform.position - units[0].transform.position;
            camera.Priority = 0;
            if (camera.Follow != null) camera.Follow = units[index].transform;
            if (camera.LookAt != null) camera.LookAt = units[index].transform;
            cameras.Add(camera);
        }
    }
    static Transform RemapTarget(Transform target, BattleUnit[] oldPlayers, List<BattleUnit> players, BattleUnit[] oldEnemies, List<BattleUnit> enemies)
    {
        if (target == null) return null;
        for (int team = 0; team < 2; team++)
        {
            var old = team == 0 ? oldPlayers : oldEnemies; var current = team == 0 ? players : enemies;
            for (int i = 0; i < old.Length && i < current.Count; i++)
            {
                if (old[i] == null || !target.IsChildOf(old[i].transform)) continue;
                var path = new List<string>(); var t = target;
                while (t != old[i].transform) { path.Insert(0, t.name); t = t.parent; }
                return path.Count == 0 ? current[i].transform : current[i].transform.Find(string.Join("/", path)) ?? current[i].transform;
            }
        }
        return target;
    }
    public void RefreshBattleItems(CombatManager manager)
    {
        manager.inventoryItems = Config.items.Where(x => x.combatAction != null && Count(x.id) > 0).Select(x => x.combatAction).Distinct().ToList();
    }
    public bool TryGetBattleItemCount(ActionData action, out int count)
    {
        count = 0;
        if (action == null || Config?.items == null) return false;
        var item = Config.items.Find(x => x != null && x.combatAction == action);
        if (item == null) return false;
        count = Data != null ? Data.Count(item.id) : 0;
        return true;
    }
    public bool ConsumeBattleItem(ActionData action)
    {
        if (encounter == null) return true;
        var item = Config.items.Find(x => x.combatAction == action);
        return item != null && Consume(item.id, 1);
    }
    public void FinishBattle(CombatManager manager, bool victory)
    {
        if (encounter == null || Busy) return;
        Busy = true;
        for (int i = 0; i < Data.party.Count && i < manager.playerParty.Count; i++) Data.party[i].hp = manager.playerParty[i].currentHP;
        if (victory)
        {
            if (!Data.collected.Contains(encounter.id))
            {
                Data.collected.Add(encounter.id);
                GainXP(encounter.xp);
                foreach (var reward in encounter.rewards) AddItem(reward.id, reward.count);
            }
            Notify("Chiến thắng! +" + encounter.xp + " EXP • Đã nhận chiến lợi phẩm.");
        }
        else
        {
            if (Config.defeatReturnsToCheckpoint && Data.hasCheckpoint)
            {
                Data.position = Data.checkpoint;
                Data.rotation = Data.checkpointRotation;
                Data.currentMapScene = !string.IsNullOrEmpty(Data.checkpointMapScene)
                    ? Data.checkpointMapScene
                    : Config.mapScene;
            }
            foreach (var member in Data.party) member.hp = Config.defeatReturnsToCheckpoint ? MaxHP(member) : Mathf.Max(1, Mathf.RoundToInt(MaxHP(member) * .3f));
            Notify("Thất bại • Đã trở về map. Quái vẫn còn để thử lại.");
        }
        Save();
        // The result panel waits for Continue before leaving combat.
    }
    public bool ContinueAfterBattle()
    {
        string returnScene = CurrentMapScene ?? Config.mapScene;
        if (encounter == null || !Busy || !LoadingScreen.Load(returnScene)) return false;
        encounter = null;
        return true;
    }
}

public enum AdventureMenu { None, Party, Inventory, Rest, Main }
