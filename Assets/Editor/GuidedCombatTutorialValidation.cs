using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Exercises both turn orders in an isolated UI scene, without starting combat or writing saves.</summary>
public static class GuidedCombatTutorialValidation
{
    const string Result = "Library/CombatValidation/GuidedTutorial.result.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Type TutorialType = typeof(CombatManager).Assembly.GetType("GuidedCombatTutorial");
    static readonly Type PracticeType = typeof(CombatManager).Assembly.GetType("GuidedCombatPractice");

    [InitializeOnLoadMethod]
    static void ScheduleRequestedValidation()
    {
        if (File.Exists("Library/CombatValidation/GuidedTutorial.request"))
            EditorApplication.delayCall += RunRequested;
    }

    static void RunRequested()
    {
        const string request = "Library/CombatValidation/GuidedTutorial.request";
        if (!File.Exists(request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        File.Delete(request);
        Run();
    }

    [MenuItem("Tools/Combat/Validate Guided Tutorial")]
    public static void Run()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Result));
        var originalCombat = CombatManager.Instance;
        var originalUI = AdvancedUIManager.Instance;
        var originalLanguage = GameLocalization.CurrentLanguage;
        var languageField = typeof(GameLocalization).GetField("language", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in Edit Mode.");
            CombatManager.Instance = null;
            foreach (var language in new[] { GameLanguage.Vietnamese, GameLanguage.English })
            foreach (bool playerNext in new[] { true, false })
            {
                // Avoid SetLanguage: validation must not alter player preferences.
                languageField.SetValue(null, language);
                using (var fixture = new Fixture())
                {
                    var opening = fixture.Player(true, true);
                    Check(opening.Count == 4, "Opening must have commands, turn order, elements, then Attack.");
                    Check(opening[0] == (language == GameLanguage.English ? "Battle commands" : "Lệnh chiến đấu"), "The first page was overwritten by a previous localized lesson.");
                    Check(opening.Distinct().Count() == 4, "Opening lessons must have distinct content after localization refresh.");
                    Check(opening[2] == "Sát thương khắc hệ" || opening[2] == "Elemental damage", "Elements must be Lesson 3.");
                    Check(fixture.ElementsChecked, "Element lesson must spotlight the active party element icons.");
                    Check(!(bool)Get(fixture.Tutorial, "attackCompleted"), "Opening Attack must not count as performing it.");
                    Call(fixture.Tutorial, "CompletePractice", Enum.Parse(PracticeType, "Skill"));
                    Check(!(bool)Get(fixture.Tutorial, "skillCompleted"), "A skill must not complete without pending practice.");
                    fixture.Complete("Attack");

                    if (!playerNext)
                        Check(fixture.Parry().Count == 1, "Enemy next must present Parry.");
                    Check(fixture.Player(true, false).Count == 0, "Unavailable skills must not trap the tutorial.");
                    var skills = fixture.Player(true, true);
                    Check(skills.Count == 2, "Player next must present Skills and Stain, even before Parry.");
                    Check(!(bool)Get(fixture.Tutorial, "skillCompleted"), "Opening Skills must not finish practice.");
                    fixture.Complete("Skill");
                    if (playerNext)
                    {
                        Check(fixture.Player(true, true).Count == 0, "The final page must wait for the remaining Parry lesson.");
                        Check(fixture.Parry().Count == 1, "Enemy later must still present Parry after Skills.");
                    }
                    Check(fixture.Parry().Count == 0, "Parry must not repeat.");
                    Check(fixture.Player(true, true).Count == 1, "Party HUD must be the last page.");
                    Check(fixture.PageCount == 8, "Both turn orders must present exactly eight pages.");
                    Check(fixture.Player(true, true).Count == 0, "Completed lessons must not repeat.");
                    Call(fixture.Tutorial, "Close");
                    Check(fixture.Player(true, true).Count == 0 && fixture.Parry().Count == 0, "Closed tutorials must stay closed.");
                }
            }
            File.WriteAllText(Result, "PASS: VI/EN; all eight lessons retain their own titles, text, counters and hints after localization OnEnable/Refresh; authored label colors preserved; element page before Attack; Player-first Skills and Enemy-first Parry branches; unavailable skill deferral; action completion gates; no repeated lessons; final HUD last. Isolated UI only, no campaign/save/preference writes.");
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception exception)
        {
            File.WriteAllText(Result, "FAIL: " + exception);
            throw;
        }
        finally
        {
            CombatManager.Instance = originalCombat;
            AdvancedUIManager.Instance = originalUI;
            languageField.SetValue(null, originalLanguage);
        }
    }

    sealed class Fixture : IDisposable
    {
        readonly Scene scene;
        readonly GuidedTutorialCommandProbe commands;
        readonly AdvancedUIManager ui;
        readonly GuidedCombatTutorialView view;
        readonly List<RectTransform> elementIcons = new List<RectTransform>();
        public object Tutorial { get; }
        public int PageCount { get; private set; }
        public bool ElementsChecked { get; private set; }

        public Fixture()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Isolated Guided Tutorial", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var host = new GameObject("Inactive UI Controller");
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, scene);
            ui = host.AddComponent<AdvancedUIManager>();
            ui.actionMenuPanel = Rect("Commands", root.transform);
            commands = ScriptableObject.CreateInstance<GuidedTutorialCommandProbe>();
            var attack = Rect("Attack", ui.actionMenuPanel).gameObject.AddComponent<Button>();
            var skills = Rect("Skills", ui.actionMenuPanel).gameObject.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(attack.onClick, commands.OnAttackClicked);
            UnityEventTools.AddPersistentListener(skills.onClick, commands.OnSkillsClicked);
            attack.onClick.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            skills.onClick.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            ui.turnOrderContainer = Rect("Turn Order", root.transform);
            Rect("Portrait", ui.turnOrderContainer);
            ui.partyHUDList = new List<PartyHUDUnit>();
            for (int i = 0; i < 3; ++i)
            {
                var card = Rect("Party " + i, root.transform);
                var icon = Rect("Element Icon", card).gameObject.AddComponent<Image>();
                icon.gameObject.SetActive(i < 2);
                if (i < 2) elementIcons.Add(icon.rectTransform);
                ui.partyHUDList.Add(new PartyHUDUnit { gameObject = card.gameObject, elementIcon = icon });
            }
            ui.partyHUDList.Add(null);
            ui.stainIcons = new[] { Rect("Stain", root.transform).gameObject.AddComponent<Image>() };
            Tutorial = TutorialType.GetMethod("Create").Invoke(null, new object[] { ui });
            Check(Tutorial != null, "Tutorial creation failed.");
            // Replace only the real command's gameplay side effects with opening
            // its practice state. The real tutorial/view/coroutines are exercised.
            commands.Attack = () => Set(Tutorial, "<PendingPractice>k__BackingField", Enum.Parse(PracticeType, "Attack"));
            commands.Skills = () => Set(Tutorial, "<PendingPractice>k__BackingField", Enum.Parse(PracticeType, "Skill"));
            view = root.GetComponentInChildren<GuidedCombatTutorialView>(true);
            // Same binding performed by AdvancedUIManager.Awake on the real,
            // authored, initially inactive combat Canvas.
            GameLocalization.BindHierarchy(root);
        }

        public List<string> Player(bool attack, bool skill) => Drain(
            (IEnumerator)Call(Tutorial, "ShowPlayerLessons", ui, attack, skill));
        public List<string> Parry() => Drain((IEnumerator)Call(Tutorial, "ShowParryLesson", ui, 0));

        public void Complete(string practice)
        {
            var value = Enum.Parse(PracticeType, practice);
            Call(Tutorial, "CompletePractice", value);
        }

        List<string> Drain(IEnumerator routine)
        {
            var pages = new List<string>();
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            int iterations = 0;
            while (stack.Count > 0)
            {
                Check(++iterations < 250, "Lesson coroutine did not finish.");
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                if (view.IsOpen)
                {
                    foreach (var label in view.GetComponentsInChildren<TMP_Text>(true))
                    {
                        string displayed = label.text;
                        Color color = label.color;
                        var binding = label.GetComponent<LocalizedGameText>();
                        Check(binding != null, "Tutorial labels must use the real localization bindings.");
                        // Edit Mode fixtures do not receive the Play Mode
                        // activation callbacks, so exercise that refresh here.
                        binding.Refresh();
                        Check(label.text == displayed, "Localization restored an old lesson on " + label.name);
                        Check(label.color == color, "Updating lesson text changed an authored label color.");
                    }
                    string title = ((TMP_Text)Get(view, "heading")).text;
                    pages.Add(title);
                    ++PageCount;
                    Check(((TMP_Text)Get(view, "counter")).text == PageCount.ToString("00") + "  /  08",
                        "Page counters must follow display order rather than a fixed Parry/Skills order.");
                    Check((bool)TutorialType.GetProperty("BlocksInput").GetValue(Tutorial), "Modal must block combat input.");
                    Set(view, "openedFrame", -1);
                    if (!(bool)Get(view, "allowCancel"))
                    {
                        view.OnCancel(null);
                        Check(view.IsOpen, "Esc must not dismiss locked lessons.");
                    }
                    if ((bool)Get(view, "requiresAction"))
                    {
                        view.OnPointerClick(new PointerEventData(null)
                        { button = PointerEventData.InputButton.Left, position = new Vector2(-10000, -10000) });
                        Check(view.IsOpen, "Clicking outside the required command must not advance practice.");
                    }
                    if (title == "Sát thương khắc hệ" || title == "Elemental damage")
                    {
                        var focus = (IReadOnlyList<RectTransform>)Get(view, "focusTargets");
                        Check(focus.SequenceEqual(elementIcons), "Spotlight must include only visible party element icons.");
                        string body = ((TMP_Text)Get(view, "content")).text;
                        Check(body.Contains("×2") && body.Contains("×1") && body.Contains("×0"), "Element lesson needs all three multipliers.");
                        ElementsChecked = true;
                    }
                    view.OnSubmit(null);
                    Check(!view.IsOpen, "Submit must dismiss informational pages or accept the required command.");
                    Set(Tutorial, "dismissedFrame", -1);
                }
                var nested = stack.Peek().Current as IEnumerator;
                // Waits need no wall-clock delay in this deterministic Edit Mode fixture.
                if (nested != null && !(nested is CustomYieldInstruction)) stack.Push(nested);
            }
            return pages;
        }

        public void Dispose()
        {
            if (Tutorial != null) Call(Tutorial, "Close");
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(commands);
        }
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(100, 40);
        return rect;
    }

    static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(target, args);
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

public sealed class GuidedTutorialCommandProbe : ScriptableObject
{
    public Action Attack;
    public Action Skills;
    public void OnAttackClicked() => Attack?.Invoke();
    public void OnSkillsClicked() => Skills?.Invoke();
}
