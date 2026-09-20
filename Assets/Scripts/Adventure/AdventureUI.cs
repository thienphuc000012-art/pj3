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
            restPage = 0; selectedSkill = 0; CloseSkillChange(); previousMenu = S.Menu;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        selectedMember = Mathf.Clamp(selectedMember, 0, Mathf.Max(0, S.Data.party.Count - 1));
        bool menuOpen = S.Menu != AdventureMenu.None;
        if (S.Menu != AdventureMenu.Main) foreach (var card in cardStages) card.Hide();
        if (menuOpen && !skillDialogOpen && !skillContextOpen && S.Menu != AdventureMenu.Inventory && S.Menu != AdventureMenu.Main && Input.GetKeyDown(KeyCode.Q)) SelectMember(-1);
        if (menuOpen && !skillDialogOpen && !skillContextOpen && S.Menu != AdventureMenu.Inventory && S.Menu != AdventureMenu.Main && Input.GetKeyDown(KeyCode.R)) SelectMember(1);
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
            if (S.Nearest != null) view.Text("HUD/Interact/Label", "[E] " + S.Nearest.displayName);
            return;
        }
        bool main = S.Menu == AdventureMenu.Main, party = S.Menu == AdventureMenu.Party, rest = S.Menu == AdventureMenu.Rest;
        bool detail = rest && (restPage == 1 || restPage == 2);
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
        string page = main || party ? "Party" : S.Menu == AdventureMenu.Inventory ? "Inventory" : restPage == 1 ? "Attributes" : restPage == 2 ? "Skills" : restPage == 3 ? "Craft" : "Rest";
        if (page != "Attributes") ResetStatDraft();
        foreach (string name in new[] { "Party", "Inventory", "Rest", "Attributes", "Skills", "Craft" }) view.Active("Menu/" + name, name == page);
        view.Text("Menu/Title", main ? "MAIN MENU" : party ? "PARTY" : page == "Inventory" ? "RƯƠNG ĐỒ" : page == "Attributes" ? "THUỘC TÍNH" : page == "Skills" ? "KỸ NĂNG" : page == "Craft" ? "CHẾ TẠO" : "ĐIỂM NGHỈ");
        view.Text("Menu/Subtitle", rest ? S.RestPoint?.displayName ?? "Nghỉ ngơi bên hành trình" : "Mỗi thành viên mang theo một câu chuyện.");
        view.Text("Menu/Back/Label", (party || S.Menu == AdventureMenu.Inventory) ? "[Esc] Main Menu" : rest && restPage > 0 ? "← Điểm nghỉ" : "[Esc] Trở lại");
        view.Text("Menu/Hints", rest ? "Q / R Đổi thành viên • Nâng cấp chỉ khả dụng tại điểm nghỉ" : main ? "P / Esc Đóng menu" : S.Menu == AdventureMenu.Inventory ? "Chọn vật phẩm • Esc Main Menu • P Đóng menu" : "Q / R Đổi thành viên • Esc Main Menu • P Đóng menu");
        if (page == "Party") {
            view.Active("Menu/Party/Overview", main); view.Active("Menu/Party/Member", party);
            if (main) Party(); else MemberSkills();
        }
        else if (page == "Inventory") Inventory();
        else if (page == "Rest") Rest();
        else if (page == "Attributes") Attributes();
        else if (page == "Skills") Skills();
        else Craft();
    }
    void Bind()
    {
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
        if (skillContextOpen || skillDialogOpen) { CloseSkillChange(); return true; }
        if (S.Menu == AdventureMenu.Party || S.Menu == AdventureMenu.Inventory) { S.SetMenu(AdventureMenu.Main); return true; }
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
        view.Text(prefix + "/Hint", "Click trái chọn ô • Click phải để thay đổi kỹ năng • Tự điền kỹ năng đã học vào ô trống");
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
        view.Text(prefix + "/Detail/Name", selected?.displayName ?? "Chọn vật phẩm");
        view.Text(prefix + "/Detail/Description", selected?.description ?? "Click một ô để xem thông tin vật phẩm.");
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
