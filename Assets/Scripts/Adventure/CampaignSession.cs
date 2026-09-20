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
    public bool OnMap => Config != null && SceneManager.GetActiveScene().name == Config.mapScene;
    public bool CanUpgrade => OnMap && RestPoint != null && IsNear(RestPoint) && !Busy;
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
    }

    public static CampaignSession Ensure(CampaignConfig config)
    {
        if (Instance != null) return Instance;
        var obj = new GameObject("Campaign Session");
        var session = obj.AddComponent<CampaignSession>();
        session.Config = config;
        session.LoadOrCreate();
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
    void LoadOrCreate()
    {
        if (File.Exists(SavePath))
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
        if (Data != null) { Data.MigrateSkillUnlocks(); return; }
        Data = new CampaignSave();
        for (int i = 0; i < Config.startingParty.Count; i++)
            if (Config.startingParty[i] != null)
                Data.party.Add(new PartyMemberProgress { prefabIndex = i, hp = Config.startingParty[i].maxHP });
    }
    public void BindMap(PlayerScript player)
    {
        MapPlayer = player;
        Busy = false;
        if (Data.hasPosition) Teleport(Data.position, Data.rotation);
        else CapturePosition();
        if (!Data.hasCheckpoint)
        {
            Data.checkpoint = Data.position;
            Data.checkpointRotation = Data.rotation;
            Data.hasCheckpoint = true;
        }
        encounterAllowedAt = Time.unscaledTime + 2;
        if (!string.IsNullOrEmpty(Message)) MessageUntil = Time.unscaledTime + 5;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    public void RefreshWorld()
    {
        worldPoints =
            FindObjectsByType<WorldInteraction>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );


        foreach (WorldInteraction point in worldPoints)
        {
            if (point == null)
                continue;


            if (point.IsConsumed)
            {
                Debug.LogWarning(
                    "[Adventure] Hiding consumed interaction\n" +
                    "Name: " + point.name + "\n" +
                    "Kind: " + point.kind + "\n" +
                    "ID: " + point.Id,
                    point
                );


                point.gameObject.SetActive(false);
            }
        }
    }
    public BattleUnit Template(PartyMemberProgress member) => Config.startingParty[member.prefabIndex];
    public int MaxHP(PartyMemberProgress member) => Template(member).maxHP + member.hpBonus;
    public bool IsNear(WorldInteraction point) => MapPlayer != null && point != null &&
        point.gameObject.activeInHierarchy && Vector3.Distance(MapPlayer.transform.position, point.transform.position) <= point.range;
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
    public bool Save()
    {
        CapturePosition();
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
        if (!IsNear(rest)) return;
        RestPoint = rest;
        SetMenu(AdventureMenu.Rest);
    }
    public void RestAndSave()
    {
        if (!CanUpgrade) return;
        CapturePosition();
        Data.checkpoint = Data.position; Data.checkpointRotation = Data.rotation; Data.hasCheckpoint = true;
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
    public void BeginEncounter(WorldInteraction point)
    {
        if (Busy || Menu != AdventureMenu.None || !IsNear(point) || Time.unscaledTime < encounterAllowedAt || point.IsConsumed) return;
        if (point.enemyPrefabs.Count == 0 || point.enemyPrefabs.Any(x => x == null) || !Data.party.Any(p => p.hp > 0))
        { Notify("Cần party còn sống và prefab quái hợp lệ."); return; }
        if (!Application.CanStreamedLevelBeLoaded(Config.battleScene)) { Notify("Chưa thêm combattest vào Build Settings."); return; }
        CapturePosition();
        encounter = new EncounterData { id = point.Id, xp = point.experience, rewards = point.rewards.Select(x => new ItemStack(x.id, x.count)).ToList(), enemies = new List<BattleUnit>(point.enemyPrefabs) };
        Busy = true;
        if (!LoadingScreen.Load(Config.battleScene))
        { encounter = null; Busy = false; Notify(LoadingScreen.Error); }
    }
    public void PrepareBattle(CombatManager manager)
    {
        if (encounter == null) return;
        var oldPlayers = manager.playerParty.ToArray();
        var oldEnemies = manager.enemyParty.ToArray();
        // Include inactive test characters that are not in the manager's roster.
        foreach (var unit in FindObjectsByType<BattleUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (unit.gameObject.scene == manager.gameObject.scene) unit.gameObject.SetActive(false);
        manager.playerParty.Clear(); manager.enemyParty.Clear(); manager.allUnitsTimeline.Clear();
        manager.playerSlots = ExpandSlots(manager.playerSlots, Data.party.Count, manager.transform, "Party slot");
        manager.playerMeleeSlots = ExpandSlots(manager.playerMeleeSlots, Data.party.Count, manager.transform, "Party melee slot");
        manager.enemySlots = ExpandSlots(manager.enemySlots, encounter.enemies.Count, manager.transform, "Enemy slot");
        manager.enemyMeleeSlots = ExpandSlots(manager.enemyMeleeSlots, encounter.enemies.Count, manager.transform, "Enemy melee slot");
        for (int i = 0; i < Data.party.Count; i++)
        {
            var member = Data.party[i];
            var unit = Instantiate(Template(member), manager.playerSlots[i].position, manager.playerSlots[i].rotation);
            unit.gameObject.SetActive(true); unit.isPlayer = true;
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
            manager.enemyParty.Add(unit);
        }
        RebindCameras(oldPlayers, oldEnemies, manager);
        RefreshBattleItems(manager);
        Busy = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
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
            { Data.position = Data.checkpoint; Data.rotation = Data.checkpointRotation; }
            foreach (var member in Data.party) member.hp = Config.defeatReturnsToCheckpoint ? MaxHP(member) : Mathf.Max(1, Mathf.RoundToInt(MaxHP(member) * .3f));
            Notify("Thất bại • Đã trở về map. Quái vẫn còn để thử lại.");
        }
        Save();
        // The result panel waits for Continue before leaving combat.
    }
    public bool ContinueAfterBattle()
    {
        if (encounter == null || !Busy || !LoadingScreen.Load(Config.mapScene)) return false;
        encounter = null;
        return true;
    }
}

public enum AdventureMenu { None, Party, Inventory, Rest, Main }
