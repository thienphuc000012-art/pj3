#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CanvasUIValidation
{
    const string Request = "Library/CombatValidation/CanvasUI.request";
    [InitializeOnLoadMethod]
    static void Queue() { if (File.Exists(Request)) EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Requested; return; }
        if (!File.Exists(Request)) return;
        File.Delete(Request); Validate();
    }
    [MenuItem("Tools/Adventure/Validate Canvas UI")]
    public static void Validate()
    {
        string result;
        try
        {
            foreach (var kind in new[] { AdventureCanvasKind.Adventure, AdventureCanvasKind.BattleResult, AdventureCanvasKind.Loading })
            {
                string path = "Assets/Resources/Adventure/UI/" + kind + "Canvas.prefab";
                var obj = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var root = obj.GetComponent<AdventureCanvasRoot>();
                    Check(root != null && root.kind == kind, "Native root controller: " + kind);
                    Check(obj.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay, "Overlay canvas");
                    Check(obj.GetComponent<CanvasScaler>().referenceResolution == new Vector2(1920, 1080), "Canvas scaling");
                    Check(obj.GetComponent<GraphicRaycaster>() != null, "UI raycaster");
                    Check(root.languageFont != null, "Vietnamese source font included");
                    Check(obj.GetComponentsInChildren<TMP_Text>(true).All(t => t.font != null), "TMP font references");
                    Check(obj.GetComponentsInChildren<Button>(true).All(b => b.targetGraphic != null), "Button target graphics");
                    Check(obj.GetComponentsInChildren<ScrollRect>(true).All(s => s.content != null && s.viewport != null && s.viewport.GetComponent<RectMask2D>() != null), "Scroll view references and masks");
                    Check(obj.GetComponentsInChildren<Image>(true).Where(i => i.type == Image.Type.Filled).All(i => i.sprite != null), "Filled bar sprites resolve");
                    Check(obj.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts");
                    if (kind == AdventureCanvasKind.Adventure)
                    {
                        Check(root.Component<RawImage>("Menu/Preview") != null && root.Component<RawImage>("Menu/DetailPreview") != null, "Both model preview surfaces");
                        int bound = 0;
                        var rows = root.Rows("Menu/Party/Overview/Cards/Viewport/Content", 2, (row, index) => bound++);
                        int first = rows[0].GetInstanceID();
                        root.Rows("Menu/Party/Overview/Cards/Viewport/Content", 2, (row, index) => bound++);
                        Check(bound == 2 && rows[0].GetInstanceID() == first, "Rows reused, handlers bound once");
                        root.Rows("Menu/Party/Overview/Cards/Viewport/Content", 1);
                        Check(!rows[1].activeSelf, "Excess rows hidden");
                        root.Rows("Menu/Party/Overview/Cards/Viewport/Content", 2);
                        Check(rows[1].activeSelf && bound == 2, "Pooled rows reused");
                        int clicks = 0;
                        root.Click("Menu/Party/Member/MoveLeft", () => clicks++);
                        var button = root.Component<Button>("Menu/Party/Member/MoveLeft");
                        button.onClick.Invoke(); Check(clicks == 1, "Native button events");
                        int selected = -1;
                        var members = root.Rows("Menu/Party/Member/Picker/Viewport/Content", 2,
                            (row, index) => AdventureCanvasRoot.RowClick(row, "Select", () => selected = index));
                        members[1].GetComponent<Button>().onClick.Invoke();
                        Check(selected == 1 && members[1].GetComponent<Image>().raycastTarget, "Full portrait card selects second member");
                        members[0].transform.Find("Select").GetComponent<Button>().onClick.Invoke();
                        Check(selected == 0, "Switch back via child selection button");
                        Check(root.transform.Find("Menu/Party/Items") == null && root.transform.Find("Menu/Party/Inventory") == null, "Party has no inventory UI");
                        Check(root.Component<GridLayoutGroup>("Menu/Inventory/Items/Viewport/Content").constraintCount == 4, "Four-column inventory");
                        Check(root.Component<Button>("Menu/Inventory/Detail/Use") != null, "Selected item use button");
                        Check(root.Component<Button>("Menu/Attributes/Confirm") != null && root.Component<Button>("Menu/Attributes/Cancel") != null, "Attribute draft confirmation controls");
                        Check(root.Component<Button>("Menu/Attributes/Stats/Viewport/Content/Template/Reduce") != null, "Attribute point removal control");
                        foreach (string page in new[] { "Attributes", "Skills" })
                        {
                            string previewPath = "Menu/" + page + "/CharacterPreview";
                            Check(root.Component<RectTransform>(previewPath).sizeDelta == new Vector2(250, 650), "Upgrade preview size: " + page);
                            Check(!root.Component<Image>(previewPath + "/Portrait").raycastTarget && !root.Component<RawImage>(previewPath + "/Render").raycastTarget, "Upgrade previews do not block selection");
                        }
                        Check(root.Component<RawImage>("Menu/Party/Overview/Cards/Viewport/Content/Template/Preview") != null, "Portrait card render surface");
                        var previewCards = root.Node("Menu/Party/Overview/Cards");
                        Check(previewCards.GetComponentsInChildren<Button>(true).Length == 0 && previewCards.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "Main party preview is not interactive");
                        Check(!previewCards.GetComponent<ScrollRect>().enabled, "Main preview does not scroll or drag");
                        Check(root.Component<Button>("Menu/Party/Overview/Party") != null && root.Component<Button>("Menu/Party/Overview/Inventory") != null, "Main menu navigation buttons");
                        var cardGrid = root.Component<GridLayoutGroup>("Menu/Party/Overview/Cards/Viewport/Content");
                        Check(cardGrid.constraintCount == 4 && cardGrid.cellSize.x * 4 + cardGrid.spacing.x * 3 + cardGrid.padding.horizontal <= 1690, "Four cards fit the party viewport");
                        AdventureCanvasRoot.RowSelected(rows[0], true);
                        var selectedHighlight = rows[0].transform.Find("SelectionHighlight");
                        Check(selectedHighlight.gameObject.activeSelf && selectedHighlight.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g => !g.raycastTarget), "Selection glow cannot block clicks");
                        var fade = selectedHighlight.GetComponent<SelectionFadeGraphic>();
                        Check(fade != null && selectedHighlight.childCount == 0, "Selection uses a fade without a full border");
                        using (var mesh = new VertexHelper())
                        {
                            typeof(SelectionFadeGraphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(fade, new object[] { mesh });
                            var bottom = new UIVertex(); var top = new UIVertex();
                            mesh.PopulateUIVertex(ref bottom, 0); mesh.PopulateUIVertex(ref top, 1);
                            Check(bottom.color.a == 0 && top.color.a > 0 && bottom.position.y < top.position.y, "Selection fades from a bright top to transparent bottom");
                        }
                        AdventureCanvasRoot.RowSelected(rows[0], false);
                        Check(!selectedHighlight.gameObject.activeSelf, "Deselection clears the glow");
                        Check(root.Component<RawImage>("Menu/Party/Member/Preview") != null, "Separate member detail panel");
                        Check(root.Component<Button>("Menu/Party/Member/SkillDialog/Confirm") != null && root.Component<Button>("Menu/Party/Member/Slots/Viewport/Content/Template/Select") != null, "Loadout slot controls");
                        Check(root.Component<TMP_Text>("Menu/Skills/Reason") != null, "Visible skill unlock reason");
                        Check(root.transform.Find("Menu/Inventory/Picker") == null && root.transform.Find("Menu/Inventory/Summary") == null, "Inventory has no character UI");
                        Check(root.transform.Find("Menu/Party/Member/Learned") == null && root.Component<Button>("Menu/Party/Member/SkillContext/Change") != null, "Skill list is only in the change dialog");
                        Check(!root.Node("Menu/Party/Member/SkillDialog").gameObject.activeSelf, "Change dialog starts closed");
                        Check(obj.GetComponentsInChildren<TMP_Text>(true).All(t => !t.text.Contains("\u25c7") && !t.text.Contains("\u25c6")), "Unsupported diamond glyphs removed");
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(obj); }
            }
            foreach (string scene in new[] { "mapgame", "combattest", "Loading" })
            {
                var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + scene + ".unity");
                try
                {
                    Check(preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AdventureCanvasRoot>(true)).Count() == 1, scene + " has one authored Canvas instance");
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            result = "PASS: native Canvas/TMP imports, font/sprite references, buttons, masks, pooled rows and scene Canvas instances.";
            Debug.Log(result);
        }
        catch (Exception e) { result = "FAIL: " + e; Debug.LogError(result); }
        Directory.CreateDirectory("Library/CombatValidation");
        File.WriteAllText("Library/CombatValidation/CanvasUI.result.txt", result);
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
