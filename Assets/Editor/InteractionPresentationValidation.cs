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
            var slot = Rect("Slot", canvasGo.transform);
            var background = slot.gameObject.AddComponent<Image>();
            var select = Rect("Select", slot);
            var hitArea = select.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            select.gameObject.AddComponent<Button>();
            var slotLabel = Rect("Label", select).gameObject.AddComponent<TextMeshProUGUI>();
            var pointer = select.gameObject.AddComponent<SkillSlotPointer>();
            pointer.BindStyle(background, slotLabel);
            pointer.SetSelected(false);
            Check(background.color == Color.black && slotLabel.color == Color.white, "Unselected slot must have a black row and white text.");
            pointer.select = () => pointer.SetSelected(true);
            pointer.OnSelect(new BaseEventData(null));
            Check(background.color == Color.white && slotLabel.color == Color.black, "Selection must invert the row and text.");
            Check(((Color32)hitArea.color).a == 100, "Selected overlay must use alpha 100/255.");
            pointer.SetSelected(false);
            Check(background.color == Color.black && slotLabel.color == Color.white, "Deselecting must restore the idle colors.");
            Check(hitArea.color.a == 0f, "Deselecting must clear the overlay.");
            int rightClicks = 0; pointer.rightClick = () => rightClicks++;
            pointer.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Check(rightClicks == 1, "Right-click skill change must remain available.");
            var otherButton = Rect("Other Button", canvasGo.transform).gameObject;
            var image = otherButton.AddComponent<Image>();
            otherButton.AddComponent<Button>();
            var wipe = otherButton.AddComponent<UIButtonWipeReveal>();
            wipe.Configure(image, sprite);
            wipe.OnPointerEnter(new PointerEventData(null)); Advance(wipe, 1);
            Check(Reveal(wipe) == 1, "Buttons outside selection-only mode must retain hover reveal.");

            File.WriteAllText(Result, "PASS: Interact backdrop survives theme; Party slots invert on selection and restore on deselection, Select remains transparent, right-click works, and other buttons keep hover wipe.");
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
