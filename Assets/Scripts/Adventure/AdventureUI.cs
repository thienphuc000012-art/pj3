using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AdventureUI : MonoBehaviour
{
    int selectedMember, selectedSkill, restPage;
    string selectedItemId;
    int selectedSlot;
    bool skillContextOpen, skillDialogOpen;
    ActionData pendingSkill;
    PartyMemberProgress skillDialogMember;
    HoldToConfirmButton attributeUpgradeHold;
    void CloseSkillChange() { skillContextOpen = false; skillDialogOpen = false; pendingSkill = null; skillDialogMember = null; }
    void OpenSkillContext(int slot) { selectedSlot = slot; CloseSkillChange(); skillContextOpen = true; skillDialogMember = S.Data.party[selectedMember]; }

    readonly List<PartyMenuStage> cardStages = new List<PartyMenuStage>();
    readonly int[] pendingStats = new int[5];
    PartyMemberProgress draftMember;
    void ResetStatDraft() { System.Array.Clear(pendingStats, 0, pendingStats.Length); draftMember = null; }
    AdventureMenu previousMenu;
    PartyMenuStage stage;
    AdventureCanvasRoot view;
    GameObject questPage, questDialog, dialoguePage;
    Button languageToggleButton;
    RectTransform questContent, dialoguePanel, dialogueChoices;
    GameObject dialogueOptionsPanel;
    TMPro.TMP_Text questPrompt, dialogueSpeaker, dialogueText;
    TMPro.TMP_Text dialogueNextLabel;
    Button dialogueScreenButton;
    AdventureQuest selectedQuest;
    string renderedDialogueNode;
    int dialogueCharacterCount, dialogueVisibleCharacters;
    float dialogueTypewriterProgress;
    bool dialogueTyping, dialogueChoicesRevealed;
    readonly List<Button> dialogueChoiceButtons = new List<Button>();
    CampaignSession S => CampaignSession.Instance;
    const string Content = "/Viewport/Content";
    void Awake() { stage = gameObject.AddComponent<PartyMenuStage>(); }
    void Update()
    {
        if (S == null || !S.OnMap || S.Data == null)
        {
            if (view != null) view.gameObject.SetActive(false);
            stage.Clear();
            foreach (var card in cardStages) card.Clear();
            previousMenu = AdventureMenu.None; return;
        }
        if (view == null) { view = AdventureCanvasRoot.Acquire(AdventureCanvasKind.Adventure); Bind(); }
        bool keepHudVisibleDuringInteraction = S.Busy && S.KeepHudVisibleDuringItemInteraction;
        view.gameObject.SetActive(!S.Busy || keepHudVisibleDuringInteraction);
        if (S.Busy)
        {
            if (keepHudVisibleDuringInteraction)
            {
                view.Active("HUD/Interact", false);
                view.Active("Toast", Time.unscaledTime < S.MessageUntil);
                view.Text("Toast/Message", S.Message ?? "");
            }
            return;
        }
        if (previousMenu != S.Menu)
        {
            restPage = 0; selectedSkill = 0; CloseSkillChange(); CloseTravel();
            if (S.Menu != AdventureMenu.Tasks) CloseQuestDialog();
            previousMenu = S.Menu;
            if (S.Menu == AdventureMenu.Tasks) RefreshQuestList();
            if (S.Menu == AdventureMenu.Dialogue)
            {
                renderedDialogueNode = null;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        selectedMember = Mathf.Clamp(selectedMember, 0, Mathf.Max(0, S.Data.party.Count - 1));
        bool menuOpen = S.Menu != AdventureMenu.None;
        if (S.Menu != AdventureMenu.Main) foreach (var card in cardStages) card.Hide();
        if (menuOpen && !(travelPanel != null && travelPanel.activeSelf) && !skillDialogOpen && !skillContextOpen && S.Menu != AdventureMenu.Inventory && S.Menu != AdventureMenu.Main && S.Menu != AdventureMenu.Tasks && S.Menu != AdventureMenu.Dialogue && Input.GetKeyDown(KeyCode.Q)) SelectMember(-1);
        if (menuOpen && !(travelPanel != null && travelPanel.activeSelf) && !skillDialogOpen && !skillContextOpen && S.Menu != AdventureMenu.Inventory && S.Menu != AdventureMenu.Main && S.Menu != AdventureMenu.Tasks && S.Menu != AdventureMenu.Dialogue && Input.GetKeyDown(KeyCode.R)) SelectMember(1);
        view.Active("HUD", !menuOpen); view.Active("Menu", menuOpen);
        view.Active("Toast", Time.unscaledTime < S.MessageUntil);
        view.Text("Toast/Message", S.Message ?? "");
        if (!menuOpen)
        {
            ResetStatDraft();
            stage.Hide();
            if (S.Data.party.Count > 0)
            {
                var p = S.Data.party[0];
                view.Text("HUD/Leader/Name", S.Template(p).unitName + " • Cấp " + p.level);
                view.Text("HUD/Leader/Progress", p.xp + " / " + p.RequiredXP + " KN");
                view.Fill("HUD/Leader/XP/Fill", (float)p.xp / p.RequiredXP);
            }
            view.Active("HUD/Interact", S.Nearest != null);
            if (S.Nearest != null) view.Text("HUD/Interact/Label", S.Nearest.displayName);
            return;
        }
        bool main = S.Menu == AdventureMenu.Main, party = S.Menu == AdventureMenu.Party, rest = S.Menu == AdventureMenu.Rest;
        bool tasks = S.Menu == AdventureMenu.Tasks, dialogue = S.Menu == AdventureMenu.Dialogue;
        bool detail = rest && (restPage == 1 || restPage == 2);
        view.Active("Menu/Background", !rest && !dialogue);
        view.Active("Menu/Pattern", !rest && !dialogue);
        foreach (string chrome in new[] { "Title", "TitleLine", "Back" })
            view.Active("Menu/" + chrome, !dialogue);
        foreach (string panel in new[] { "Rest", "Attributes", "Skills", "Craft" })
        {
            var backdrop = view.transform.Find("Menu/" + panel + "/Backdrop");
            if (backdrop != null) backdrop.gameObject.SetActive(!rest);
        }
        view.Active("Menu/Preview", false);
        view.Active("Menu/DetailPreview", false);
        if (detail && S.Data.party.Count > 0)
        {
            var unit = S.Template(S.Data.party[selectedMember]);
            var assigned = S.Config.PreviewImage(unit);
            string prefix = restPage == 1 ? "Menu/Attributes/CharacterPreview" : "Menu/Skills/CharacterPreview";
            var portrait = view.Component<Image>(prefix + "/Portrait");
            portrait.sprite = assigned != null ? assigned : unit.unitPortrait;
            portrait.preserveAspect = true; portrait.enabled = portrait.sprite != null;
            var preview = view.Component<RawImage>(prefix + "/Render");
            if (assigned != null) stage.Hide(); else stage.Show(new[] { unit });
            preview.texture = stage.Texture; preview.enabled = assigned == null && stage.ReadyFor(unit);
        }
        else if (party)
        {
            stage.Show(S.Data.party.Skip(selectedMember).Take(1).Select(p => S.Template(p)));
            var preview = view.Component<RawImage>("Menu/Party/Member/Preview");
            preview.texture = stage.Texture; preview.enabled = stage.Ready;
        }
        else stage.Hide();
        string page = dialogue ? "Dialogue" : tasks ? "Tasks" : main || party ? "Party" : S.Menu == AdventureMenu.Inventory ? "Inventory" : restPage == 1 ? "Attributes" : restPage == 2 ? "Skills" : restPage == 3 ? "Craft" : "Rest";
        if (page != "Attributes")
        {
            ResetStatDraft();
            attributeUpgradeHold?.SetAvailable(false);
        }
        foreach (string name in new[] { "Party", "Inventory", "Rest", "Attributes", "Skills", "Craft", "Tasks", "Dialogue" }) view.Active("Menu/" + name, name == page);
        if (!dialogue)
        {
            view.Text("Menu/Title", main ? "MENU CHÍNH" : tasks ? "NHIỆM VỤ" : party ? "ĐỘI HÌNH" : page == "Inventory" ? "RƯƠNG ĐỒ" : page == "Attributes" ? "THUỘC TÍNH" : page == "Skills" ? "KỸ NĂNG" : page == "Craft" ? "CHẾ TẠO" : "ĐIỂM NGHỈ");
            view.Text("Menu/Back/Label", main ? "Đóng" : rest && restPage > 0 ? "Điểm nghỉ" : "Trở lại");
        }
        if (page == "Party") {
            view.Active("Menu/Party/Overview", main); view.Active("Menu/Party/Member", party);
            if (main) Party(); else MemberSkills();
        }
        else if (page == "Inventory") Inventory();
        else if (page == "Dialogue") UpdateDialogue();
        else if (page == "Rest") Rest();
        else if (page == "Attributes") Attributes();
        else if (page == "Skills") Skills();
        else if (page == "Craft") Craft();
    }
    void Bind()
    {
        BuildFastTravelUI();
        BuildQuestUI();
        BuildLanguageToggle();
        BuildDialogueUI();
        var attributeConfirm = view.Component<Button>("Menu/Attributes/Confirm");
        attributeUpgradeHold = attributeConfirm.GetComponent<HoldToConfirmButton>();
        if (attributeUpgradeHold == null) attributeUpgradeHold = attributeConfirm.gameObject.AddComponent<HoldToConfirmButton>();
        attributeUpgradeHold.Bind(ApplyPendingAttributeUpgrade);
        PositionAttributeConfirmUnderStats();
        view.Text("Menu/Attributes/Confirm/Label", "Nâng cấp thuộc tính");
        view.Active("Menu/Attributes/Confirm", true);
        view.Active("Menu/Attributes/Cancel", false);
        view.Active("Menu/Subtitle", false);
        view.Active("Menu/Hints", false);
        view.Active("Menu/Party/Member/Hint", false);
        ClearInstructionText("Chọn + / - rồi bấm Chấp nhận để áp dụng.");
        ClearInstructionText("Chọn ô rồi click kỹ năng đã học. Thay đổi tự lưu.");
        ClearInstructionText("Chọn Party để đổi kỹ năng mang theo hoặc Inventory để xem rương đồ.");
        ClearInstructionText("Click một ô để xem thông tin vật phẩm.");
        ClearInstructionText("Tận dụng nguyên liệu thu thập trên đường để chuẩn bị vật phẩm cho cả party.");
        ClearInstructionText("Dừng chân. Hồi phục.\nChuẩn bị cho chặng đường tiếp theo.");
        ClearInstructionText("P Party   B Rương đồ   Q / R Đổi thành viên");
        ClearInstructionText("P  Main Menu     E  Tương tác");
        view.Click("Menu/Back", () => { if (!HandleBack()) S.SetMenu(AdventureMenu.None); });
        view.Click("Menu/Party/Overview/Party", () => S.SetMenu(AdventureMenu.Party));
        view.Click("Menu/Party/Overview/Inventory", () => S.SetMenu(AdventureMenu.Inventory));
        view.Click("HUD/Interact", () => S.Nearest?.Interact());
        view.Click("Menu/Inventory/Detail/Use", () => { if (selectedItemId != null) S.UseItem(selectedItemId, selectedMember); });
        view.Click("Menu/Party/Member/MoveLeft", () => { if (selectedMember > 0) { S.Swap(selectedMember, selectedMember - 1); selectedMember--; } });
        view.Click("Menu/Party/Member/MoveRight", () => { if (selectedMember + 1 < S.Data.party.Count) { S.Swap(selectedMember, selectedMember + 1); selectedMember++; } });
        view.Click("Menu/Party/Member/SkillContext/Change", () => { skillContextOpen = false; skillDialogOpen = true; pendingSkill = null; });
        view.Click("Menu/Party/Member/SkillContext/Cancel", CloseSkillChange);
        view.Click("Menu/Party/Member/SkillDialog/Cancel", CloseSkillChange);
        view.Click("Menu/Party/Member/SkillDialog/Confirm", () => {
            if (pendingSkill != null && skillDialogMember == S.Data.party[selectedMember]) S.EquipSkill(selectedMember, selectedSlot, pendingSkill);
            CloseSkillChange();
        });
        view.Click("Menu/Rest/Save", () => S.RestAndSave());
        view.Click("Menu/Rest/Attributes", () => restPage = 1);
        view.Click("Menu/Rest/Skills", () => { restPage = 2; SelectLearnable(); });
        view.Click("Menu/Rest/Craft", () => restPage = 3);
        view.Click("Menu/Skills/Detail/Upgrade", () =>
        {
            var skills = CurrentSkills();
            if (selectedSkill < skills.Length) S.UnlockSkill(selectedMember, skills[selectedSkill]);
        });
        // Runtime-created menu buttons are added after AdventureCanvasRoot.Awake themes the prefab.
        GameUITheme.RefreshMenuButtonHover(view.gameObject);
    }
    void ClearInstructionText(string text)
    {
        foreach (var label in view.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if (label != null && (string.Equals(label.text.Trim(), text, System.StringComparison.Ordinal) ||
                                  string.Equals(label.text.Trim(), GameLocalization.Translate(text), System.StringComparison.Ordinal)))
                GameLocalization.SetText(label, "");
    }
    GameObject travelPanel, travelDialog;
    RectTransform travelContent;
    Button travelButtonTemplate;
    TMPro.TMP_Text travelQuestion;
    string travelDestination;
    void CloseTravel()
    {
        if (travelPanel != null) travelPanel.SetActive(false);
        if (travelDialog != null) travelDialog.SetActive(false);
        travelDestination = null;
    }
    static RectTransform TravelRect(GameObject obj, Transform parent, Vector2 min, Vector2 max)
    {
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    Button TravelButton(Transform parent, string name, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        var button = Instantiate(travelButtonTemplate, parent); button.name = name;
        TravelRect(button.gameObject, parent, min, max);
        button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(action);
        var buttonLabel = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        GameLocalization.SetText(buttonLabel, label);
        buttonLabel.enableWordWrapping = true;
        button.gameObject.SetActive(true);
        GameUITheme.Apply(button.gameObject);
        return button;
    }
    TMPro.TMP_Text TravelText(Transform parent, string name, string text, Vector2 min, Vector2 max)
    {
        var source = travelButtonTemplate.GetComponentInChildren<TMPro.TMP_Text>(true);
        var label = Instantiate(source, parent); label.name = name;
        TravelRect(label.gameObject, parent, min, max);
        GameLocalization.SetText(label, text); label.raycastTarget = false;
        label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        label.gameObject.SetActive(true); return label;
    }
    GameObject TravelPanel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        TravelRect(panel, parent, min, max);
        panel.GetComponent<Image>().color = new Color(.012f, .014f, .017f, .96f);
        return panel;
    }
    void BuildFastTravelUI()
    {
        var rest = view.Node("Menu/Rest");
        travelButtonTemplate = view.Component<Button>("Menu/Rest/Save");
        // Insert a fifth action while keeping the existing authored button style and spacing.
        var save = (RectTransform)travelButtonTemplate.transform;
        var attributes = view.Component<RectTransform>("Menu/Rest/Attributes");
        var step = attributes.anchoredPosition - save.anchoredPosition;
        var entry = Instantiate(travelButtonTemplate, rest); entry.name = "FastTravel";
        ((RectTransform)entry.transform).anchoredPosition = attributes.anchoredPosition;
        GameLocalization.SetText(entry.GetComponentInChildren<TMPro.TMP_Text>(true), "Di chuyển nhanh");
        entry.onClick = new Button.ButtonClickedEvent(); entry.onClick.AddListener(OpenTravel);
        GameUITheme.Apply(entry.gameObject);
        foreach (var path in new[] { "Attributes", "Skills", "Craft" })
            view.Component<RectTransform>("Menu/Rest/" + path).anchoredPosition += step;
        travelPanel = TravelPanel("FastTravelList", view.Node("Menu"), Vector2.zero, Vector2.one);
        travelPanel.GetComponent<Image>().color = new Color(.012f, .014f, .017f, .96f);
        var listBox = TravelPanel("Box", travelPanel.transform, new Vector2(.2f,.15f), new Vector2(.8f,.85f));
        TravelText(listBox.transform, "Title", "DI CHUYỂN NHANH - Điểm nghỉ đã mở khóa", new Vector2(.05f,.86f), new Vector2(.95f,.98f));
        var scrollObj = new GameObject("List", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
        var viewport = TravelRect(scrollObj, listBox.transform, new Vector2(.05f,.18f), new Vector2(.95f,.84f));
        scrollObj.GetComponent<Image>().color = new Color(.08f, .085f, .09f, .55f);
        var contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        travelContent = TravelRect(contentObj, viewport, new Vector2(0,1), Vector2.one);
        travelContent.pivot = new Vector2(.5f,1);
        var layout = contentObj.GetComponent<VerticalLayoutGroup>(); layout.spacing = 10; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        contentObj.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollObj.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = travelContent; scroll.horizontal = false;
        TravelButton(listBox.transform, "Back", "Trở lại", new Vector2(.6f,.03f), new Vector2(.95f,.14f), CloseTravel);
        travelDialog = TravelPanel("FastTravelConfirmation", view.Node("Menu"), Vector2.zero, Vector2.one);
        travelDialog.GetComponent<Image>().color = new Color(.012f, .014f, .017f, .96f);
        var box = TravelPanel("Box", travelDialog.transform, new Vector2(.25f,.32f), new Vector2(.75f,.68f));
        travelQuestion = TravelText(box.transform, "Question", "", new Vector2(.06f,.45f), new Vector2(.94f,.92f));
        TravelButton(box.transform, "Yes", "Có", new Vector2(.08f,.12f), new Vector2(.46f,.35f), () => { if (S.FastTravel(travelDestination)) CloseTravel(); });
        TravelButton(box.transform, "No", "Không", new Vector2(.54f,.12f), new Vector2(.92f,.35f), () => travelDialog.SetActive(false));
        CloseTravel();
    }

    void BuildLanguageToggle()
    {
        var overview = view.Node("Menu/Party/Overview");
        var template = view.Component<Button>("Menu/Party/Overview/Inventory");
        languageToggleButton = Instantiate(template, overview);
        languageToggleButton.name = "Language Toggle";
        var rect = (RectTransform)languageToggleButton.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(300f, 58f);
        rect.anchoredPosition = new Vector2(-72f, -92f);
        languageToggleButton.onClick = new Button.ButtonClickedEvent();
        languageToggleButton.onClick.AddListener(GameLocalization.ToggleLanguage);
        GameLocalization.SetText(languageToggleButton.GetComponentInChildren<TMPro.TMP_Text>(true), GameLocalization.LanguageButtonLabel);
        GameUITheme.Apply(languageToggleButton.gameObject);
    }

    void BuildQuestUI()
    {
        var overview = view.Node("Menu/Party/Overview");
        var inventory = view.Component<Button>("Menu/Party/Overview/Inventory");
        var taskButton = Instantiate(inventory, overview);
        taskButton.name = "Quests";
        ((RectTransform)taskButton.transform).anchoredPosition = ((RectTransform)inventory.transform).anchoredPosition + Vector2.down * 88f;
        taskButton.onClick = new Button.ButtonClickedEvent();
        taskButton.onClick.AddListener(() => S.SetMenu(AdventureMenu.Tasks));
        GameLocalization.SetText(taskButton.GetComponentInChildren<TMPro.TMP_Text>(true), "NHIỆM VỤ");

        questPage = new GameObject("Tasks", typeof(RectTransform), typeof(Image));
        TravelRect(questPage, view.Node("Menu"), Vector2.zero, Vector2.one);
        questPage.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        questPage.GetComponent<Image>().raycastTarget = false;
        var listPanel = TravelPanel("Quest List", questPage.transform, new Vector2(.09f, .16f), new Vector2(.91f, .84f));
        listPanel.GetComponent<Image>().color = new Color(.012f, .014f, .017f, .78f);
        TravelText(listPanel.transform, "Heading", "NHIỆM VỤ ĐANG CÓ", new Vector2(.04f, .87f), new Vector2(.96f, .98f));
        var scrollObject = new GameObject("Quest Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
        var viewport = TravelRect(scrollObject, listPanel.transform, new Vector2(.04f, .1f), new Vector2(.96f, .84f));
        scrollObject.GetComponent<Image>().color = new Color(.05f, .06f, .07f, .45f);
        var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        questContent = TravelRect(contentObject, viewport, new Vector2(0f, 1f), Vector2.one);
        questContent.pivot = new Vector2(.5f, 1f);
        var listLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f; listLayout.padding = new RectOffset(6, 6, 6, 6);
        listLayout.childControlHeight = true; listLayout.childForceExpandHeight = false;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollObject.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = questContent; scroll.horizontal = false;
        questDialog = TravelPanel("Quest Track Confirmation", view.Node("Menu"), Vector2.zero, Vector2.one);
        questDialog.GetComponent<Image>().color = new Color(0f, 0f, 0f, .62f);
        var box = TravelPanel("Panel", questDialog.transform, new Vector2(.27f, .3f), new Vector2(.73f, .7f));
        questPrompt = TravelText(box.transform, "Prompt", "", new Vector2(.07f, .38f), new Vector2(.93f, .91f));
        TravelButton(box.transform, "Yes", "Có", new Vector2(.08f, .1f), new Vector2(.46f, .3f), ConfirmTrackQuest);
        TravelButton(box.transform, "No", "Không", new Vector2(.54f, .1f), new Vector2(.92f, .3f), CloseQuestDialog);
        questDialog.SetActive(false);
    }

    void RefreshQuestList()
    {
        if (questContent == null) return;
        foreach (Transform child in questContent) Destroy(child.gameObject);
        var progressList = S.QuestProgresses;
        var entries = progressList == null ? null : progressList.Where(progress => progress != null)
            .Select(progress => new { progress, quest = S.FindQuestDefinition(progress.questId) })
            .Where(entry => entry.quest != null).OrderBy(entry => entry.progress.completed)
            .ThenBy(entry => entry.quest.displayName).ToArray();
        if (entries == null || entries.Length == 0)
        {
            var empty = TravelText(questContent, "Empty", "Chưa có nhiệm vụ.", Vector2.zero, Vector2.one);
            var size = empty.gameObject.AddComponent<LayoutElement>(); size.preferredHeight = 64f;
            return;
        }
        foreach (var entry in entries)
        {
            var progress = entry.progress; var quest = entry.quest;
            string status = progress.completed
                ? progress.rewardsClaimed ? "Hoàn thành" : "Hoàn thành • Báo cáo NPC để nhận thưởng"
                : progress.objectiveCount + " / " + Mathf.Max(1, quest.requiredCount);
            status = GameLocalization.Translate(status);
            if (S.Data.trackedQuestId == progress.questId) status += GameLocalization.Translate(" • Đang theo dõi");
            var button = TravelButton(questContent, "Quest " + quest.Id, quest.displayName + "\n" + status,
                Vector2.zero, Vector2.one, () => OpenQuestDialog(quest));
            var size = button.gameObject.AddComponent<LayoutElement>(); size.preferredHeight = 72f; size.minHeight = 60f;
            button.interactable = !progress.completed || !progress.rewardsClaimed;
        }
    }

    void OpenQuestDialog(AdventureQuest quest)
    {
        if (quest != null && S.Data.trackedQuestId == quest.Id)
        {
            S.TrackQuest(quest.Id);
            RefreshQuestList();
            return;
        }
        selectedQuest = quest;
        var progress = quest != null ? S.QuestProgress(quest.Id) : null;
        string progressText = progress != null && progress.completed
            ? progress.rewardsClaimed ? GameLocalization.Translate("Đã báo cáo và nhận thưởng.") : GameLocalization.Translate("Mục tiêu đã xong. Hãy quay lại NPC để nhận thưởng.")
            : GameLocalization.Translate("Tiến độ: " + (progress?.objectiveCount ?? 0) + " / " + Mathf.Max(1, quest != null ? quest.requiredCount : 1));
        string prompt = progress != null && progress.completed && !progress.rewardsClaimed
            ? GameLocalization.Translate("Theo dõi vị trí NPC để báo cáo nhiệm vụ?")
            : GameLocalization.Translate("Theo dõi nhiệm vụ?");
        GameLocalization.SetText(questPrompt, quest == null ? prompt :
            quest.displayName + "\n\n" + quest.description + "\n\n" + progressText + "\n\n" + prompt);
        questDialog.SetActive(true);
        questDialog.transform.SetAsLastSibling();
    }

    void ConfirmTrackQuest()
    {
        if (selectedQuest != null && S.TrackQuest(selectedQuest.Id)) RefreshQuestList();
        CloseQuestDialog();
    }

    void CloseQuestDialog()
    {
        selectedQuest = null;
        if (questDialog != null) questDialog.SetActive(false);
    }

    void BuildDialogueUI()
    {
        dialoguePage = new GameObject("Dialogue", typeof(RectTransform), typeof(Image));
        TravelRect(dialoguePage, view.Node("Menu"), Vector2.zero, Vector2.one);
        var screenImage = dialoguePage.GetComponent<Image>();
        screenImage.color = new Color(0f, 0f, 0f, 0f);
        screenImage.raycastTarget = true;
        dialogueScreenButton = dialoguePage.AddComponent<Button>();
        dialogueScreenButton.targetGraphic = screenImage;
        dialogueScreenButton.transition = Selectable.Transition.None;
        dialogueScreenButton.navigation = new Navigation { mode = Navigation.Mode.None };
        dialogueScreenButton.onClick.AddListener(HandleDialogueScreenClick);

        var panel = new GameObject("Dialogue Frame", typeof(RectTransform));
        dialoguePanel = TravelRect(panel, dialoguePage.transform, new Vector2(.01f, .04f), new Vector2(.89f, .20f));

        var shade = new GameObject("Dialogue Frame Shade", typeof(RectTransform), typeof(Image));
        TravelRect(shade, panel.transform, Vector2.zero, Vector2.one);
        var shadeImage = shade.GetComponent<Image>();
        shadeImage.color = new Color(0f, 0f, 0f, .32f);
        shadeImage.raycastTarget = false;

        var frameObject = new GameObject("Dialogue Frame Art", typeof(RectTransform), typeof(Image));
        TravelRect(frameObject, panel.transform, Vector2.zero, Vector2.one);
        var frame = frameObject.GetComponent<Image>();
        frame.sprite = GameUITheme.DialogueBackgroundSprite;
        frame.type = Image.Type.Simple;
        frame.preserveAspect = false;
        frame.color = Color.white;
        frame.raycastTarget = false;

        dialogueSpeaker = TravelText(dialoguePage.transform, "Speaker", "", new Vector2(.025f, .20f), new Vector2(.78f, .248f));
        dialogueSpeaker.fontSize *= 1.25f;
        dialogueSpeaker.color = new Color(.95f, .94f, .91f, 1f);
        dialogueSpeaker.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        dialogueText = TravelText(panel.transform, "Line", "", new Vector2(.035f, .24f), new Vector2(.82f, .76f));
        dialogueText.color = new Color(.93f, .92f, .88f, 1f);
        dialogueText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        dialogueText.margin = new Vector4(12f, 0f, 8f, 0f);
        dialogueText.fontSize *= 1.08f;
        dialogueNextLabel = TravelText(panel.transform, "Next Label", "Tiếp", new Vector2(.835f, .30f), new Vector2(.975f, .70f));
        dialogueNextLabel.alignment = TMPro.TextAlignmentOptions.Center;
        dialogueNextLabel.color = new Color(.92f, .9f, .85f, 1f);

        dialogueOptionsPanel = new GameObject("Dialogue Options Panel", typeof(RectTransform), typeof(Image));
        TravelRect(dialogueOptionsPanel, dialoguePage.transform, new Vector2(.01f, .025f), new Vector2(.91f, .245f));
        var optionsBackground = dialogueOptionsPanel.GetComponent<Image>();
        optionsBackground.sprite = GameUITheme.DialogueBackgroundSprite;
        optionsBackground.type = Image.Type.Simple;
        optionsBackground.preserveAspect = false;
        optionsBackground.color = Color.white;
        optionsBackground.raycastTarget = false;

        var choicesObject = new GameObject("Choices", typeof(RectTransform));
        dialogueChoices = choicesObject.GetComponent<RectTransform>();
        dialogueChoices.SetParent(dialogueOptionsPanel.transform, false);
        dialogueChoices.anchorMin = Vector2.zero;
        dialogueChoices.anchorMax = Vector2.one;
        dialogueChoices.offsetMin = dialogueChoices.offsetMax = Vector2.zero;
        dialogueChoices.SetAsLastSibling();
        SetDialogueChoiceLayout(false);
        dialoguePage.SetActive(false);
    }

    void SetDialogueChoiceLayout(bool showChoices)
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.anchorMin = new Vector2(.01f, showChoices ? .255f : .04f);
            dialoguePanel.anchorMax = new Vector2(.89f, showChoices ? .415f : .20f);
            dialoguePanel.offsetMin = dialoguePanel.offsetMax = Vector2.zero;
        }
        if (dialogueSpeaker != null)
        {
            float frameTop = showChoices ? .415f : .20f;
            dialogueSpeaker.rectTransform.anchorMin = new Vector2(.025f, frameTop + .001f);
            dialogueSpeaker.rectTransform.anchorMax = new Vector2(.78f, frameTop + .048f);
            dialogueSpeaker.rectTransform.offsetMin = dialogueSpeaker.rectTransform.offsetMax = Vector2.zero;
        }
        if (dialogueOptionsPanel != null) dialogueOptionsPanel.SetActive(showChoices);
        if (dialogueChoices != null) dialogueChoices.gameObject.SetActive(showChoices);
    }

    void UpdateDialogue()
    {
        var node = S.ActiveDialogueNode;
        if (node == null) { S.EndDialogue(); return; }
        var choices = node.choices?.Where(choice => choice != null).ToArray() ?? new AdventureDialogueChoice[0];
        string nodeKey = S.ActiveDialogue.name + ":" + node.id + (S.ShowingDialogueResponse ? ":player:" + S.DialogueResponseText : "");
        if (renderedDialogueNode != nodeKey)
        {
            renderedDialogueNode = nodeKey;
            dialogueChoicesRevealed = false;
        string speakerName = S.ShowingDialogueResponse
                ? S.DialogueResponseSpeaker
                : !string.IsNullOrWhiteSpace(node.speaker) ? node.speaker
                : node.speakerType == AdventureDialogueSpeaker.Player
                    ? string.IsNullOrWhiteSpace(S.ActiveDialogue.playerSpeakerName) ? "Người chơi" : S.ActiveDialogue.playerSpeakerName
                    : S.ActiveDialogueNpc?.displayName ?? "";
            GameLocalization.SetText(dialogueSpeaker, string.Equals(speakerName, "Player", System.StringComparison.OrdinalIgnoreCase)
                ? "Người chơi" : speakerName);
            GameLocalization.SetText(dialogueText, S.ShowingDialogueResponse ? S.DialogueResponseText : node.text ?? "");
            dialogueText.ForceMeshUpdate();
            dialogueCharacterCount = dialogueText.textInfo.characterCount;
            dialogueVisibleCharacters = 0;
            dialogueTypewriterProgress = 0f;
            dialogueTyping = dialogueCharacterCount > 0;
            dialogueText.maxVisibleCharacters = dialogueTyping ? 0 : int.MaxValue;
            foreach (var button in dialogueChoiceButtons) if (button != null) Destroy(button.gameObject);
            dialogueChoiceButtons.Clear();
            SetDialogueChoiceLayout(false);
        }

        if (dialogueTyping)
        {
            float speed = Mathf.Max(1f, S.ActiveDialogue.charactersPerSecond);
            dialogueTypewriterProgress += Time.unscaledDeltaTime * speed;
            int visible = Mathf.Min(dialogueCharacterCount, Mathf.FloorToInt(dialogueTypewriterProgress));
            if (visible != dialogueVisibleCharacters)
            {
                dialogueVisibleCharacters = visible;
                dialogueText.maxVisibleCharacters = visible;
            }
            if (dialogueVisibleCharacters >= dialogueCharacterCount) dialogueTyping = false;
        }

        bool canShowChoices = !S.ShowingDialogueResponse && choices.Length > 0;
        if (dialogueChoicesRevealed && canShowChoices && dialogueChoiceButtons.Count == 0)
            ShowDialogueChoices(choices);
    }

    void HandleDialogueScreenClick()
    {
        if (S == null || S.Menu != AdventureMenu.Dialogue) return;
        if (dialogueTyping)
        {
            dialogueTyping = false;
            dialogueVisibleCharacters = dialogueCharacterCount;
            dialogueText.maxVisibleCharacters = int.MaxValue;
            var typingNode = S.ActiveDialogueNode;
            var typingChoices = typingNode?.choices?.Where(choice => choice != null).ToArray() ?? new AdventureDialogueChoice[0];
            if (!S.ShowingDialogueResponse && typingChoices.Length > 0)
            {
                dialogueChoicesRevealed = true;
                ShowDialogueChoices(typingChoices);
            }
            return;
        }

        var node = S.ActiveDialogueNode;
        if (node == null) return;
        if (S.ShowingDialogueResponse)
        {
            renderedDialogueNode = null;
            S.AdvanceDialogue();
            return;
        }

        var choices = node.choices?.Where(choice => choice != null).ToArray() ?? new AdventureDialogueChoice[0];
        if (choices.Length > 0)
        {
            if (!dialogueChoicesRevealed)
            {
                dialogueChoicesRevealed = true;
                ShowDialogueChoices(choices);
            }
            return;
        }

        renderedDialogueNode = null;
        S.AdvanceDialogue();
    }

    void ShowDialogueChoices(AdventureDialogueChoice[] choices)
    {
        foreach (var button in dialogueChoiceButtons) if (button != null) Destroy(button.gameObject);
        dialogueChoiceButtons.Clear();
        if (choices == null || choices.Length == 0) return;
        SetDialogueChoiceLayout(true);
        dialogueChoices.SetAsLastSibling();
        dialogueOptionsPanel.SetActive(true);
        dialogueChoices.gameObject.SetActive(true);
        float gap = choices.Length > 1 ? Mathf.Min(.04f, .12f / (choices.Length - 1)) : 0f;
        float rowHeight = Mathf.Min(.26f, (.64f - gap * (choices.Length - 1)) / choices.Length);
        float top = .82f;
        for (int i = 0; i < choices.Length; i++)
        {
            int choiceIndex = i;
            float bottom = top - rowHeight;
            var button = CreateDialogueChoiceButton(i, choices[i].text, () => S.ChooseDialogue(choiceIndex), bottom, top);
            dialogueChoiceButtons.Add(button);
            top = bottom - gap;
        }
        Canvas.ForceUpdateCanvases();
        if (EventSystem.current != null && dialogueChoiceButtons.Count > 0)
            EventSystem.current.SetSelectedGameObject(dialogueChoiceButtons[0].gameObject);
    }

    Button CreateDialogueChoiceButton(int index, string choiceText, UnityEngine.Events.UnityAction action, float bottom, float top)
    {
        var button = Instantiate(travelButtonTemplate, dialogueChoices);
        button.name = "Choice " + (index + 1);
        TravelRect(button.gameObject, dialogueChoices, new Vector2(.085f, bottom), new Vector2(.915f, top));
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        button.interactable = true;

        // The source menu button has the global hover-wipe component; a choice
        // must stay readable even before the pointer enters it.
        var wipe = button.GetComponent<UIButtonWipeReveal>();
        if (wipe != null) { wipe.enabled = false; Destroy(wipe); }
        var hoverScale = button.GetComponent<SmoothButtonHoverScale>();
        if (hoverScale != null) { hoverScale.enabled = false; Destroy(hoverScale); }
        foreach (var child in button.GetComponentsInChildren<Transform>(true))
            if (child != null && (child.name == "Wipe Reveal" || child.name == "Wipe Reveal Edge" || child.name == "Button Grunge Background"))
                Destroy(child.gameObject);

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(.018f, .025f, .04f, 0f);
            image.raycastTarget = true;
            image.fillAmount = 1f;
        }
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .89f, .67f, 1f);
        colors.selectedColor = new Color(1f, .89f, .67f, 1f);
        colors.pressedColor = new Color(.78f, .68f, .5f, 1f);
        button.colors = colors;

        var goldLine = button.transform.Find("GoldLine");
        if (goldLine != null)
        {
            var lineImage = goldLine.GetComponent<Image>();
            if (lineImage != null) lineImage.enabled = false;
        }
        var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (label != null)
        {
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(.075f, 0f);
            labelRect.anchorMax = new Vector2(.97f, 1f);
            labelRect.offsetMin = new Vector2(10f, 2f);
            labelRect.offsetMax = new Vector2(-8f, -2f);
            label.enabled = true;
            label.gameObject.SetActive(true);
            label.color = new Color(.96f, .94f, .88f, 1f);
            label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(18f, 2f, 12f, 2f);
            label.enableWordWrapping = true;
            label.enableAutoSizing = false;
            label.fontSize = Mathf.Max(label.fontSize, 28f);
            label.overflowMode = TMPro.TextOverflowModes.Overflow;
            label.raycastTarget = false;
            GameLocalization.SetText(label, choiceText ?? "");
            label.ForceMeshUpdate();
        }
        button.transform.SetAsLastSibling();
        button.gameObject.SetActive(true);
        return button;
    }
    void OpenTravel()
    {
        if (!S.CanUpgrade) return;
        foreach (Transform child in travelContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        foreach (var point in S.Data.unlockedRestPoints.Where(x => x != null).OrderBy(x => x.displayName))
        {
            var destination = point;
            bool current = point.id == S.RestPoint.Id;
            var row = TravelButton(travelContent, "Destination", point.displayName + (current ? GameLocalization.Translate(" (Hiện tại)") : ""), Vector2.zero, Vector2.one, () => {
                travelDestination = destination.id;
                GameLocalization.SetText(travelQuestion, GameLocalization.Translate("Bạn có chắc muốn dịch chuyển đến {0}?").Replace("{0}", destination.displayName));
                travelDialog.SetActive(true); travelDialog.transform.SetAsLastSibling();
            });
            var size = row.gameObject.AddComponent<LayoutElement>(); size.preferredHeight = 58; size.minHeight = 48;
            row.interactable = !current;
        }
        travelPanel.SetActive(true); travelPanel.transform.SetAsLastSibling();
    }

    void SelectMember(int direction)
    {
        if (S.Data.party.Count == 0) return;
        ChooseMember((selectedMember + direction + S.Data.party.Count) % S.Data.party.Count);
    }
    void ChooseMember(int index)
    {
        CloseSkillChange(); selectedMember = index; selectedSkill = 0; selectedSlot = 0; ResetStatDraft();
        if (restPage == 2) SelectLearnable();
    }
    void SelectLearnable()
    {
        var skills = CurrentSkills(); selectedSkill = 0;
        if (S.Data.party.Count == 0) return;
        var p = S.Data.party[selectedMember];
        int index = System.Array.FindIndex(skills, x => S.CanUnlockSkill(p, x));
        if (index < 0) index = System.Array.FindIndex(skills, x => !S.IsSkillUnlocked(p, x));
        if (index >= 0) selectedSkill = index;
    }
    public bool HandleBack()
    {
        if (S.Menu == AdventureMenu.Dialogue) { S.EndDialogue(); renderedDialogueNode = null; return true; }
        if (questDialog != null && questDialog.activeSelf) { CloseQuestDialog(); return true; }
        if (travelDialog != null && travelDialog.activeSelf) { travelDialog.SetActive(false); return true; }
        if (travelPanel != null && travelPanel.activeSelf) { CloseTravel(); return true; }
        if (skillContextOpen || skillDialogOpen) { CloseSkillChange(); return true; }
        if (S.Menu == AdventureMenu.Party || S.Menu == AdventureMenu.Inventory || S.Menu == AdventureMenu.Tasks) { CloseQuestDialog(); S.SetMenu(AdventureMenu.Main); return true; }
        if (S.Menu == AdventureMenu.Rest && restPage > 0) { restPage = 0; ResetStatDraft(); return true; }
        return false;
    }
    void Party()
    {
        var rows = view.Rows("Menu/Party/Overview/Cards" + Content, S.Data.party.Count);
        var grid = view.Component<GridLayoutGroup>("Menu/Party/Overview/Cards" + Content);
        int columns = Mathf.Clamp(S.Data.party.Count, 1, 4);
        if (grid.constraintCount != columns) grid.constraintCount = columns;
        int left = Mathf.Max(0, Mathf.RoundToInt((grid.GetComponent<RectTransform>().rect.width - grid.padding.right - columns * grid.cellSize.x - (columns - 1) * grid.spacing.x) * .5f));
        if (grid.padding.left != left) grid.padding = new RectOffset(left, grid.padding.right, grid.padding.top, grid.padding.bottom);
        while (cardStages.Count < S.Data.party.Count) {
            var go = new GameObject("Party card preview " + cardStages.Count); go.transform.SetParent(transform);
            cardStages.Add(go.AddComponent<PartyMenuStage>());
        }
        bool preparing = false;
        for (int i = 0; i < S.Data.party.Count; i++)
        {
            var p = S.Data.party[i];
            AdventureCanvasRoot.RowText(rows[i], "PlayerName", S.Template(p).unitName);
            AdventureCanvasRoot.RowText(rows[i], "Details", "Máu " + p.hp + "/" + S.MaxHP(p));
            AdventureCanvasRoot.RowText(rows[i], "Level", p.level.ToString());
            AdventureCanvasRoot.RowFill(rows[i], "HP/Fill", (float)p.hp / S.MaxHP(p));
            AdventureCanvasRoot.RowFill(rows[i], "XP/Fill", (float)p.xp / p.RequiredXP);
            AdventureCanvasRoot.RowText(rows[i], "Experience", "KN " + p.xp + " / " + p.RequiredXP);
            var assigned = S.Config.PreviewImage(S.Template(p));
            AdventureCanvasRoot.RowPortrait(rows[i], "Portrait", assigned != null ? assigned : S.Template(p).unitPortrait);
            if (assigned != null) cardStages[i].Hide();
            else if (!preparing) { cardStages[i].Show(new[] { S.Template(p) }); preparing = !cardStages[i].Ready; }
            var preview = rows[i].transform.Find("Preview").GetComponent<RawImage>();
            preview.texture = cardStages[i].Texture; preview.enabled = assigned == null && cardStages[i].ReadyFor(S.Template(p));

        }

    }
    void MemberSkills()
    {
        const string prefix = "Menu/Party/Member";
        Picker(prefix); Summary(prefix + "/Summary");
        view.Enabled("Menu/Party/Member/MoveLeft", selectedMember > 0);
        view.Enabled("Menu/Party/Member/MoveRight", selectedMember + 1 < S.Data.party.Count);
        if (S.Data.party.Count == 0) return;
        var p = S.Data.party[selectedMember]; S.EnsureLoadout(p);
        selectedSlot = Mathf.Clamp(selectedSlot, 0, S.SkillSlots - 1);
        var all = CurrentSkills(); var learned = S.LearnedSkills(p);
        var slots = view.Rows(prefix + "/Slots" + Content, S.SkillSlots, (row, i) => {
            AdventureCanvasRoot.RowClick(row, "Select", () => { selectedSlot = i; CloseSkillChange(); });
            row.AddComponent<SkillSlotPointer>().rightClick = () => OpenSkillContext(i);
            row.transform.Find("Select").gameObject.AddComponent<SkillSlotPointer>().rightClick = () => OpenSkillContext(i);
        });
        for (int i = 0; i < S.SkillSlots; i++) {
            var skill = all.FirstOrDefault(x => x.name == p.equippedSkills[i]);
            AdventureCanvasRoot.RowSelected(slots[i], selectedSlot == i);
            AdventureCanvasRoot.RowText(slots[i], "Select/Label", "Ô " + (i + 1) + " • " + (skill != null ? VietnameseGameUIText.ActionName(skill) : "Trống"));
        }
        view.Active(prefix + "/SkillContext", skillContextOpen);
        view.Active(prefix + "/SkillDialog", skillDialogOpen);
        view.Active(prefix + "/Hint", false);
        if (!skillDialogOpen) return;
        var rows = view.Rows(prefix + "/SkillDialog/Learned" + Content, learned.Length, (row, i) => AdventureCanvasRoot.RowClick(row, "Select", () => {
            var known = S.LearnedSkills(S.Data.party[selectedMember]); if (i < known.Length) pendingSkill = known[i];
        }));
        for (int i = 0; i < learned.Length; i++) {
            AdventureCanvasRoot.RowSelected(rows[i], pendingSkill == learned[i]);
            AdventureCanvasRoot.RowText(rows[i], "Select/Label", VietnameseGameUIText.ActionName(learned[i]));
            AdventureCanvasRoot.RowText(rows[i], "Description", VietnameseGameUIText.ActionDescription(learned[i]));
        }
        view.Text(prefix + "/SkillDialog/Title", "Đổi kỹ năng ô " + (selectedSlot + 1));
        view.Active(prefix + "/SkillDialog/Empty", learned.Length == 0);
        view.Enabled(prefix + "/SkillDialog/Confirm", pendingSkill != null && skillDialogMember == p);
    }
    void Inventory()
    {
        const string prefix = "Menu/Inventory";
        var stacks = S.Data.inventory;
        view.Active(prefix + "/Empty", stacks.Count == 0);
        if (!stacks.Any(x => x.id == selectedItemId)) selectedItemId = stacks.FirstOrDefault()?.id;
        int cells = Mathf.Max(16, Mathf.CeilToInt(stacks.Count / 4f) * 4);
        var rows = view.Rows(prefix + "/Items" + Content, cells, (row, i) => AdventureCanvasRoot.RowClick(row, "Select", () => { if (i < S.Data.inventory.Count) selectedItemId = S.Data.inventory[i].id; }));
        for (int i = 0; i < cells; i++)
        {
            var row = rows[i]; var stack = i < stacks.Count ? stacks[i] : null;
            var item = stack != null ? S.Config.FindItem(stack.id) : null;
            AdventureCanvasRoot.RowText(row, "Name", stack != null ? item?.displayName ?? stack.id : "");
            AdventureCanvasRoot.RowText(row, "Count", stack != null ? "x" + stack.count : "");
            AdventureCanvasRoot.RowPortrait(row, "Icon", item?.icon != null ? item.icon : item?.combatAction?.icon);
            row.transform.Find("Selected").gameObject.SetActive(false);
            AdventureCanvasRoot.RowSelected(row, stack != null && stack.id == selectedItemId);
            row.transform.Find("Select").GetComponent<Button>().interactable = stack != null;
        }
        var selected = selectedItemId != null ? S.Config.FindItem(selectedItemId) : null;
        view.Text(prefix + "/Detail/Name", selected?.displayName ?? "");
        view.Text(prefix + "/Detail/Description", selected?.description ?? "");
        view.Text(prefix + "/Detail/Count", selectedItemId != null ? "Số lượng: " + S.Count(selectedItemId) : "");
        bool canHeal = selected != null && selected.mapHeal > 0;
        view.Active(prefix + "/Detail/Use", canHeal);
        view.Text(prefix + "/Detail/Use/Label", canHeal && S.Data.party.Count > 0 ? "Hồi " + selected.mapHeal + " máu • " + S.Template(S.Data.party[selectedMember]).unitName : "Dùng");
        view.Enabled(prefix + "/Detail/Use", canHeal && S.Data.party.Count > 0 && S.Data.party[selectedMember].hp > 0 && S.Data.party[selectedMember].hp < S.MaxHP(S.Data.party[selectedMember]));
    }
    void Picker(string prefix)
    {
        bool canReorder = prefix == "Menu/Party/Member";
        var rows = view.Rows(prefix + "/Picker" + Content, S.Data.party.Count, (row, i) =>
        {
            if (canReorder)
            {
                var portrait = row.transform.Find("Portrait")?.GetComponent<Image>();
                var drag = row.AddComponent<PartyMemberDragReorder>();
                drag.Configure(i, row.GetComponent<RectTransform>(), SwapPartyMembers);
                if (portrait != null) portrait.raycastTarget = true;
                AdventureCanvasRoot.RowClick(row, "Select", () =>
                {
                    if (!drag.SuppressClick()) ChooseMember(i);
                });
            }
            else AdventureCanvasRoot.RowClick(row, "Select", () => ChooseMember(i));
        });
        for (int i = 0; i < S.Data.party.Count; i++)
        {
            AdventureCanvasRoot.RowPortrait(rows[i], "Portrait", S.Template(S.Data.party[i]).unitPortrait);
            AdventureCanvasRoot.RowText(rows[i], "Select/Label", prefix == "Menu/Party/Member" ? S.Template(S.Data.party[i]).unitName : "Chọn");
            AdventureCanvasRoot.RowSelected(rows[i], selectedMember == i);
        }
    }

    void SwapPartyMembers(int from, int to)
    {
        if (S == null || from == to || from < 0 || to < 0 || from >= S.Data.party.Count || to >= S.Data.party.Count)
            return;
        S.Swap(from, to);
        if (selectedMember == from) selectedMember = to;
        else if (selectedMember == to) selectedMember = from;
    }
    void Summary(string prefix)
    {
        view.Active(prefix, S.Data.party.Count > 0); if (S.Data.party.Count == 0) return;
        var p = S.Data.party[selectedMember]; var u = S.Template(p);
        var details = view.Node(prefix + "/Details").gameObject;
        details.SetActive(prefix != "Menu/Attributes/Summary");
        view.Text(prefix + "/Name", u.unitName);
        view.Text(prefix + "/Progress", "Cấp " + p.level + " • " + p.xp + " / " + p.RequiredXP + " KN");
        view.Fill(prefix + "/XP/Fill", (float)p.xp / p.RequiredXP);
        view.Text(prefix + "/Details", "Máu " + p.hp + " / " + S.MaxHP(p) + "\nCông " + (u.baseAtk + p.atkBonus) + "    Thủ " + (u.baseDef + p.defBonus) + "\nChí mạng " + Mathf.Clamp(u.baseCrit + p.critBonus, 0, 100) + "%\nSát thương chí mạng " + (u.baseCritDamage + p.critDamageBonus) + "%");
    }
    void Rest()
    {
        var rows = view.Rows("Menu/Rest/Members" + Content, S.Data.party.Count);
        for (int i = 0; i < S.Data.party.Count; i++)
        {
            var p = S.Data.party[i];
            AdventureCanvasRoot.RowPortrait(rows[i], "Portrait", S.Template(p).unitPortrait);
            AdventureCanvasRoot.RowText(rows[i], "Name", S.Template(p).unitName + " • " + p.hp + "/" + S.MaxHP(p) + " máu");
            AdventureCanvasRoot.RowFill(rows[i], "HP/Fill", (float)p.hp / S.MaxHP(p));
        }
    }
    void Attributes()
    {
        view.Active("Menu/Attributes/Confirm", true);
        Picker("Menu/Attributes"); Summary("Menu/Attributes/Summary");
        if (S.Data.party.Count == 0) return;
        var p = S.Data.party[selectedMember]; var u = S.Template(p);
        if (draftMember != p) { ResetStatDraft(); draftMember = p; }
        int spent = pendingStats.Sum();
        view.Text("Menu/Attributes/Points", u.unitName + " • ĐIỂM RIÊNG: " + (p.statPoints - spent) + " • ĐANG CHỌN: " + spent);
        string[] labels = { "Sinh lực", "Sức mạnh", "Phòng thủ", "Tỉ lệ chí mạng", "Sát thương chí mạng" };
        int[] values = { S.MaxHP(p), u.baseAtk + p.atkBonus, u.baseDef + p.defBonus, Mathf.Clamp(u.baseCrit + p.critBonus, 0, 100), u.baseCritDamage + p.critDamageBonus };
        int[] deltas = { 10, 2, 1, 2, 5 };
        UpdateAttributeSpiderChart(values, deltas);
        var rows = view.Rows("Menu/Attributes/Stats" + Content, 5, (row, i) => {
            AdventureCanvasRoot.RowClick(row, "Upgrade", () => {
                var member = S.Data.party[selectedMember];
                if (S.CanUpgrade && pendingStats.Sum() < member.statPoints && (i != 3 || S.Template(member).baseCrit + member.critBonus + pendingStats[3] * 2 < 100)) pendingStats[i]++;
            });
            AdventureCanvasRoot.RowClick(row, "Reduce", () => { if (pendingStats[i] > 0) pendingStats[i]--; });
            var nameRect = row.transform.Find("Name")?.GetComponent<RectTransform>();
            if (nameRect != null)
            {
                var position = nameRect.anchoredPosition;
                position.x += 16f;
                nameRect.anchoredPosition = position;
                var size = nameRect.sizeDelta;
                size.x = Mathf.Max(80f, size.x - 16f);
                nameRect.sizeDelta = size;
            }
        });
        for (int i = 0; i < 5; i++)
        {
            AdventureCanvasRoot.RowText(rows[i], "Name", labels[i]);
            AdventureCanvasRoot.RowSelected(rows[i], pendingStats[i] > 0);
            int next = values[i] + pendingStats[i] * deltas[i]; if (i == 3) next = Mathf.Min(100, next);
            AdventureCanvasRoot.RowText(rows[i], "Value", values[i] + " → " + next);
            rows[i].transform.Find("Upgrade").GetComponent<Button>().interactable = S.CanUpgrade && spent < p.statPoints && (i != 3 || next < 100);
            rows[i].transform.Find("Reduce").GetComponent<Button>().interactable = pendingStats[i] > 0;
        }
        attributeUpgradeHold.SetAvailable(S.CanUpgrade && spent > 0 && spent <= p.statPoints);
    }

    void ApplyPendingAttributeUpgrade()
    {
        if (S != null && S.Data != null && selectedMember < S.Data.party.Count && draftMember == S.Data.party[selectedMember] &&
            S.ConfirmStats(selectedMember, pendingStats)) ResetStatDraft();
    }

    void PositionAttributeConfirmUnderStats()
    {
        if (view == null) return;
        var stats = view.Component<RectTransform>("Menu/Attributes/Stats");
        var confirm = view.Component<RectTransform>("Menu/Attributes/Confirm");
        if (stats == null || confirm == null || stats.parent != confirm.parent) return;

        confirm.anchorMin = confirm.anchorMax = new Vector2(0f, 1f);
        confirm.pivot = new Vector2(.5f, 1f);
        confirm.anchoredPosition = new Vector2(
            stats.anchoredPosition.x + stats.rect.width * .5f,
            stats.anchoredPosition.y - stats.rect.height - 18f);
    }

    void UpdateAttributeSpiderChart(int[] values, int[] deltas)
    {
        const string detailsPath = "Menu/Attributes/Summary/Details";
        Transform details = view.Node(detailsPath);
        Transform summary = details.parent;
        var statsRect = view.Component<RectTransform>("Menu/Attributes/Stats");
        EnsureAttributeSummaryLayout(summary, details, view.Component<TMPro.TMP_Text>("Menu/Attributes/Summary/Name"), statsRect.anchoredPosition.y);
        Transform chartTransform = summary.Find("SpiderChart");
        if (chartTransform == null)
        {
            var chartObject = new GameObject("SpiderChart", typeof(RectTransform), typeof(CanvasRenderer), typeof(SpiderChartGraphic));
            chartTransform = chartObject.transform;
            chartTransform.SetParent(summary, false);
            var rect = (RectTransform)chartTransform;
            rect.anchorMin = rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(25, -306);
            rect.sizeDelta = new Vector2(305, 275);
            chartTransform.SetAsLastSibling();
            CreateSpiderChartLabels(chartTransform, view.Component<TMPro.TMP_Text>("Menu/Attributes/Summary/Name"));
        }

        chartTransform.gameObject.SetActive(true);
        details.gameObject.SetActive(false);
        var chart = chartTransform.GetComponent<SpiderChartGraphic>();
        if (chart == null) chart = chartTransform.gameObject.AddComponent<SpiderChartGraphic>();
        chart.raycastTarget = false;

        // The five axes follow the five upgrade rows: HP, attack, defense,
        // critical chance, and critical damage. Square-root compression keeps
        // low and high values legible while preserving their order.
        // Keep the same scale for every party member so selecting another
        // character does not rescale the chart and hide a stat difference.
        float[] axisRanges = { 300f, 100f, 60f, 100f, 300f };
        var current = new float[5];
        var preview = new float[5];
        for (int i = 0; i < 5; i++)
        {
            current[i] = Mathf.Sqrt(Mathf.Clamp01(values[i] / axisRanges[i]));
            preview[i] = Mathf.Sqrt(Mathf.Clamp01((values[i] + pendingStats[i] * deltas[i]) / axisRanges[i]));
        }
        chart.SetValues(current, preview);

        var member = S.Data.party[selectedMember];
        var unit = S.Template(member);
        view.Text("Menu/Attributes/Summary/HealthLabel", "Máu " + member.hp + "/" + S.MaxHP(member));
        view.Fill("Menu/Attributes/Summary/HealthBar/Fill", (float)member.hp / S.MaxHP(member));
        SetCombatStat("Health", S.MaxHP(member).ToString());
        SetCombatStat("AttackPower", (unit.baseAtk + member.atkBonus).ToString());
        SetCombatStat("Speed", unit.speed.ToString());
        SetCombatStat("Defense", (unit.baseDef + member.defBonus).ToString());
        SetCombatStat("CriticalRate", Mathf.Clamp(unit.baseCrit + member.critBonus, 0, 100) + "%");
    }

    void SetCombatStat(string id, string value)
    {
        view.Text("Menu/Attributes/Summary/CombatStats/" + id + "/Value", value);
    }

    static void EnsureAttributeSummaryLayout(Transform summary, Transform details, TMPro.TMP_Text style, float firstRowTopY)
    {
        var summaryRect = (RectTransform)summary;
        summaryRect.anchoredPosition = new Vector2(summaryRect.anchoredPosition.x, firstRowTopY);
        summaryRect.sizeDelta = new Vector2(355, 620);

        SetTopLeftRect(summary.Find("Name") as RectTransform, new Vector2(25, -18), new Vector2(305, 48));
        SetTopLeftRect(summary.Find("Progress") as RectTransform, new Vector2(25, -70), new Vector2(305, 28));
        SetTopLeftRect(summary.Find("XP") as RectTransform, new Vector2(25, -101), new Vector2(305, 8));
        details.gameObject.SetActive(false);

        if (summary.Find("HealthLabel") == null)
            CreateSummaryText(summary, "HealthLabel", style, "Máu", new Vector2(25, -117), new Vector2(305, 22), 15, TextAlignmentOptions.Left, Color.white);

        if (summary.Find("HealthBar") == null)
        {
            var background = new GameObject("HealthBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(summary, false);
            SetTopLeftRect(background.GetComponent<RectTransform>(), new Vector2(25, -143), new Vector2(305, 8));
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(.13f, .14f, .15f, .9f);
            backgroundImage.raycastTarget = false;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(.72f, .16f, .13f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.raycastTarget = false;
        }

        if (summary.Find("CombatStatsTitle") == null)
            CreateSummaryText(summary, "CombatStatsTitle", style, "CHỈ SỐ CHIẾN ĐẤU", new Vector2(25, -164), new Vector2(305, 24), 18, TextAlignmentOptions.Left, new Color(.89f, .73f, .48f, 1f));

        Transform stats = summary.Find("CombatStats");
        if (stats == null)
        {
            var statsObject = new GameObject("CombatStats", typeof(RectTransform));
            stats = statsObject.transform;
            stats.SetParent(summary, false);
            SetTopLeftRect((RectTransform)stats, new Vector2(25, -190), new Vector2(305, 76));
            CreateStatCell(stats, "Health", "Máu", 0, 0, 95, 99, 42, style);
            CreateStatCell(stats, "Speed", "Tốc độ", 159, 0, 83, 258, 47, style);
            CreateStatCell(stats, "AttackPower", "Lực tấn công", 0, -24, 99, 99, 42, style);
            CreateStatCell(stats, "CriticalRate", "Tỉ lệ chí mạng", 159, -24, 99, 258, 47, style);
            CreateStatCell(stats, "Defense", "Phòng thủ", 0, -48, 95, 99, 42, style);
        }

        if (summary.Find("AttributesTitle") == null)
            CreateSummaryText(summary, "AttributesTitle", style, "THUỘC TÍNH", new Vector2(25, -276), new Vector2(305, 24), 18, TextAlignmentOptions.Left, new Color(.89f, .73f, .48f, 1f));
    }

    static void CreateStatCell(Transform parent, string id, string label, float x, float y, float labelWidth, float valueX, float valueWidth, TMPro.TMP_Text style)
    {
        Transform row = parent.Find(id);
        if (row == null)
        {
            var go = new GameObject(id, typeof(RectTransform));
            row = go.transform;
            row.SetParent(parent, false);
        }
        SetTopLeftRect((RectTransform)row, new Vector2(x, y), new Vector2(valueX + valueWidth - x, 23));
        CreateSummaryText(row, "Label", style, label, Vector2.zero, new Vector2(labelWidth, 23), 13, TMPro.TextAlignmentOptions.Left, new Color(.88f, .86f, .80f, 1f));
        CreateSummaryText(row, "Value", style, "0", new Vector2(valueX - x, 0), new Vector2(valueWidth, 23), 14, TMPro.TextAlignmentOptions.Right, Color.white);
    }

    static TMPro.TMP_Text CreateSummaryText(Transform parent, string path, TMPro.TMP_Text style, string source, Vector2 position, Vector2 size, float fontSize, TMPro.TextAlignmentOptions alignment, Color color)
    {
        Transform existing = parent.Find(path);
        TMPro.TMP_Text text;
        if (existing != null) text = existing.GetComponent<TMPro.TMP_Text>();
        else
        {
            var go = new GameObject(path, typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            text = go.GetComponent<TMPro.TextMeshProUGUI>();
        }
        SetTopLeftRect((RectTransform)text.transform, position, size);
        text.font = style.font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        GameLocalization.SetText(text, source);
        return text;
    }

    static void SetTopLeftRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = Vector2.up;
        rect.pivot = Vector2.up;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void CreateSpiderChartLabels(Transform chart, TMPro.TMP_Text style)
    {
        string[] labels = { "Sinh lực", "Sức mạnh", "Phòng thủ", "Tỉ lệ chí mạng", "Sát thương chí mạng" };
        Vector2[] positions = { new Vector2(0, 77), new Vector2(91, 31), new Vector2(86, -61), new Vector2(-86, -61), new Vector2(-80, 31) };
        for (int i = 0; i < labels.Length; i++)
        {
            var labelObject = new GameObject("Axis " + labels[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            labelObject.transform.SetParent(chart, false);
            var rect = (RectTransform)labelObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = positions[i];
            rect.sizeDelta = new Vector2(i == 0 ? 105 : i == 4 ? 145 : 118, 22);
            var text = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
            text.font = style.font;
            text.fontSize = Mathf.Min(15, style.fontSize * .8f);
            text.color = new Color(.88f, .86f, .80f, 1f);
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            GameLocalization.SetText(text, labels[i]);
        }

        CreateSpiderLegend(chart, style, "Hiện tại", new Vector2(-48, -91), new Color(.91f, .72f, .40f, 1f));
        CreateSpiderLegend(chart, style, "Dự kiến", new Vector2(52, -91), new Color(.29f, .82f, 1f, 1f));
    }

    static void CreateSpiderLegend(Transform chart, TMPro.TMP_Text style, string label, Vector2 position, Color tint)
    {
        var go = new GameObject("Legend " + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        go.transform.SetParent(chart, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(88, 18);
        var text = go.GetComponent<TMPro.TextMeshProUGUI>();
        text.font = style.font;
        text.fontSize = Mathf.Min(12, style.fontSize * .66f);
        text.color = tint;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.raycastTarget = false;
        GameLocalization.SetText(text, label);
    }
    ActionData[] CurrentSkills() => S.Data.party.Count == 0 ? new ActionData[0] : S.Template(S.Data.party[selectedMember]).characterSkills.Where(x => x != null).Distinct().ToArray();
    void Skills()
    {
        Picker("Menu/Skills"); Summary("Menu/Skills/Summary");
        var skills = CurrentSkills();
        view.Active("Menu/Skills/Empty", skills.Length == 0); view.Active("Menu/Skills/Detail", skills.Length > 0);
        selectedSkill = Mathf.Clamp(selectedSkill, 0, Mathf.Max(0, skills.Length - 1));
        var rows = view.Rows("Menu/Skills/Nodes" + Content, skills.Length, (row, i) => AdventureCanvasRoot.RowClick(row, "Select", () => selectedSkill = i));
        if (S.Data.party.Count == 0) return;
        var p = S.Data.party[selectedMember]; view.Text("Menu/Skills/Points", "ĐIỂM KỸ NĂNG : " + p.skillPoints);
        if (skills.Length == 0) view.Text("Menu/Skills/Empty", "Chưa học kỹ năng nào.");
        for (int i = 0; i < skills.Length; i++)
        {
            AdventureCanvasRoot.RowText(rows[i], "Rank", S.IsSkillUnlocked(p, skills[i]) ? "Đã mở" : S.SkillCost(skills[i]) + " điểm");
            AdventureCanvasRoot.RowText(rows[i], "Select/Label", "");
            AdventureCanvasRoot.RowPortrait(rows[i], "Icon", skills[i].icon);
            rows[i].transform.Find("Icon").GetComponent<Image>().color = S.IsSkillUnlocked(p, skills[i]) ? Color.white : new Color(.4f, .4f, .4f, 1);
            AdventureCanvasRoot.RowText(rows[i], "Name", VietnameseGameUIText.ActionName(skills[i]));
            AdventureCanvasRoot.RowSelected(rows[i], selectedSkill == i);
        }
        if (skills.Length == 0) return;
        var skill = skills[selectedSkill];
        view.Text("Menu/Skills/Detail/Name", VietnameseGameUIText.ActionName(skill));
        bool unlocked = S.IsSkillUnlocked(p, skill);
        var required = S.Prerequisites(p, skill).Where(x => !S.IsSkillUnlocked(p, x)).ToArray();
        view.Text("Menu/Skills/Detail/Description", VietnameseGameUIText.ActionDescription(skill) + (required.Length > 0 ? "\nCần mở: " + string.Join(", ", required.Select(VietnameseGameUIText.ActionName)) : ""));
        view.Text("Menu/Skills/Detail/Upgrade/Label", unlocked ? "Đã mở khóa" : "Mở khóa • " + S.SkillCost(skill) + " điểm");
        view.Enabled("Menu/Skills/Detail/Upgrade", S.CanUnlockSkill(p, skill));
        view.Text("Menu/Skills/Reason", S.UnlockBlockedReason(p, skill));
    }
    void Craft()
    {
        var recipes = S.Config.recipes;
        var rows = view.Rows("Menu/Craft/Recipes" + Content, recipes.Count, (row, i) => AdventureCanvasRoot.RowClick(row, "Craft", () => { if (i < S.Config.recipes.Count) S.Craft(S.Config.recipes[i]); }));
        for (int i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            AdventureCanvasRoot.RowText(rows[i], "Name", recipe.displayName + " ×" + recipe.output.count);
            AdventureCanvasRoot.RowText(rows[i], "Ingredients", string.Join(" + ", recipe.ingredients.Select(x => (S.Config.FindItem(x.id)?.displayName ?? x.id) + " " + S.Count(x.id) + "/" + x.count)));
            rows[i].transform.Find("Craft").GetComponent<Button>().interactable = S.CanCraft(recipe);
        }
    }
}
