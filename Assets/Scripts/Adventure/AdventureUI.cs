using System.Linq;
using System.Collections.Generic;
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
    RectTransform questContent, dialogueChoices;
    TMPro.TMP_Text questPrompt, dialogueSpeaker, dialogueText;
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
        view.gameObject.SetActive(!S.Busy);
        if (S.Busy) return;
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
                view.Text("HUD/Leader/Progress", p.xp + " / " + p.RequiredXP + " EXP");
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
        if (page != "Attributes") ResetStatDraft();
        foreach (string name in new[] { "Party", "Inventory", "Rest", "Attributes", "Skills", "Craft", "Tasks", "Dialogue" }) view.Active("Menu/" + name, name == page);
        if (!dialogue)
        {
            view.Text("Menu/Title", main ? "MAIN MENU" : tasks ? "NHIỆM VỤ" : party ? "PARTY" : page == "Inventory" ? "RƯƠNG ĐỒ" : page == "Attributes" ? "THUỘC TÍNH" : page == "Skills" ? "KỸ NĂNG" : page == "Craft" ? "CHẾ TẠO" : "ĐIỂM NGHỈ");
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
        BuildDialogueUI();
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
        view.Click("Menu/Attributes/Confirm", () => { if (selectedMember < S.Data.party.Count && draftMember == S.Data.party[selectedMember] && S.ConfirmStats(selectedMember, pendingStats)) ResetStatDraft(); });
        view.Click("Menu/Attributes/Cancel", ResetStatDraft);
        view.Click("Menu/Skills/Detail/Upgrade", () =>
        {
            var skills = CurrentSkills();
            if (selectedSkill < skills.Length) S.UnlockSkill(selectedMember, skills[selectedSkill]);
        });
    }
    void ClearInstructionText(string text)
    {
        foreach (var label in view.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if (label != null && string.Equals(label.text.Trim(), text, System.StringComparison.Ordinal))
                label.text = "";
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
        buttonLabel.text = label;
        buttonLabel.enableWordWrapping = true;
        button.gameObject.SetActive(true); return button;
    }
    TMPro.TMP_Text TravelText(Transform parent, string name, string text, Vector2 min, Vector2 max)
    {
        var source = travelButtonTemplate.GetComponentInChildren<TMPro.TMP_Text>(true);
        var label = Instantiate(source, parent); label.name = name;
        TravelRect(label.gameObject, parent, min, max);
        label.text = text; label.raycastTarget = false;
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
        entry.GetComponentInChildren<TMPro.TMP_Text>(true).text = "Di chuyển nhanh";
        entry.onClick = new Button.ButtonClickedEvent(); entry.onClick.AddListener(OpenTravel);
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

    void BuildQuestUI()
    {
        var overview = view.Node("Menu/Party/Overview");
        var inventory = view.Component<Button>("Menu/Party/Overview/Inventory");
        var taskButton = Instantiate(inventory, overview);
        taskButton.name = "Quests";
        ((RectTransform)taskButton.transform).anchoredPosition = ((RectTransform)inventory.transform).anchoredPosition + Vector2.down * 88f;
        taskButton.onClick = new Button.ButtonClickedEvent();
        taskButton.onClick.AddListener(() => S.SetMenu(AdventureMenu.Tasks));
        taskButton.GetComponentInChildren<TMPro.TMP_Text>(true).text = "Nhiệm vụ";

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
            string status = progress.completed ? "Hoàn thành" : progress.objectiveCount + " / " + Mathf.Max(1, quest.requiredCount);
            if (!progress.completed && S.Data.trackedQuestId == progress.questId) status += " • Đang theo dõi";
            var button = TravelButton(questContent, "Quest " + quest.Id, quest.displayName + "\n" + status,
                Vector2.zero, Vector2.one, () => OpenQuestDialog(quest));
            var size = button.gameObject.AddComponent<LayoutElement>(); size.preferredHeight = 72f; size.minHeight = 60f;
            button.interactable = !progress.completed;
        }
    }

    void OpenQuestDialog(AdventureQuest quest)
    {
        selectedQuest = quest;
        var progress = quest != null ? S.QuestProgress(quest.Id) : null;
        questPrompt.text = quest == null ? "Theo dõi nhiệm vụ?" :
            quest.displayName + "\n\n" + quest.description + "\n\nTiến độ: " + (progress?.objectiveCount ?? 0) + " / " + Mathf.Max(1, quest.requiredCount) + "\n\nTheo dõi nhiệm vụ?";
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

        var panel = TravelPanel("Dialogue Panel", dialoguePage.transform, new Vector2(.045f, .035f), new Vector2(.9f, .45f));
        panel.GetComponent<Image>().color = new Color(.015f, .02f, .025f, .82f);
        var speakerPanel = TravelPanel("Speaker Region", panel.transform, new Vector2(.025f, .76f), new Vector2(.975f, .96f));
        speakerPanel.GetComponent<Image>().color = new Color(.09f, .075f, .045f, .92f);
        dialogueSpeaker = TravelText(speakerPanel.transform, "Speaker", "", new Vector2(.035f, .08f), new Vector2(.94f, .92f));
        dialogueSpeaker.fontSize *= 1.2f;
        var linePanel = TravelPanel("Dialogue Text Region", panel.transform, new Vector2(.025f, .34f), new Vector2(.975f, .72f));
        linePanel.GetComponent<Image>().color = new Color(.005f, .008f, .012f, .78f);
        dialogueText = TravelText(linePanel.transform, "Line", "", new Vector2(.025f, .04f), new Vector2(.96f, .96f));
        dialogueText.alignment = TMPro.TextAlignmentOptions.TopLeft;
        dialogueText.margin = new Vector4(16f, 8f, 16f, 8f);
        dialogueChoices = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        dialogueChoices.SetParent(panel.transform, false);
        dialogueChoices.anchorMin = new Vector2(.035f, .035f); dialogueChoices.anchorMax = new Vector2(.965f, .31f);
        dialogueChoices.offsetMin = dialogueChoices.offsetMax = Vector2.zero;
        var choicesLayout = dialogueChoices.GetComponent<VerticalLayoutGroup>();
        choicesLayout.spacing = 4f; choicesLayout.childControlHeight = true; choicesLayout.childControlWidth = true;
        choicesLayout.childForceExpandHeight = false; choicesLayout.childForceExpandWidth = true;
        dialogueChoices.gameObject.SetActive(false);
        dialoguePage.SetActive(false);
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
            dialogueSpeaker.text = S.ShowingDialogueResponse
                ? S.DialogueResponseSpeaker
                : !string.IsNullOrWhiteSpace(node.speaker) ? node.speaker
                : node.speakerType == AdventureDialogueSpeaker.Player
                    ? string.IsNullOrWhiteSpace(S.ActiveDialogue.playerSpeakerName) ? "Player" : S.ActiveDialogue.playerSpeakerName
                    : S.ActiveDialogueNpc?.displayName ?? "";
            dialogueText.text = S.ShowingDialogueResponse ? S.DialogueResponseText : node.text ?? "";
            dialogueText.ForceMeshUpdate();
            dialogueCharacterCount = dialogueText.textInfo.characterCount;
            dialogueVisibleCharacters = 0;
            dialogueTypewriterProgress = 0f;
            dialogueTyping = dialogueCharacterCount > 0;
            dialogueText.maxVisibleCharacters = dialogueTyping ? 0 : int.MaxValue;
            foreach (var button in dialogueChoiceButtons) if (button != null) Destroy(button.gameObject);
            dialogueChoiceButtons.Clear();
            dialogueChoices.gameObject.SetActive(false);
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
        dialogueChoices.gameObject.SetActive(true);
        for (int i = 0; i < choices.Length; i++)
        {
            int choiceIndex = i;
            var button = TravelButton(dialogueChoices, "Choice " + (i + 1), choices[i].text, Vector2.zero, Vector2.one,
                () => S.ChooseDialogue(choiceIndex));
            var size = button.gameObject.AddComponent<LayoutElement>(); size.preferredHeight = 42f; size.minHeight = 36f;
            dialogueChoiceButtons.Add(button);
        }
    }
    void OpenTravel()
    {
        if (!S.CanUpgrade) return;
        foreach (Transform child in travelContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        foreach (var point in S.Data.unlockedRestPoints.Where(x => x != null).OrderBy(x => x.displayName))
        {
            var destination = point;
            bool current = point.id == S.RestPoint.Id;
            var row = TravelButton(travelContent, "Destination", point.displayName + (current ? " (Hiện tại)" : ""), Vector2.zero, Vector2.one, () => {
                travelDestination = destination.id;
                travelQuestion.text = "Bạn có chắc muốn dịch chuyển đến " + destination.displayName + "?";
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
            AdventureCanvasRoot.RowText(rows[i], "Details", "HP " + p.hp + "/" + S.MaxHP(p));
            AdventureCanvasRoot.RowText(rows[i], "Level", p.level.ToString());
            AdventureCanvasRoot.RowFill(rows[i], "HP/Fill", (float)p.hp / S.MaxHP(p));
            AdventureCanvasRoot.RowFill(rows[i], "XP/Fill", (float)p.xp / p.RequiredXP);
            AdventureCanvasRoot.RowText(rows[i], "Experience", "EXP " + p.xp + " / " + p.RequiredXP);
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
            AdventureCanvasRoot.RowText(slots[i], "Select/Label", "Ô " + (i + 1) + " • " + (skill != null ? skill.actionName : "Trống"));
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
            AdventureCanvasRoot.RowText(rows[i], "Select/Label", learned[i].actionName);
            AdventureCanvasRoot.RowText(rows[i], "Description", learned[i].description ?? "");
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
        view.Text(prefix + "/Detail/Use/Label", canHeal && S.Data.party.Count > 0 ? "Hồi " + selected.mapHeal + " HP • " + S.Template(S.Data.party[selectedMember]).unitName : "Dùng");
        view.Enabled(prefix + "/Detail/Use", canHeal && S.Data.party.Count > 0 && S.Data.party[selectedMember].hp > 0 && S.Data.party[selectedMember].hp < S.MaxHP(S.Data.party[selectedMember]));
    }
    void Picker(string prefix)
    {
        var rows = view.Rows(prefix + "/Picker" + Content, S.Data.party.Count, (row, i) => AdventureCanvasRoot.RowClick(row, "Select", () => ChooseMember(i)));
        for (int i = 0; i < S.Data.party.Count; i++)
        {
            AdventureCanvasRoot.RowPortrait(rows[i], "Portrait", S.Template(S.Data.party[i]).unitPortrait);
            AdventureCanvasRoot.RowText(rows[i], "Select/Label", prefix == "Menu/Party/Member" ? S.Template(S.Data.party[i]).unitName : "Chọn");
            AdventureCanvasRoot.RowSelected(rows[i], selectedMember == i);
        }
    }
    void Summary(string prefix)
    {
        view.Active(prefix, S.Data.party.Count > 0); if (S.Data.party.Count == 0) return;
        var p = S.Data.party[selectedMember]; var u = S.Template(p);
        view.Text(prefix + "/Name", u.unitName);
        view.Text(prefix + "/Progress", "Cấp " + p.level + " • " + p.xp + " / " + p.RequiredXP + " EXP");
        view.Fill(prefix + "/XP/Fill", (float)p.xp / p.RequiredXP);
        view.Text(prefix + "/Details", "HP " + p.hp + " / " + S.MaxHP(p) + "\nATK " + (u.baseAtk + p.atkBonus) + "    DEF " + (u.baseDef + p.defBonus) + "\nCrit " + Mathf.Clamp(u.baseCrit + p.critBonus, 0, 100) + "%\nSát thương crit " + (u.baseCritDamage + p.critDamageBonus) + "%");
    }
    void Rest()
    {
        var rows = view.Rows("Menu/Rest/Members" + Content, S.Data.party.Count);
        for (int i = 0; i < S.Data.party.Count; i++)
        {
            var p = S.Data.party[i];
            AdventureCanvasRoot.RowPortrait(rows[i], "Portrait", S.Template(p).unitPortrait);
            AdventureCanvasRoot.RowText(rows[i], "Name", S.Template(p).unitName + " • " + p.hp + "/" + S.MaxHP(p));
            AdventureCanvasRoot.RowFill(rows[i], "HP/Fill", (float)p.hp / S.MaxHP(p));
        }
    }
    void Attributes()
    {
        Picker("Menu/Attributes"); Summary("Menu/Attributes/Summary");
        if (S.Data.party.Count == 0) return;
        var p = S.Data.party[selectedMember]; var u = S.Template(p);
        if (draftMember != p) { ResetStatDraft(); draftMember = p; }
        int spent = pendingStats.Sum();
        view.Text("Menu/Attributes/Points", u.unitName + " • ĐIỂM RIÊNG: " + (p.statPoints - spent) + " • ĐANG CHỌN: " + spent);
        string[] labels = { "Sinh lực", "Sức mạnh", "Phòng thủ", "Tỉ lệ chí mạng", "Sát thương chí mạng" };
        int[] values = { S.MaxHP(p), u.baseAtk + p.atkBonus, u.baseDef + p.defBonus, Mathf.Clamp(u.baseCrit + p.critBonus, 0, 100), u.baseCritDamage + p.critDamageBonus };
        int[] deltas = { 10, 2, 1, 2, 5 };
        var rows = view.Rows("Menu/Attributes/Stats" + Content, 5, (row, i) => {
            AdventureCanvasRoot.RowClick(row, "Upgrade", () => {
                var member = S.Data.party[selectedMember];
                if (S.CanUpgrade && pendingStats.Sum() < member.statPoints && (i != 3 || S.Template(member).baseCrit + member.critBonus + pendingStats[3] * 2 < 100)) pendingStats[i]++;
            });
            AdventureCanvasRoot.RowClick(row, "Reduce", () => { if (pendingStats[i] > 0) pendingStats[i]--; });
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
        view.Enabled("Menu/Attributes/Confirm", S.CanUpgrade && spent > 0 && spent <= p.statPoints);
        view.Enabled("Menu/Attributes/Cancel", spent > 0);
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
            AdventureCanvasRoot.RowText(rows[i], "Name", skills[i].actionName);
            AdventureCanvasRoot.RowSelected(rows[i], selectedSkill == i);
        }
        if (skills.Length == 0) return;
        var skill = skills[selectedSkill];
        view.Text("Menu/Skills/Detail/Name", skill.actionName);
        bool unlocked = S.IsSkillUnlocked(p, skill);
        var required = S.Prerequisites(p, skill).Where(x => !S.IsSkillUnlocked(p, x)).ToArray();
        view.Text("Menu/Skills/Detail/Description", skill.description + (required.Length > 0 ? "\nCần mở: " + string.Join(", ", required.Select(x => x.actionName)) : ""));
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
