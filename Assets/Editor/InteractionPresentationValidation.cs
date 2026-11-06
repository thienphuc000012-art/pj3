using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Isolated UI checks: no gameplay, scene writes or campaign save files.
[InitializeOnLoad]
public static class InteractionPresentationValidation
{
    const string Request = "Library/CombatValidation/InteractionPresentation.request";
    const string Result = "Library/CombatValidation/InteractionPresentation.result.txt";
    static double nextCheck;
    static InteractionPresentationValidation() { EditorApplication.update += CheckRequest; }
    static void CheckRequest()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request); Validate();
    }

    [MenuItem("Tools/Adventure/UI/Validate Interaction Presentation")]
    public static void Validate()
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        Sprite sprite = null;
        Texture2D texture = null;
        try
        {
            var canvasGo = new GameObject("Interaction Preview", typeof(RectTransform), typeof(Canvas), typeof(AdventureCanvasRoot));
            canvasGo.layer = 5; SceneManager.MoveGameObjectToScene(canvasGo, preview);
            var root = canvasGo.GetComponent<AdventureCanvasRoot>(); root.kind = AdventureCanvasKind.Adventure;
            var hud = Rect("HUD", canvasGo.transform);
            var prompt = Rect("Interact", hud); prompt.gameObject.AddComponent<Image>();
            var button = prompt.gameObject.AddComponent<Button>();
            var label = Rect("Label", prompt); label.sizeDelta = new Vector2(300, 42);
            label.gameObject.AddComponent<TextMeshProUGUI>().text = "Nghỉ ngơi và lưu game";
            root.EnsureInteractLabelBackground();
            var plate = prompt.Find("Label Background").GetComponent<Image>();
            int id = plate.GetInstanceID();
            root.EnsureInteractLabelBackground();
            GameUITheme.Apply(canvasGo);
            Check(plate.GetInstanceID() == id && prompt.GetComponentsInChildren<Image>(true).Length >= 1, "Interact backdrop must be reused.");
            Check(plate.color == root.interactLabelBackgroundColor && plate.color.a > .7f, "Interact backdrop must stay black after theme updates.");
            Check(plate.transform.GetSiblingIndex() < label.GetSiblingIndex() && !plate.raycastTarget && button.interactable,
                "Backdrop must draw behind the label without blocking interaction.");
            Check(plate.rectTransform.offsetMin == label.offsetMin - root.interactLabelBackgroundPadding &&
                plate.rectTransform.offsetMax == label.offsetMax + root.interactLabelBackgroundPadding, "Backdrop must follow label bounds plus padding.");
            prompt.gameObject.SetActive(false); Check(!plate.gameObject.activeInHierarchy, "Backdrop must hide with Interact.");
            prompt.gameObject.SetActive(true);

            texture = new Texture2D(2, 2); texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            var slot = Rect("Slot", canvasGo.transform); slot.gameObject.AddComponent<Image>();
            var rootEdge = slot.gameObject.AddComponent<UIHoverOutline>(); rootEdge.Configure(Color.white);
            var pointer = slot.gameObject.AddComponent<SkillSlotPointer>();
            var select = Rect("Select", slot); var image = select.gameObject.AddComponent<Image>();
            select.gameObject.AddComponent<Button>();
            var wipe = select.gameObject.AddComponent<UIButtonWipeReveal>(); wipe.Configure(image, sprite);
            var edge = select.gameObject.AddComponent<UIHoverOutline>(); edge.Configure(Color.white);
            var selectPointer = select.gameObject.AddComponent<SkillSlotPointer>();
            var input = new PointerEventData(null);
            pointer.SetSelected(false);
            wipe.OnPointerEnter(input); edge.OnPointerEnter(input); rootEdge.OnPointerEnter(input);
            Advance(wipe, 1);
            Check(Reveal(wipe) == 0 && !edge.GetComponent<Outline>().enabled && !rootEdge.GetComponent<Outline>().enabled,
                "Hovering an unselected Party slot must not reveal the plate or outline.");
            pointer.SetSelected(true); Advance(wipe, .14f);
            Check(Reveal(wipe) > 0 && Reveal(wipe) < 1 && edge.GetComponent<Outline>().enabled, "Selected slot must animate its reveal.");
            Advance(wipe, 1); Check(Reveal(wipe) == 1, "Selected slot must reveal fully.");
            pointer.SetSelected(false); wipe.OnPointerEnter(input); Advance(wipe, 1);
            Check(Reveal(wipe) == 0 && !edge.GetComponent<Outline>().enabled, "Changing selection must hide the old slot even while hovered.");
            selectPointer.select = () => pointer.SetSelected(true);
            selectPointer.OnSelect(new BaseEventData(null)); Advance(wipe, 1);
            Check(Reveal(wipe) == 1, "Keyboard/gamepad selection must reveal the selected slot.");
            int rightClicks = 0; selectPointer.rightClick = () => rightClicks++;
            selectPointer.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Check(rightClicks == 1, "Right-click skill change must remain available.");
            wipe.SetForcedReveal(false); wipe.SetSelectionOnly(false); wipe.OnPointerEnter(input); Advance(wipe, 1);
            Check(Reveal(wipe) == 1, "Buttons outside selection-only mode must retain hover reveal.");

            File.WriteAllText(Result, "PASS: Interact black backdrop survives theme and repeat setup, stays behind text and does not block clicks; Party slot hover stays off, selection animates, deselection clears, navigation/right-click and other hover buttons remain functional.");
        }
        catch (Exception error) { File.WriteAllText(Result, "FAIL: " + error); Debug.LogException(error); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
    static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer; rect.SetParent(parent, false); return rect;
    }
    static void Advance(UIButtonWipeReveal wipe, float delta) => typeof(UIButtonWipeReveal).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wipe, new object[] { delta });
    static float Reveal(UIButtonWipeReveal wipe) => ((Image)typeof(UIButtonWipeReveal).GetField("reveal", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wipe)).fillAmount;
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
