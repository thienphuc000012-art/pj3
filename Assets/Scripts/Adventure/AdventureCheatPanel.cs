using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Per-save cheats. Never edits character prefabs, skills or global time settings.</summary>
public static class AdventureCheats
{
    // HP, max HP, ATK, DEF, speed, crit %, crit damage %, stat points, skill points.
    public static int[] ReadStats(CampaignConfig config, PartyMemberProgress member)
    {
        var unit = config.startingParty[member.prefabIndex];
        return new[] { member.hp, unit.maxHP + member.hpBonus, unit.baseAtk + member.atkBonus, unit.baseDef + member.defBonus,
            unit.speed + member.speedBonus, unit.baseCrit + member.critBonus, unit.baseCritDamage + member.critDamageBonus, member.statPoints, member.skillPoints };
    }
    public static void ApplyStats(CampaignConfig config, PartyMemberProgress member, int[] values)
    {
        if (values == null || values.Length != 9) throw new ArgumentException("Expected nine stats.");
        var unit = config.startingParty[member.prefabIndex];
        member.hpBonus = Mathf.Clamp(values[1], 1, 1000000) - unit.maxHP;
        member.hp = Mathf.Clamp(values[0], 0, unit.maxHP + member.hpBonus);
        member.atkBonus = Mathf.Clamp(values[2], 0, 1000000) - unit.baseAtk;
        member.defBonus = Mathf.Clamp(values[3], 0, 1000000) - unit.baseDef;
        member.speedBonus = Mathf.Clamp(values[4], 1, 100000) - unit.speed;
        member.critBonus = Mathf.Clamp(values[5], 0, 100) - unit.baseCrit;
        member.critDamageBonus = Mathf.Clamp(values[6], 100, 10000) - unit.baseCritDamage;
        member.statPoints = Mathf.Clamp(values[7], 0, 100000);
        member.skillPoints = Mathf.Clamp(values[8], 0, 100000);
    }
    public static void UnlockAllSkills(CampaignConfig config, CampaignSave data)
    {
        foreach (var member in data.party)
        {
            member.skills ??= new List<SkillRank>();
            var skills = config.startingParty[member.prefabIndex].characterSkills?.Where(skill => skill != null).Distinct().ToArray() ?? Array.Empty<ActionData>();
            foreach (var skill in skills)
            {
                var rank = member.skills.Find(value => value.id == skill.name);
                if (rank == null) { rank = new SkillRank { id = skill.name }; member.skills.Add(rank); }
                rank.rank = Math.Max(1, rank.rank);
            }
            member.FillEmptySkillSlots(skills.Where(skill => !skill.isUltimate).Select(skill => skill.name).ToArray(), Math.Max(1, config.equippedSkillSlots));
            member.loadoutInitialized = true;
        }
    }
    public static void LevelUp(PartyMemberProgress member, int levels)
    {
        int target = Mathf.Clamp(member.level + Mathf.Clamp(levels, 1, 99), 1, 100);
        while (member.level < target) member.GainXP(member.RequiredXP - member.xp);
    }
    public static int UnlockRestPoints(CampaignSave data, IEnumerable<UnlockedRestPoint> points)
    {
        data.unlockedRestPoints ??= new List<UnlockedRestPoint>(); int count = 0;
        foreach (var point in points)
        {
            if (point == null || string.IsNullOrEmpty(point.id) || data.unlockedRestPoints.Any(value => value != null && value.id == point.id)) continue;
            data.unlockedRestPoints.Add(new UnlockedRestPoint { id = point.id, displayName = point.displayName, sceneName = point.sceneName, position = point.position, rotation = point.rotation });
            count++;
        }
        return count;
    }
}

[DisallowMultipleComponent]
public sealed class AdventureCheatPanel : MonoBehaviour
{
    static AdventureCheatPanel opened;
    static int blockThroughFrame = -1;
    public static bool IsOpen => opened != null || (Application.isPlaying && Time.frameCount <= blockThroughFrame);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { opened = null; blockThroughFrame = -1; }
    CampaignSession session;
    GameObject canvasRoot;
    RectTransform panel;
    TMP_FontAsset font;
    bool ownsFont;
    TMP_Text memberLabel, message;
    TMP_InputField[] stats;
    TMP_InputField dayMinutes, hour, levels;
    int memberIndex;
    float previousTimeScale;
    CursorLockMode previousLock;
    bool previousVisible;
    GameObject previousSelection;
    readonly Dictionary<Animator, float> pausedAnimators = new Dictionary<Animator, float>();
    static string L(string vi, string en) => GameLocalization.IsEnglish ? en : vi;
    static void DisposeObject(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }

    void Awake() { session = GetComponent<CampaignSession>(); }
    void OnEnable() { SceneManager.activeSceneChanged += SceneChanged; GameLocalization.LanguageChanged += LanguageChanged; }
    void OnDisable() { Close(); SceneManager.activeSceneChanged -= SceneChanged; GameLocalization.LanguageChanged -= LanguageChanged; }
    void OnDestroy()
    {
        Close(); if (canvasRoot != null) DisposeObject(canvasRoot);
        if (ownsFont && font != null)
        {
            foreach (var atlas in font.atlasTextures) if (atlas != null) DisposeObject(atlas);
            if (font.material != null) DisposeObject(font.material); DisposeObject(font);
        }
    }
    void SceneChanged(Scene before, Scene after) => Close();
    void LanguageChanged() { if (opened == this) { Build(); Refresh(); } else if (canvasRoot != null) { DisposeObject(canvasRoot); canvasRoot = null; } }
    void Update()
    {
        if (opened == this)
        {
            if (session == null || session.Data == null || LoadingScreen.IsLoading) { Close(); return; }
            bool typing = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.GetComponentInParent<TMP_InputField>() != null;
            if (Input.GetKeyDown(KeyCode.Escape) || (!typing && Input.GetKeyDown(KeyCode.O))) Close();
            return;
        }
        if (Input.GetKeyDown(KeyCode.O)) Open();
    }
    public bool Open()
    {
        if (opened != null || session?.Data?.party == null || session.Data.party.Count == 0 || session.Busy || session.PresentationActive ||
            session.Menu != AdventureMenu.None || LoadingScreen.IsLoading || MapChunkStreamer.MovementBlocked || (!session.OnMap && !session.InEncounter)) return false;
        previousTimeScale = Time.timeScale; previousLock = Cursor.lockState; previousVisible = Cursor.visible;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        opened = this; Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        // Some combat clips use unscaled time. Freeze them as well so their
        // animation events cannot resolve a hit while stats are being edited.
        var combat = CombatManager.Instance;
        if (!session.OnMap && session.InEncounter && combat != null)
            foreach (var unit in combat.playerParty.Concat(combat.enemyParty).Where(unit => unit != null))
                foreach (var animator in unit.GetComponentsInChildren<Animator>(true))
                    if (!pausedAnimators.ContainsKey(animator)) { pausedAnimators.Add(animator, animator.speed); animator.speed = 0; }
        session.MapPlayer?.MCC?.SuppressMouseLook();
        if (canvasRoot == null) Build();
        canvasRoot.SetActive(true); Refresh();
        EventSystem.current?.SetSelectedGameObject(null);
        return true;
    }
    public void Close()
    {
        if (opened != this) return;
        opened = null;
        blockThroughFrame = Time.frameCount + 1;
        foreach (var pair in pausedAnimators) if (pair.Key != null) pair.Key.speed = pair.Value;
        pausedAnimators.Clear();
        if (canvasRoot != null) canvasRoot.SetActive(false);
        Time.timeScale = previousTimeScale; Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        session?.MapPlayer?.MCC?.SuppressMouseLook();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
    }
    RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    TMP_Text Label(string name, string value, float x, float y, float width, float height = 38)
    {
        var text = Rect(name, panel, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = 24; text.color = new Color(.92f, .88f, .76f); text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.text = value; return text;
    }
    Button Button(string name, string value, float x, float y, float width, Action action)
    {
        var rect = Rect(name, panel, x, y, width, 44);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.12f, .14f, .17f, 1);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(rect, false); text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.font = font; text.fontSize = 23; text.alignment = TextAlignmentOptions.Center; text.text = value; text.raycastTarget = false;
        return button;
    }
    TMP_InputField Field(string name, float x, float y, float width, bool decimalNumber = false)
    {
        var rect = Rect(name, panel, x, y, width, 40);
        rect.gameObject.AddComponent<Image>().color = new Color(.04f, .05f, .07f, 1);
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        var viewport = Rect("Text Area", rect, 8, 0, width - 16, 40); viewport.gameObject.AddComponent<RectMask2D>();
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(viewport, false); text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.font = font; text.fontSize = 24; text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        input.textViewport = viewport; input.textComponent = text; input.fontAsset = font;
        input.contentType = decimalNumber ? TMP_InputField.ContentType.DecimalNumber : TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 9; input.lineType = TMP_InputField.LineType.SingleLine; return input;
    }
    public void Build()
    {
        if (canvasRoot != null) { canvasRoot.SetActive(false); DisposeObject(canvasRoot); }
        if (font == null)
        {
            var source = Resources.Load<AdventureCanvasRoot>("Adventure/UI/AdventureCanvas");
            if (source != null && source.languageFont != null)
            { font = TMP_FontAsset.CreateFontAsset(source.languageFont); ownsFont = true; font.atlasPopulationMode = AtlasPopulationMode.Dynamic; font.isMultiAtlasTexturesEnabled = true; }
            else font = source != null ? source.defaultFont : TMP_Settings.defaultFontAsset;
        }
        canvasRoot = new GameObject("Cheat Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
        var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var shade = new GameObject("Input Blocker", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); shade.transform.SetParent(canvasRoot.transform, false);
        shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one; shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
        shade.color = new Color(0, 0, 0, .78f);
        panel = Rect("Cheat Panel", canvasRoot.transform, 0, 0, 1180, 820); panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f); panel.pivot = new Vector2(.5f, .5f);
        panel.gameObject.AddComponent<Image>().color = new Color(.014f, .021f, .031f, .98f);
        Label("Title", L("CHEAT • O để đóng", "CHEATS • O to close"), 36, 22, 900).fontSize = 32;
        Button("Close", "×", 1086, 20, 56, Close);
        memberLabel = Label("Member", "", 100, 87, 870);
        Button("Previous", "‹", 36, 86, 50, () => Select(-1)); Button("Next", "›", 1086, 86, 56, () => Select(1));
        string[] vi = { "HP hiện tại", "HP tối đa", "Tấn công (ATK)", "Phòng thủ (DEF)", "Tốc độ", "Chí mạng (%)", "Sát thương chí mạng (%)", "Điểm thuộc tính", "Điểm kỹ năng" };
        string[] en = { "Current HP", "Maximum HP", "Attack (ATK)", "Defense (DEF)", "Speed", "Critical chance (%)", "Critical damage (%)", "Stat points", "Skill points" };
        stats = new TMP_InputField[9];
        for (int i = 0; i < stats.Length; i++) { float y = 151 + i * 47; Label("Stat Label " + i, L(vi[i], en[i]), 36, y, 340); stats[i] = Field("Stat " + i, 388, y, 160); }
        Button("Apply Stats", L("Áp dụng chỉ số", "Apply stats"), 36, 586, 512, ApplyStats);
        Label("Time Heading", L("THỜI GIAN NGÀY / ĐÊM", "DAY / NIGHT CLOCK"), 610, 151, 530);
        Label("Day Length Label", L("Phút thực / 1 ngày", "Real minutes / game day"), 610, 201, 350);
        dayMinutes = Field("Day Length", 966, 201, 176, true);
        Label("Hour Label", L("Giờ trong game (0–24)", "Game hour (0–24)"), 610, 251, 350);
        hour = Field("Hour", 966, 251, 176, true);
        Button("Apply Time", L("Áp dụng thời gian", "Apply time"), 610, 304, 532, ApplyTime);
        Label("Day Night Note", L("Ánh sáng ngày/đêm chỉ thay đổi ở scene m.", "Day/night lighting changes only in scene m."), 610, 354, 532, 60).fontSize = 21;
        Label("Levels Label", L("Số cấp muốn tăng", "Levels to add"), 610, 432, 350); levels = Field("Levels", 966, 432, 176); levels.text = "1";
        Button("Level Up", L("Lên cấp nhân vật", "Level up character"), 610, 484, 532, LevelUp);
        Button("Unlock Skills", L("Mở tất cả kỹ năng • cả đội", "Unlock all skills • entire party"), 610, 536, 532, UnlockSkills);
        Button("Unlock Save Points", L("Mở tất cả điểm lưu trên các map", "Unlock every map save point"), 610, 588, 532, UnlockSavePoints);
        message = Label("Status", "", 36, 667, 1106, 70); message.fontSize = 23;
        Label("Hint", L("Thay đổi được lưu vào tiến trình hiện tại. Thoát bằng O, Esc hoặc ×.", "Changes are saved to current progress. Close with O, Esc or ×."), 36, 755, 1106).fontSize = 21;
        canvasRoot.SetActive(opened == this);
    }
    void Select(int delta) { memberIndex = (memberIndex + delta + session.Data.party.Count) % session.Data.party.Count; Refresh(); }
    void Refresh()
    {
        memberIndex = Mathf.Clamp(memberIndex, 0, session.Data.party.Count - 1);
        var member = session.Data.party[memberIndex]; var manager = CombatManager.Instance;
        if (!session.OnMap && session.InEncounter && manager != null && memberIndex < manager.playerParty.Count && manager.playerParty[memberIndex] != null)
            member.hp = manager.playerParty[memberIndex].currentHP;
        var template = session.Template(member);
        memberLabel.text = (string.IsNullOrWhiteSpace(template.unitName) ? template.name : template.unitName) + " • Lv " + member.level;
        int[] values = AdventureCheats.ReadStats(session.Config, member);
        for (int i = 0; i < values.Length; i++) stats[i].SetTextWithoutNotify(values[i].ToString());
        dayMinutes.SetTextWithoutNotify(AdventureWorldTime.DayLengthMinutes(session.Data).ToString("0.##", CultureInfo.InvariantCulture));
        hour.SetTextWithoutNotify((AdventureWorldTime.Read(session.Data) % 24).ToString("0.##", CultureInfo.InvariantCulture));
    }
    void ApplyStats()
    {
        int[] values = new int[stats.Length];
        for (int i = 0; i < values.Length; i++) if (!int.TryParse(stats[i].text, out values[i])) { message.text = L("Hãy nhập số nguyên hợp lệ.", "Enter valid whole numbers."); return; }
        var member = session.Data.party[memberIndex]; var before = AdventureCheats.ReadStats(session.Config, member);
        AdventureCheats.ApplyStats(session.Config, member, values); SyncBattle(before); Persist();
    }
    void SyncBattle(int[] before)
    {
        var manager = CombatManager.Instance;
        if (session.OnMap || !session.InEncounter || manager == null || memberIndex >= manager.playerParty.Count) return;
        var unit = manager.playerParty[memberIndex]; if (unit == null) return;
        var member = session.Data.party[memberIndex]; var after = AdventureCheats.ReadStats(session.Config, member);
        unit.maxHP += after[1] - before[1]; unit.baseAtk += after[2] - before[2]; unit.baseDef += after[3] - before[3];
        unit.speed += after[4] - before[4]; unit.baseCrit += after[5] - before[5]; unit.baseCritDamage += after[6] - before[6];
        unit.SetCheatHP(member.hp);
    }
    static bool Number(TMP_InputField field, out float value) => float.TryParse(field.text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
    void ApplyTime()
    {
        if (!Number(dayMinutes, out float minutes) || !Number(hour, out float hours) || minutes < 1 || minutes > 1440 || hours < 0 || hours > 24)
        { message.text = L("Một ngày: 1–1440 phút; giờ: 0–24.", "Day duration: 1–1440 minutes; hour: 0–24."); return; }
        session.Data.dayLengthMinutesOverride = minutes;
        session.Data.worldTimeHours = Math.Floor(AdventureWorldTime.Read(session.Data) / 24) * 24 + hours % 24;
        session.Data.hasWorldTime = true; Persist();
    }
    void LevelUp()
    {
        if (!int.TryParse(levels.text, out int count) || count < 1) { message.text = L("Nhập số cấp từ 1–99.", "Enter 1–99 levels."); return; }
        AdventureCheats.LevelUp(session.Data.party[memberIndex], count); Persist();
    }
    void UnlockSkills()
    {
        AdventureCheats.UnlockAllSkills(session.Config, session.Data);
        var manager = CombatManager.Instance;
        if (!session.OnMap && session.InEncounter && manager != null)
            for (int i = 0; i < session.Data.party.Count && i < manager.playerParty.Count; i++)
                if (manager.playerParty[i] != null) manager.playerParty[i].characterSkills = session.EquippedSkills(session.Data.party[i]);
        Persist();
    }
    void UnlockSavePoints()
    {
        var catalog = Resources.Load<AdventureRestPointCatalog>("Adventure/RestPointCatalog");
        var points = new List<UnlockedRestPoint>(catalog != null ? catalog.points : new List<UnlockedRestPoint>());
        foreach (var point in FindObjectsByType<WorldInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (point.kind == WorldInteractionKind.RestPoint && !points.Any(value => value.id == point.Id))
                points.Add(AdventureRestPointCatalog.FromPoint(point, session.CurrentMapScene));
        int count = AdventureCheats.UnlockRestPoints(session.Data, points); Persist();
        message.text = L("Đã mở thêm ", "Unlocked ") + count + L(" điểm lưu.", " save points.");
    }
    void Persist()
    {
        var manager = CombatManager.Instance;
        if (!session.OnMap && session.InEncounter && manager != null)
            for (int i = 0; i < session.Data.party.Count && i < manager.playerParty.Count; i++)
                if (manager.playerParty[i] != null) session.Data.party[i].hp = Mathf.Clamp(manager.playerParty[i].currentHP, 0, session.MaxHP(session.Data.party[i]));
        bool saved = session.Save(capturePosition: session.OnMap); Refresh();
        message.text = saved ? L("Đã áp dụng và lưu.", "Applied and saved.") : session.Message;
    }
}
