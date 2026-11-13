using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal enum GuidedCombatPractice { None, Attack, Skill }

/// <summary>One battle's lessons, presented at safe points between combat actions.</summary>
internal sealed class GuidedCombatTutorial
{
    const int LessonCount = 8;
    readonly GuidedCombatTutorialView view;
    int shownLessons;
    bool openingShown;
    bool attackCompleted;
    bool parryShown;
    bool skillCompleted;
    bool stainLessonShown;
    bool partyHudLessonShown;
    bool closed;
    bool presentingCommands;
    int dismissedFrame = -1;

    GuidedCombatTutorial(GuidedCombatTutorialView view) => this.view = view;

    public bool BlocksInput => view != null && (presentingCommands || view.IsOpen || Time.frameCount == dismissedFrame);
    public bool NeedsOpeningLessons => !closed && !openingShown;
    public GuidedCombatPractice PendingPractice { get; private set; }

    public static GuidedCombatTutorial Create(AdvancedUIManager ui)
    {
        if (ui == null) return null;
        var menu = ui.actionMenuPanel;
        var canvas = menu != null ? menu.GetComponentInParent<Canvas>(true) : ui.GetComponentInParent<Canvas>(true);
        if (canvas == null) return null;
        var font = menu != null ? menu.GetComponentInChildren<TMP_Text>(true)?.font : null;
        var view = GuidedCombatTutorialView.Create(canvas.rootCanvas, font);
        return view != null ? new GuidedCombatTutorial(view) : null;
    }

    public IEnumerator ShowPlayerLessons(AdvancedUIManager ui, bool canPracticeAttack, bool canPracticeSkill)
    {
        bool teachAttack = canPracticeAttack && !attackCompleted;
        // Either side can act next. Skills must not wait for an enemy/parry turn.
        bool teachSkills = canPracticeSkill && attackCompleted && !skillCompleted;
        bool teachPartyHud = skillCompleted && parryShown && !partyHudLessonShown;
        if (ui == null || view == null || closed || presentingCommands ||
            PendingPractice != GuidedCombatPractice.None ||
            (!NeedsOpeningLessons && !teachAttack && !teachSkills && !teachPartyHud)) yield break;

        // Let the existing menu finish its stagger and select its initial button
        // before the modal takes keyboard focus and frames the actual controls.
        presentingCommands = true;
        try
        {
            yield return ShowCommandLessons(ui, teachAttack, teachSkills, teachPartyHud);
        }
        finally
        {
            presentingCommands = false;
        }
    }

    IEnumerator ShowCommandLessons(AdvancedUIManager ui, bool teachAttack, bool teachSkills, bool teachPartyHud)
    {
        yield return null;
        yield return new WaitForSecondsRealtime(CommandEntranceTime(ui.actionMenuPanel));
        if (closed || view == null) yield break;

        var menu = ui.actionMenuPanel;
        var buttons = menu != null ? menu.GetComponentsInChildren<Button>(false) : new Button[0];
        if (NeedsOpeningLessons)
        {
            yield return Show(buttons.Select(button => (RectTransform)button.transform).ToArray(),
                "Lệnh chiến đấu", "Battle commands",
                "Đến lượt mình, nhân vật có thể tấn công, dùng kỹ năng hoặc dùng vật phẩm. Hãy xem lượng Tinh lực trước khi chọn kỹ năng.",
                "On their turn, a character can attack, use a skill, or use an item. Check your Stain before selecting a skill.");
            var order = ui.turnOrderContainer;
            var portraits = order == null ? new RectTransform[0] : order.Cast<Transform>()
                .Where(child => child.gameObject.activeInHierarchy).OfType<RectTransform>().ToArray();
            yield return Show(portraits,
                "Thứ tự lượt", "Turn order",
                "Thanh bên trái hiển thị thứ tự hành động. Ảnh nổi bật cho biết ai đang đến lượt. Hãy nhìn các ảnh tiếp theo để biết lúc nào địch sẽ đánh.",
                "The portraits on the left show who acts next. The highlighted portrait is the current turn. Watch the next portraits to anticipate the enemy's attack.");
            yield return Show(GetElementFocus(ui),
                "Sát thương khắc hệ", "Elemental damage",
                ElementLessonBody(false), ElementLessonBody(true));
            openingShown = true;
        }
        if (teachAttack)
        {
            var attack = FindCommand(buttons, nameof(CombatManager.OnAttackClicked));
            if (attack != null)
                yield return Show(new[] { (RectTransform)attack.transform },
                    "Tấn công thường", "Basic attack",
                    "Bấm nút <b>Tấn công</b> đang sáng, rồi chọn và xác nhận mục tiêu để ra đòn. Bạn cần thực hiện đòn đánh để hoàn thành bài.",
                    "Choose the highlighted <b>Attack</b> button, then select and confirm a target. Perform the attack to finish the lesson.",
                    attack, GuidedCombatPractice.Attack);
        }
        else if (teachSkills)
        {
            var skills = FindCommand(buttons, nameof(CombatManager.OnSkillsClicked));
            if (skills != null)
            {
                yield return Show(new[] { (RectTransform)skills.transform },
                    "Kỹ năng", "Skills",
                    "Bấm nút <b>Kỹ năng</b> đang sáng để mở danh sách chiêu. Chọn một chiêu có thể dùng và thực hiện để hoàn thành bài.",
                    "Choose the highlighted <b>Skills</b> button to open the skill list. Select an available skill and use it to finish the lesson.",
                    skills, GuidedCombatPractice.Skill);

                if (PendingPractice == GuidedCombatPractice.Skill && !stainLessonShown)
                {
                    var stainFocus = GetStainFocus(ui);
                    yield return Show(stainFocus,
                        "Tiêu hao Stain", "Stain Cost",
                        "Dùng kỹ năng sẽ tiêu hao <b>Stain</b>. Hãy kiểm tra các biểu tượng Stain trước khi chọn chiêu.",
                        "Skills consume <b>Stain</b>. Check the Stain meter before choosing a skill.");
                    stainLessonShown = true;
                }
            }
        }

        if (teachPartyHud && !closed && view != null)
        {
            yield return Show(GetPartyHudFocus(ui),
                "Máu và Tuyệt kỹ", "Health & Ultimate",
                "HP về 0 thì cả đội thua trận. Khi thanh năng lượng Ultimate đầy, nhân vật có thể dùng Tuyệt kỹ.",
                "If HP reaches zero, the party loses. When the Ultimate energy bar is full, that character can use an Ultimate.",
                allowCancel: true);
            partyHudLessonShown = true;
        }
    }

    static IReadOnlyList<RectTransform> GetElementFocus(AdvancedUIManager ui)
    {
        if (ui == null || ui.partyHUDList == null) return new RectTransform[0];
        return ui.partyHUDList
            .Where(hud => hud != null && hud.gameObject != null && hud.gameObject.activeInHierarchy &&
                hud.elementIcon != null && hud.elementIcon.gameObject.activeInHierarchy)
            .Select(hud => hud.elementIcon.rectTransform)
            .ToArray();
    }

    static string ElementLessonBody(bool english)
    {
        var combat = CombatManager.Instance;
        var culture = System.Globalization.CultureInfo.GetCultureInfo(english ? "en-US" : "vi-VN");
        string advantage = (combat != null ? combat.elementalAdvantageMultiplier : 2f).ToString("0.##", culture);
        string same = (combat != null ? combat.elementalSameTypeMultiplier : 1.5f).ToString("0.##", culture);
        string disadvantage = (combat != null ? combat.elementalDisadvantageMultiplier : .5f).ToString("0.##", culture);
        return english
            ? "The icons beside party HP show each character's <b>defensive element</b>. Skills have their own element.\n\n" +
                $"Compare the <b>skill's element</b> with the target's element:\n" +
                $"<b>Advantage: ×{advantage}</b> damage\n<b>Same element: ×{same}</b> damage\n" +
                $"<b>Disadvantage: ×{disadvantage}</b> damage\n\nChoose a skill that counters the enemy. " +
                "Enemies can also exploit your party's elements."
            : "Icon cạnh HP cho biết <b>hệ phòng thủ</b> của từng thành viên. Mỗi kỹ năng có hệ riêng.\n\n" +
                $"So sánh <b>hệ của kỹ năng</b> với hệ của mục tiêu:\n" +
                $"<b>Khắc hệ: ×{advantage}</b> sát thương\n<b>Cùng hệ: ×{same}</b> sát thương\n" +
                $"<b>Bị khắc hệ: ×{disadvantage}</b> sát thương\n\nChọn kỹ năng khắc hệ địch để tăng sát thương. " +
                "Địch cũng có thể khắc hệ thành viên trong đội.";
    }

    static IReadOnlyList<RectTransform> GetStainFocus(AdvancedUIManager ui)
    {
        if (ui == null || ui.stainIcons == null) return new RectTransform[0];
        var icons = ui.stainIcons
            .Where(icon => icon != null && icon.gameObject.activeInHierarchy)
            .Select(icon => icon.rectTransform)
            .ToArray();
        if (icons.Length > 0) return icons;
        return ui.stainIcons.FirstOrDefault(icon => icon != null)?.transform.parent is RectTransform parent
            ? new[] { parent }
            : new RectTransform[0];
    }

    static IReadOnlyList<RectTransform> GetPartyHudFocus(AdvancedUIManager ui)
    {
        if (ui == null || ui.partyHUDList == null) return new RectTransform[0];
        return ui.partyHUDList
            .Where(hud => hud != null && hud.gameObject != null && hud.gameObject.activeInHierarchy)
            .Select(hud => hud.gameObject.GetComponent<RectTransform>())
            .Where(rect => rect != null)
            .ToArray();
    }

    static float CommandEntranceTime(RectTransform menu)
    {
        if (menu == null) return 0f;
        var transition = menu.GetComponent<CombatActionMenuTransition>();
        var stagger = menu.GetComponent<BattleMenuStagger>();
        var slides = menu.GetComponentsInChildren<BattleMenuButtonSlide>(false);
        float panelTime = transition != null ? transition.showDuration : 0f;
        float buttonTime = slides.Length > 0 ? slides.Max(slide => slide.entryDuration) : 0f;
        if (stagger != null)
            buttonTime += Mathf.Max(0, slides.Length - 1) * stagger.staggerInterval;
        return Mathf.Max(panelTime, buttonTime) + .04f;
    }

    public IEnumerator ShowParryLesson(AdvancedUIManager ui, int targetIndex)
    {
        if (!attackCompleted || parryShown || view == null || closed) yield break;
        parryShown = true;
        var partyHud = ui != null ? ui.partyHUDList : null;
        RectTransform target = partyHud != null && targetIndex >= 0 && targetIndex < partyHud.Count
            ? partyHud[targetIndex]?.gameObject?.GetComponent<RectTransform>() : null;
        yield return Show(target != null ? new[] { target } : new RectTransform[0],
            "Đỡ đòn", "Parry",
            "Quái chuẩn bị tấn công. Quan sát đòn đánh rồi nhấn <b>Space</b> ngay trước khi đòn chạm để Parry. Bấm quá sớm sẽ dễ hụt nhịp.",
            "The enemy is about to attack. Watch the strike and press <b>Space</b> just before impact to parry. Pressing too early can miss the timing.");
    }

    IEnumerator Show(IReadOnlyList<RectTransform> focus, string title, string englishTitle, string body,
        string englishBody, Button requiredButton = null, GuidedCombatPractice practice = GuidedCombatPractice.None,
        bool allowCancel = false)
    {
        if (view == null || closed) yield break;
        bool english = GameLocalization.IsEnglish;
        bool accepted = false;
        string hint = requiredButton != null
            ? (english ? "Select the highlighted command" : "Bấm nút đang sáng")
            : allowCancel
                ? (english ? "Click to return to commands  •  Esc / Right-click" : "Bấm để về lựa chọn  •  Esc / Chuột phải")
                : (english ? "Click anywhere to continue" : "Bấm bất kỳ đâu để tiếp tục");
        // Number pages in their actual presentation order, even when Skills precedes Parry.
        view.Show(++shownLessons, LessonCount, focus, english ? englishTitle : title, english ? englishBody : body,
            hint, () =>
            {
                dismissedFrame = Time.frameCount;
                accepted = true;
            }, requiredButton, allowCancel);
        while (!closed && view != null && (view.IsOpen || Time.frameCount == dismissedFrame)) yield return null;

        // Forward only the accepted command after the modal's click/submit frame.
        if (accepted && !closed && requiredButton != null &&
            requiredButton.isActiveAndEnabled && requiredButton.IsInteractable())
        {
            presentingCommands = false;
            requiredButton.onClick.Invoke();
            if (practice != GuidedCombatPractice.None && CombatManager.Instance != null &&
                CombatManager.Instance.HasStartedGuidedPractice(practice))
                PendingPractice = practice;
        }
    }

    public void CompletePractice(GuidedCombatPractice practice)
    {
        if (practice == GuidedCombatPractice.None || PendingPractice != practice) return;
        if (practice == GuidedCombatPractice.Attack) attackCompleted = true;
        else if (practice == GuidedCombatPractice.Skill) skillCompleted = true;
        PendingPractice = GuidedCombatPractice.None;
    }

    public void AbandonPractice() => PendingPractice = GuidedCombatPractice.None;

    static Button FindCommand(Button[] buttons, string method)
    {
        foreach (var button in buttons)
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.isActiveAndEnabled && button.IsInteractable() &&
                    button.onClick.GetPersistentMethodName(i) == method)
                    return button;
        return null;
    }

    public void Close()
    {
        closed = true;
        PendingPractice = GuidedCombatPractice.None;
        presentingCommands = false;
        if (view != null) view.Close();
    }
}
