using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Measures the rendered layout on inert copies, preserving the authored combat Canvas.</summary>
[InitializeOnLoad]
public static class EnemyBuffLayoutValidation
{
    const string Request = "Library/CombatValidation/EnemyBuffLayout.request";
    const string Output = "Tools/CanvasUI/EnemyBuffLayout.txt";
    static EnemyBuffLayoutValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/combat/AdvancedUIManager.cs") ||
            File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/EnemyBuffLayoutValidation.cs")) return;
        File.Delete(Request);
        try { Run(); } catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, "FAILED\n" + error); Debug.LogException(error); }
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [MenuItem("Tools/Combat/Validate Enemy Buff Layout")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode.");
        var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        var combat = SceneManager.GetSceneByPath("Assets/Scenes/combattest.unity"); bool preview = !combat.IsValid() || !combat.isLoaded;
        if (preview) combat = EditorSceneManager.OpenPreviewScene("Assets/Scenes/combattest.unity");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var root = new GameObject("Buff layout fixture", typeof(RectTransform), typeof(Canvas));
        var report = new StringBuilder();
        try
        {
            var source = combat.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<AdvancedUIManager>(true)).Single();
            var authored = (RectTransform)source.enemyBuffContainer;
            var row = new GameObject("Enemy Buff Row", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>(); row.SetParent(root.transform, false);
            row.anchorMin = authored.anchorMin; row.anchorMax = authored.anchorMax; row.pivot = authored.pivot;
            row.anchoredPosition = authored.anchoredPosition; row.sizeDelta = authored.sizeDelta; row.localScale = authored.localScale; row.localRotation = authored.localRotation;
            var manager = root.AddComponent<AdvancedUIManager>(); manager.enemyBuffContainer = row; manager.buffDebuffIconSize = source.buffDebuffIconSize; manager.buffDebuffIconSpacing = source.buffDebuffIconSpacing;
            typeof(AdvancedUIManager).GetField("battleUIVisible", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, true);
            var ensure = typeof(AdvancedUIManager).GetMethod("EnsureEnemyBuffContainer", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (float parentScale in new[] { .25f, 1f, 1.6f })
            foreach (float height in new[] { authored.rect.height, 127f })
            foreach (int count in new[] { 0, 1, 2, 8, 16 })
            {
                foreach (Transform child in row.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                root.transform.localScale = Vector3.one * parentScale;
                row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                Vector2 position = row.anchoredPosition, size = row.sizeDelta, anchorMin = row.anchorMin, anchorMax = row.anchorMax, pivot = row.pivot;
                Vector3 scale = row.localScale;
                for (int i = 0; i < count; i++)
                {
                    var icon = UnityEngine.Object.Instantiate(source.buffIconPrefab, row);
                    icon.SetActive(true); icon.transform.localScale = Vector3.one * .25f;
                }
                ensure.Invoke(manager, null); Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(row);
                Check(row.anchoredPosition == position && row.sizeDelta == size && row.anchorMin == anchorMin && row.anchorMax == anchorMax && row.pivot == pivot && row.localScale == scale, "Authored row geometry must not be overridden.");
                float previousRight = float.NegativeInfinity;
                foreach (RectTransform icon in row)
                {
                    var corners = new Vector3[4]; icon.GetWorldCorners(corners); var left = row.InverseTransformPoint(corners[0]); var right = row.InverseTransformPoint(corners[2]);
                    Check(icon.localScale == Vector3.one && left.x >= row.rect.xMin - .01f && right.x <= row.rect.xMax + .01f && left.y >= row.rect.yMin - .01f, "Icons must fit the authored row at every parent scale.");
                    Check(Mathf.Abs(right.y - row.rect.yMax) < .01f && left.x >= previousRight - .01f, "Icons must touch the top edge and never overlap.");
                    previousRight = right.x;
                }
                report.AppendLine($"PASS: icons={count}, row height={height:F3}, parent scale={parentScale:F2}; authored transform preserved, top-aligned, fits bounds.");
            }
            report.AppendLine("PASS: empty rows, buff/debuff shared prefab, crowded rows and non-unit parent scales. No Play Mode, live Canvas or campaign save changes.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, report.ToString()); Debug.Log("Enemy buff layout PASS: " + Output);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.CloseScene(scene, true);
            if (preview) EditorSceneManager.ClosePreviewScene(combat);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            Check(active.isDirty == dirty, "Preserve unsaved scene state.");
        }
    }
}
