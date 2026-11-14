using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CombatActionDescriptionValidation
{
    const string Request = "Library/CombatValidation/CombatActionDescription.request";
    const string Output = "Tools/SkillDescriptions";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/CombatActionDescriptionValidation.cs")) return;
        File.Delete(Request);
        try { Validate(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Validation.txt", "FAIL: " + error); Debug.LogException(error); }
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [MenuItem("Tools/Adventure/UI/Validate Generated Skill Descriptions")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode for isolated description validation.");
        Directory.CreateDirectory(Output);
        string previewStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var report = new StringBuilder(); var action = ScriptableObject.CreateInstance<ActionData>();
        var preview = EditorSceneManager.NewPreviewScene();
        var oldManager = CombatManager.Instance;
        TMP_FontAsset font = null;
        var languageField = typeof(GameLocalization).GetField("language", BindingFlags.Static | BindingFlags.NonPublic);
        var oldLanguage = GameLocalization.CurrentLanguage;
        try
        {
            action.type = ActionData.ActionType.Skill; action.power = 20; action.damageMultiplier = 1.5f; action.element = ActionData.ElementType.Fire;
            action.damageCondition = ActionData.DamageCondition.CasterHasBurn;
            action.useLifeSteal = true; action.lifeStealPercent = 33.3f; action.stainChange = -2;
            action.debuffs = new[] { new ActionData.DebuffSettings { type = ActionData.DebuffType.Burn, applyChance = 70, turns = 3, burnDamage = 12, burnAttackReductionPercent = 25 } };
            string vi = CombatActionDescription.Build(action, GameLanguage.Vietnamese), en = CombatActionDescription.Build(action, GameLanguage.English);
            Check(vi.Contains("Lửa") && vi.Contains("150% ATK + 20") && vi.Contains("×2") && vi.Contains("×0,5") && vi.Contains("70%") && vi.Contains("12 máu") && vi.Contains("25%") && vi.Contains("Hút máu"), "Missing damage/element/Burn/debuff/life-steal settings");
            Check(en.Contains("Fire") && en.Contains("without Burn → ×0.5") && en.Contains("Life steal") && !en.Contains("Thiêu đốt"), "English generation is incomplete");
            File.WriteAllText(Output + "/Example.txt", vi + "\n\n" + en);

            var actor = new GameObject("Description caster"); SceneManager.MoveGameObjectToScene(actor, preview);
            var caster = actor.AddComponent<BattleUnit>(); caster.baseAtk = 100;
            var targetGo = new GameObject("Description target"); SceneManager.MoveGameObjectToScene(targetGo, preview); var target = targetGo.AddComponent<BattleUnit>();
            var managerGo = new GameObject("Description damage check"); SceneManager.MoveGameObjectToScene(managerGo, preview);
            var manager = managerGo.AddComponent<CombatManager>(); manager.enabled = false; manager.useElementalDamage = false;
            var damage = typeof(CombatManager).GetMethod("CalculateActionDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            Check((int)damage.Invoke(manager, new object[] { caster, target, action, false }) == 85, "Unburned skill branch changed");
            caster.activeDebuffs.Add(new BattleUnit.ActiveDebuff { settings = action.debuffs[0], remainingTurns = 3 });
            Check((int)damage.Invoke(manager, new object[] { caster, target, action, false }) == 340, "Burn empowerment must ignore Burn's ATK reduction when configured");
            action.damageCondition = ActionData.DamageCondition.TargetHasPoison; action.damageMultiplierWhenTargetPoisoned = 1.75f;
            caster.activeDebuffs.Clear(); target.activeDebuffs.Add(new BattleUnit.ActiveDebuff { settings = new ActionData.DebuffSettings { type = ActionData.DebuffType.Poison }, remainingTurns = 3 });
            Check((int)damage.Invoke(manager, new object[] { caster, target, action, false }) == 298 && CombatActionDescription.Build(action, GameLanguage.English).Contains("×1.75"), "Poison condition must agree with combat calculation");
            report.AppendLine("PASS: VI/EN, per-hit ATK/power formula, both Burn branches, ignored Burn reduction, Poison multiplier and life steal agree with combat data and actual damage calculations.");

            action.isFriendlyAction = true; action.statusOnly = true; action.isSelfOnly = true;
            action.buffStat = ActionData.BuffStat.CritDamage; action.buffAmount = 50; action.buffDuration = 4;
            action.cleanse = ActionData.CleanseType.RemoveDebuffs;
            en = CombatActionDescription.Build(action, GameLanguage.English);
            Check(en.Contains("self") && en.Contains("50 percentage points") && en.Contains("4 turns") && en.Contains("Remove all target debuffs") && !en.Contains("Condition:"), "Status-only buffs must not claim conditional direct damage");
            action.debuffs = new[] { null, new ActionData.DebuffSettings { type = ActionData.DebuffType.Poison, turns = 2, applyChance = 80, poisonMaxHpPercent = 7 }, new ActionData.DebuffSettings { type = ActionData.DebuffType.Paralysis, turns = 3, paralysisSkipChance = 40 } };
            en = CombatActionDescription.Build(action, GameLanguage.English);
            Check(en.Contains("7% of maximum HP") && en.Contains("40% chance to skip") && en.Contains("first successful") && en.Contains("no stacking"), "Multiple-debuff priority and values missing");
            action.isHeal = true; action.isSelfOnly = false; action.isAoE = true;
            Check(CombatActionDescription.Build(action, GameLanguage.English).Contains("Restore 150% ATK + 20 HP"), "Healing formula missing");
            action.isSummonSkill = true; action.summonPrefab = target; target.unitName = "Test summon"; action.summonCount = 2; action.isUltimate = true;
            en = CombatActionDescription.Build(action, GameLanguage.English);
            Check(en.Contains("Summon up to 2 Test summon") && en.Contains("full Ultimate gauge") && !en.Contains("Restore 150%"), "Summons must not claim damage or healing ignored by combat");
            report.AppendLine("PASS: buff percentages/duration, self/AoE targeting, healing, cleanse, Burn/Poison/Paralysis, debuff priority, summons and Ultimate requirements.");

            var canvasGo = new GameObject("Description UI", typeof(RectTransform), typeof(Canvas)); SceneManager.MoveGameObjectToScene(canvasGo, preview);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var cameraGo = new GameObject("Description camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraGo, preview); var camera = cameraGo.GetComponent<Camera>();
            camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.015f, .02f, .035f);
            camera.scene = preview; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview); canvas.worldCamera = camera; canvas.planeDistance = 1;
            var fontSource = AssetDatabase.LoadAssetAtPath<AdventureCanvasRoot>("Assets/Resources/Adventure/UI/AdventureCanvas.prefab");
            font = TMP_FontAsset.CreateFontAsset(fontSource.languageFont); font.atlasPopulationMode = AtlasPopulationMode.Dynamic; font.isMultiAtlasTexturesEnabled = true;
            var labelGo = new GameObject("Description", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); labelGo.transform.SetParent(canvasGo.transform, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = 22; label.color = Color.white; label.alignment = TextAlignmentOptions.TopLeft;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f); label.rectTransform.sizeDelta = new Vector2(720, 190);
            action.isSummonSkill = action.isHeal = action.isFriendlyAction = action.isSelfOnly = action.isAoE = false; action.statusOnly = false;
            action.damageCondition = ActionData.DamageCondition.CasterHasBurn; action.buffStat = ActionData.BuffStat.None;
            languageField.SetValue(null, GameLanguage.Vietnamese);
            GameLocalization.SetActionDescription(label, action);
            var binding = label.GetComponent<LocalizedGameText>();
            Check(canvasGo.transform.Find("Description").GetComponentInChildren<TMP_Text>(true) == label, "Authoring lookup must survive the scroll wrapper");
            action.power = 37; typeof(LocalizedGameText).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(binding, null);
            Check(label.text.Contains("+ 37"), "Open description did not react to setting changes");
            languageField.SetValue(null, GameLanguage.English); binding.Refresh(); Check(label.text.StartsWith("Element: Fire"), "Language switch did not rebuild a bound description");
            languageField.SetValue(null, GameLanguage.Vietnamese); binding.Refresh();
            var texture = new RenderTexture(960, 540, 24); texture.Create(); camera.targetTexture = texture;
            try
            {
                Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(label.rectTransform);
                var scroll = label.transform.parent.GetComponent<ActionDescriptionScroll>();
                Check(scroll != null && scroll.content.rect.height > scroll.viewport.rect.height, "Long descriptions must remain readable by scrolling");
                Render(camera, texture, Output + "/Description-Top-" + previewStamp + ".png");
                scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                Render(camera, texture, Output + "/Description-Bottom-" + previewStamp + ".png");
                Check(label.rectTransform.anchoredPosition.y > 1, "Scroll did not reach the hidden description lines");
                action.type = ActionData.ActionType.Item; action.description = "Item text retained.";
                GameLocalization.SetActionDescription(label, action); Check(label.text == action.description, "Item copy was replaced by skill statistics");
                GameLocalization.SetText(label, "Normal label"); binding.Refresh(); Check(label.text == "Normal label", "Switching to a normal text label retained the old skill binding");
            }
            finally { camera.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            report.AppendLine("PASS: live setting edits, VI/EN bound-text refresh, authored path lookup, long-text scrolling to bottom, item copy and clearing/reusing labels.");

            int count = 0; var examples = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:ActionData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); string before = Hash(path);
                var real = AssetDatabase.LoadAssetAtPath<ActionData>(path);
                examples.AppendLine(path + "\n" + CombatActionDescription.Build(real, GameLanguage.Vietnamese) + "\n" + CombatActionDescription.Build(real, GameLanguage.English) + "\n");
                Check(before == Hash(path), "Generating description wrote an action asset: " + path); count++;
            }
            File.WriteAllText(Output + "/ConfiguredSkills.txt", examples.ToString());
            report.AppendLine("PASS: generated both languages for " + count + " configured actions; action assets and player save files were not written.");
            File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[Skill Descriptions] " + report);
        }
        finally
        {
            languageField.SetValue(null, oldLanguage); CombatManager.Instance = oldManager;
            EditorSceneManager.ClosePreviewScene(preview);
            if (font != null) { foreach (var atlas in font.atlasTextures) if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas); UnityEngine.Object.DestroyImmediate(font.material); UnityEngine.Object.DestroyImmediate(font); }
            UnityEngine.Object.DestroyImmediate(action);
        }
    }
    static void Render(Camera camera, RenderTexture texture, string path)
    {
        var old = RenderTexture.active; Texture2D pixels = null;
        try { camera.Render(); RenderTexture.active = texture; pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG()); }
        finally { RenderTexture.active = old; if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); }
    }
    static string Hash(string path) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
}
