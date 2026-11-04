using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-time, reversible migration of the combat party HUD. Never enters Play Mode.</summary>
[InitializeOnLoad]
public static class PartyTurnHUDSetup
{
    private const string ScenePath = "Assets/Scenes/combattest.unity";
    private const string StylePath = "Assets/Resources/PartyHUDStyle.asset";
    private const string Output = "Tools/CanvasUI";
    private const string Request = "Library/CombatValidation/PartyTurnHUDSetup.request";
    private const string ValidateRequest = "Library/CombatValidation/PartyTurnHUDValidate.request";
    private const string ResourceRequest = "Library/CombatValidation/PartyHPUltimateLayout.request";
    private const string Status = "Library/CombatValidation/PartyTurnHUDSetup.result.json";
    private static double nextCheck;
    private static bool busy;

    static PartyTurnHUDSetup() { EditorApplication.update += CheckRequest; }

    [MenuItem("Tools/Adventure/UI/Setup Party Turn HUD")]
    public static void SetupMenu() { Run(false); }

    /// <summary>Use with -executeMethod PartyTurnHUDSetup.SetupBatch, or the request file.</summary>
    public static void SetupBatch() { Run(true); }

    [MenuItem("Tools/Adventure/UI/Update Party HP Ultimate Layout")]
    public static void UpdateHPUltimateLayout() { RunResourceMigration(); }

    private static void CheckRequest()
    {
        if (busy || EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(ValidateRequest))
        {
            File.Delete(ValidateRequest);
            try { ValidatePreview(AssetDatabase.LoadAssetAtPath<PartyHUDStyle>(StylePath)); WriteStatus("passed", "HP / Ultimate layout, energy gain/spend, turn portraits, delayed HP shape and hidden HUD validated; previews rendered."); }
            catch (Exception ex) { WriteStatus("failed", ex.ToString()); Debug.LogException(ex); }
        }
        if (File.Exists(ResourceRequest))
        {
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.IsValid() && loaded.isDirty)
            {
                WriteStatus("waiting", "Save combattest first; its open scene has unsaved changes.");
                return;
            }
            RunResourceMigration();
        }
        if (!File.Exists(Request)) return;
        var existing = SceneManager.GetSceneByPath(ScenePath);
        if (existing.IsValid() && existing.isDirty)
        {
            WriteStatus("waiting", "Save combattest first; its open scene has unsaved changes.");
            return;
        }
        Run(true);
    }

    // Updates only resource fields on the already authored cards, without rebuilding their layout.
    private static void RunResourceMigration()
    {
        if (busy) return;
        busy = true;
        Scene original = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = false;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before updating the saved combat Canvas.");
            if (scene.IsValid() && scene.isDirty)
                throw new InvalidOperationException("combattest has unsaved changes. Save it before updating resources.");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AdvancedUIManager>(true)).Single();
            Require(manager.partyHUDList != null && manager.partyHUDList.Count > 0, "Set up the party turn HUD first.");
            var cards = manager.partyHUDList.Where(h => h != null && h.turnCard != null).ToArray();
            Require(cards.Length == manager.partyHUDList.Count, "Every registered party card needs its turn presenter.");
            Directory.CreateDirectory(Output);
            string backup = Output + "/BeforePartyHPUltimateLayout-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".unity";
            File.Copy(ScenePath, backup, false);
            var style = AssetDatabase.LoadAssetAtPath<PartyHUDStyle>(StylePath);
            Require(style != null, "PartyHUDStyle is missing.");
            style.ultimateColor = new Color(0f, .94f, 1f);
            EditorUtility.SetDirty(style);
            ValidatePreview(style);
            Undo.RecordObject(manager, "Update party HP / Ultimate fields");
            foreach (var hud in cards)
            {
                Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Update party HP / Ultimate fields");
                var protectedRects = new[] { hud.gameObject.GetComponent<RectTransform>(), hud.turnCard.portrait.rectTransform, hud.turnCard.barFrame.rectTransform };
                var positions = protectedRects.Select(r => r.anchoredPosition).ToArray();
                var sizes = protectedRects.Select(r => r.sizeDelta).ToArray();
                var scales = protectedRects.Select(r => r.localScale).ToArray();
                var rotations = protectedRects.Select(r => r.localRotation).ToArray();
                hud.turnCard.ConfigureResources(hud);
                ValidateResourceReferences(hud);
                for (int i = 0; i < protectedRects.Length; i++)
                    Require(protectedRects[i].anchoredPosition == positions[i] && protectedRects[i].sizeDelta == sizes[i] &&
                        protectedRects[i].localScale == scales[i] && protectedRects[i].localRotation == rotations[i],
                        "Resource migration must preserve slot, portrait and frame transforms.");
                EditorUtility.SetDirty(hud.turnCard);
            }
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unity could not save the HP / Ultimate layout.");
            AssetDatabase.SaveAssets();
            WriteStatus("passed", "Updated existing cards to HP / Ultimate; removed Stain labels and names; preserved slots and art transforms. Energy gain/spend and turn previews validated.", backup);
            if (File.Exists(ResourceRequest)) File.Delete(ResourceRequest);
            Debug.Log("Party HP / Ultimate layout ready. Backup: " + backup);
        }
        catch (Exception ex)
        {
            WriteStatus("failed", ex.ToString());
            if (File.Exists(ResourceRequest)) File.Delete(ResourceRequest);
            Debug.LogException(ex);
        }
        finally
        {
            if (opened && scene.IsValid() && !scene.isDirty) EditorSceneManager.CloseScene(scene, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            busy = false;
        }
    }

    private static void Run(bool validate)
    {
        if (busy) return;
        busy = true;
        Scene original = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = false;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before updating the saved combat Canvas.");
            if (scene.IsValid() && scene.isDirty)
                throw new InvalidOperationException("combattest has unsaved changes. Save it before running setup.");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AdvancedUIManager>(true)).Single();
            var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(r => r.name == "HP Party");
            if (root == null) throw new InvalidOperationException("Canvas/HP Party was not found.");
            Directory.CreateDirectory(Output);
            string backup = Output + "/BeforePartyTurnHUD-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".unity";
            File.Copy(ScenePath, backup, false);
            PartyHUDStyle style = UpdateStyle();
            if (validate) ValidatePreview(style);
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Setup Party Turn HUD");
            Undo.RecordObject(manager, "Bind party HUD cards");
            Migrate(manager, root, style);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unity could not save combattest.");
            AssetDatabase.SaveAssets();
            WriteStatus("passed", "Created four party cards and preserved the legacy HUD under an inactive holder.", backup);
            if (File.Exists(Request)) File.Delete(Request);
            Debug.Log("Party Turn HUD ready. Backup: " + backup, root);
        }
        catch (Exception ex)
        {
            WriteStatus("failed", ex.ToString());
            if (File.Exists(Request)) File.Delete(Request);
            Debug.LogException(ex);
        }
        finally
        {
            if (opened && scene.IsValid() && !scene.isDirty) EditorSceneManager.CloseScene(scene, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            busy = false;
        }
    }

    private static PartyHUDStyle UpdateStyle()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var style = AssetDatabase.LoadAssetAtPath<PartyHUDStyle>(StylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<PartyHUDStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
        }
        style.barFrame = SpriteAt("hp stain");
        if (style.portraits == null) style.portraits = new List<PartyHUDPortraitPair>();
        foreach (string key in new[] { "player (3)", "SK_Lauren Red" })
        {
            var pair = style.portraits.FirstOrDefault(p => p != null && p.characterKey == key);
            if (pair == null) { pair = new PartyHUDPortraitPair { characterKey = key }; style.portraits.Add(pair); }
            pair.waiting = SpriteAt(key);
            pair.acting = SpriteAt(key + " turn");
        }
        EditorUtility.SetDirty(style);
        return style;
    }

    private static Sprite SpriteAt(string name)
    {
        string path = "Assets/asset/Ui/hp player/" + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Missing HUD art: " + path);
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null) throw new InvalidOperationException("No Sprite imported for " + path);
        return sprite;
    }

    private static void Migrate(AdvancedUIManager manager, RectTransform root, PartyHUDStyle style)
    {
        var legacy = root.Find("Legacy Party HUD");
        if (legacy == null)
        {
            legacy = new GameObject("Legacy Party HUD", typeof(RectTransform)).transform;
            legacy.SetParent(root, false);
        }
        var oldCards = manager.partyHUDList != null ? manager.partyHUDList.ToArray() : Array.Empty<PartyHUDUnit>();
        foreach (var old in oldCards)
        {
            if (old == null || old.gameObject == null || old.gameObject.GetComponent<PartyTurnHUDCard>() != null) continue;
            if (old.buffContainer != null && !old.buffContainer.IsChildOf(old.gameObject.transform)) old.buffContainer.SetParent(legacy, true);
            old.gameObject.transform.SetParent(legacy, true);
        }
        foreach (Transform child in root.Cast<Transform>().ToArray())
        {
            if (child == legacy || child.GetComponent<PartyTurnHUDCard>() != null) continue;
            child.SetParent(legacy, true);
        }
        legacy.gameObject.SetActive(false);
        foreach (var card in root.GetComponentsInChildren<PartyTurnHUDCard>(true))
            if (!card.transform.IsChildOf(legacy)) UnityEngine.Object.DestroyImmediate(card.gameObject);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1, 0);
        root.anchoredPosition = new Vector2(-32, 32);
        root.sizeDelta = new Vector2(1000, 320);
        root.localScale = Vector3.one;
        var layout = root.GetComponent<HorizontalLayoutGroup>() ?? root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.enabled = true;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.LowerRight;
        layout.spacing = style.slotSpacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childScaleWidth = layout.childScaleHeight = false;
        layout.reverseArrangement = false;
        manager.partyHUDList = new List<PartyHUDUnit>();
        for (int i = 0; i < 4; i++)
        {
            var hud = PartyTurnHUDCard.Create(root, style, "Party Slot " + (i + 1));
            var size = hud.gameObject.GetComponent<LayoutElement>() ?? hud.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = style.slotSize.x;
            size.minHeight = size.preferredHeight = style.slotSize.y;
            size.flexibleWidth = size.flexibleHeight = 0;
            manager.partyHUDList.Add(hud);
        }
        var combat = manager.GetComponentInParent<Canvas>().GetComponentsInChildren<CombatManager>(true).FirstOrDefault();
        if (combat == null) combat = root.gameObject.scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<CombatManager>(true)).FirstOrDefault();
        for (int i = 0; i < manager.partyHUDList.Count; i++)
        {
            var hud = manager.partyHUDList[i];
            var unit = combat != null && i < combat.playerParty.Count ? combat.playerParty[i] : null;
            hud.gameObject.SetActive(unit != null);
            if (unit == null) continue;
            hud.turnCard.Bind(unit);
            hud.hpText.text = unit.maxHP.ToString();
            hud.turnCard.ultimateText.text = "0";
            hud.gameObject.transform.SetAsFirstSibling();
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private static void ValidatePreview(PartyHUDStyle style)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        Camera camera = null;
        RenderTexture target = null;
        Texture2D image = null;
        try
        {
            var cameraGo = new GameObject("Party HUD Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGo, preview);
            camera = cameraGo.GetComponent<Camera>();
            camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .045f, .06f);
            camera.orthographic = true; camera.orthographicSize = 280;
            camera.transform.position = new Vector3(0, 0, -10);
            var canvasGo = new GameObject("Party HUD Preview", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasGo, preview);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var cards = new List<PartyHUDUnit>();
            var units = new List<BattleUnit>();
            var names = new[] { "player (3)", "SK_Lauren Red", "player(Clone)" };
            for (int i = 0; i < names.Length; i++)
            {
                var unitGo = new GameObject(names[i]); SceneManager.MoveGameObjectToScene(unitGo, preview);
                var unit = unitGo.AddComponent<BattleUnit>(); unit.enabled = false;
                unit.unitName = names[i]; unit.isPlayer = true; unit.maxHP = 150; unit.SetPersistentHP(110 - 15 * i);
                unit.AddUltimateEnergy(35 + i * 20);
                units.Add(unit);
                var hud = PartyTurnHUDCard.Create(canvas.transform, style, "Preview Slot " + i);
                var rect = hud.gameObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2((i - 1) * (style.slotSize.x + style.slotSpacing), 48);
                hud.BindUnit(unit); hud.turnCard.Bind(unit); cards.Add(hud);
                Require(hud.hpFill != null && hud.shieldFill != null && hud.ultimateFill != null && hud.buffContainer != null,
                    "HP, shield, ultimate or status references missing.");
                Require(hud.hpFill != hud.ultimateFill && hud.hpFill.type == Image.Type.Filled,
                    "HP trail and ultimate must use independent fill Images.");
                ValidateResourceReferences(hud);
            }
            target = new RenderTexture(1100, 560, 24); target.Create(); camera.targetTexture = target;
            for (int selected = 1; selected <= 2; selected++)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    cards[i].turnCard.SetTurn(i == selected, true); cards[i].turnCard.Tick(1);
                    Require(cards[i].turnCard.ultimateText.text == Mathf.RoundToInt(units[i].UltimateEnergy).ToString(),
                        "Ultimate number must match energy without a percent suffix.");
                    var pair = style.portraits[i == 1 ? 1 : 0];
                    Require(cards[i].turnCard.portrait.sprite == (i == selected ? pair.acting : pair.waiting), "Incorrect portrait state / prefab name mapping.");
                }
                Require(cards[selected].turnCard.visualRoot.localScale.x > cards[(selected + 1) % 3].turnCard.visualRoot.localScale.x,
                    "Active turn card must be larger.");
                Canvas.ForceUpdateCanvases();
                foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true)) graphic.SetAllDirty();
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
                Canvas.ForceUpdateCanvases();
                for (int i = 1; i < cards.Count; i++)
                {
                    var left = new Vector3[4]; var right = new Vector3[4];
                    cards[i - 1].turnCard.visualRoot.GetWorldCorners(left); cards[i].turnCard.visualRoot.GetWorldCorners(right);
                    Require(left.Max(v => v.x) <= right.Min(v => v.x) + .01f, "Adjacent card rectangles overlap.");
                }
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target; image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                    File.WriteAllBytes(Output + "/PartyTurnHUD-" + (selected == 1 ? "Middle" : "Third") + ".png", image.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; if (image != null) UnityEngine.Object.DestroyImmediate(image); image = null; }
            }
            var renderTrail = typeof(AdvancedUIManager).GetMethod("RenderDamageTrail", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            object[] args = { cards[0].hpFill, null, .9f };
            renderTrail.Invoke(null, args);
            var trail = args[1] as PartyHUDSlantedFill;
            Require(trail != null && Mathf.Approximately(trail.fillAmount, .9f) && trail.topLeft == ((PartyHUDSlantedFill)cards[0].hpFill).topLeft,
                "Delayed HP damage trail must keep the supplied slanted geometry.");
            var resourceHud = cards[0];
            units[0].AddUltimateEnergy(10);
            resourceHud.AnimateUltimate(resourceHud.ultimateFillDuration * .5f);
            resourceHud.turnCard.Tick(0);
            Require(resourceHud.ultimateFill.fillAmount > .35f && resourceHud.ultimateFill.fillAmount < .45f,
                "Ultimate must charge gradually on the lower rail.");
            Require(resourceHud.turnCard.ultimateText.text == Mathf.RoundToInt(resourceHud.ultimateFill.fillAmount * 100).ToString(),
                "Ultimate number must follow the displayed charge.");
            resourceHud.AnimateUltimate(resourceHud.ultimateFillDuration);
            float energyBeforeHpChange = resourceHud.ultimateFill.fillAmount;
            units[0].SetPersistentHP(90); resourceHud.turnCard.Tick(0);
            Require(Mathf.Approximately(resourceHud.ultimateFill.fillAmount, energyBeforeHpChange),
                "HP changes must not overwrite Ultimate energy.");
            var ultimateAction = ScriptableObject.CreateInstance<ActionData>();
            try
            {
                ultimateAction.isUltimate = true; units[0].AddUltimateEnergy(100);
                resourceHud.AnimateUltimate(resourceHud.ultimateFillDuration);
                Require(units[0].CommitActionEnergy(ultimateAction), "Full energy should permit Ultimate.");
                resourceHud.turnCard.Tick(0);
                Require(resourceHud.ultimateFill.fillAmount == 0 && resourceHud.turnCard.ultimateText.text == "0",
                    "Spending Ultimate must clear lower rail and number immediately.");
            }
            finally { UnityEngine.Object.DestroyImmediate(ultimateAction); }
            cards[0].gameObject.SetActive(false);
            cards[0].turnCard.SetTurn(true); cards[0].turnCard.Tick(1);
            Require(!cards[0].gameObject.activeSelf, "Turn animation must not reveal a hidden HUD.");
            cards[1].turnCard.SetTurn(true, true);
            cards[1].turnCard.SetTurn(false); cards[1].turnCard.Tick(.01f);
            Require(cards[1].turnCard.visualRoot.localScale.x < style.activeScale, "Turn switch must immediately start shrinking the previous card.");
        }
        finally
        {
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void ValidateResourceReferences(PartyHUDUnit hud)
    {
        var root = hud.turnCard.visualRoot;
        Require(root.Find("Stain Value") == null && root.Find("Stain Label") == null && root.Find("Name") == null && hud.nameText == null,
            "Party cards must not retain Stain values, Stain labels or names.");
        Require(hud.ultimateFill != null && hud.ultimateFill.transform.parent == hud.turnCard.barFrame.transform &&
            hud.ultimateFill.name == "Ultimate Energy" && hud.ultimateFill is PartyHUDSlantedFill,
            "Ultimate must use the lower slanted rail inside the supplied art.");
        Require(root.Find("Ultimate Energy") == null && root.Find("Resource Divider") != null,
            "Only the two art rails should remain, with a divider between HP and Ultimate values.");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void WriteStatus(string state, string message, string backup = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Status));
        File.WriteAllText(Status, JsonUtility.ToJson(new Result { status = state, message = message, backup = backup }, true));
    }
    [Serializable] private class Result { public string status, message, backup; }
}
