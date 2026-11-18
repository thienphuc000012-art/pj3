using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Matches the enemy element icon to the active player icon in Canvas units.</summary>
[InitializeOnLoad]
public static class EnemyElementIconSetup
{
    const string Request = "Library/CombatValidation/EnemyElementIcon.request";
    const string Output = "Tools/CanvasUI/EnemyElementIcon.txt";
    static EnemyElementIconSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || AnimationMode.InAnimationMode()) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/EnemyElementIconSetup.cs")) return;
        File.Delete(Request);
        try { Apply(); } catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, "FAILED\n" + error); Debug.LogException(error); }
    }
    // Exclude the Canvas' device-dependent scale; compare both HUDs in the same reference units.
    static Vector2 SizeOnCanvas(Transform node, Vector2 size, PartyTurnHUDCard card = null)
    {
        Vector3 horizontal = Vector3.right * size.x, vertical = Vector3.up * size.y;
        while (node != null && node.GetComponent<Canvas>() == null)
        {
            Vector3 scale = card != null && node == card.visualRoot
                ? Vector3.one * (card.style != null ? card.style.activeScale : 1.22f) : node.localScale;
            horizontal = node.localRotation * Vector3.Scale(horizontal, scale);
            vertical = node.localRotation * Vector3.Scale(vertical, scale);
            node = node.parent;
        }
        return new Vector2(horizontal.magnitude, vertical.magnitude);
    }
    [MenuItem("Tools/Combat/Match Enemy Element Icon to Player")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        const string path = "Assets/Scenes/combattest.unity";
        var active = SceneManager.GetActiveScene(); var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AdvancedUIManager>(true)).Single();
            var fill = manager.enemyHpFill;
            if (fill == null) throw new InvalidOperationException("Enemy HP fill is missing.");
            Vector2 target = Vector2.zero;
            foreach (var hud in manager.partyHUDList)
            {
                if (hud?.elementIcon == null) continue;
                Vector2 size = SizeOnCanvas(hud.elementIcon.transform, hud.elementIcon.rectTransform.rect.size, hud.turnCard);
                target = Vector2.Max(target, size);
            }
            Vector2 parentScale = SizeOnCanvas(fill.transform.parent, Vector2.one);
            Vector3 iconScale = manager.enemyElementIcon != null ? manager.enemyElementIcon.transform.localScale : Vector3.one;
            if (target.x <= 0 || target.y <= 0 || parentScale.x <= 0 || parentScale.y <= 0 || Mathf.Abs(iconScale.x) < .001f || Mathf.Abs(iconScale.y) < .001f)
                throw new InvalidOperationException("Player or enemy HUD has no measurable size.");
            Vector2 desired = new Vector2(target.x / parentScale.x / Mathf.Abs(iconScale.x), target.y / parentScale.y / Mathf.Abs(iconScale.y));
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            string backup = "Tools/CanvasUI/BeforeEnemyElementIcon-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            File.Copy(path, backup + "-disk.unity");
            if (!EditorSceneManager.SaveScene(scene, backup + "-live.unity", true)) throw new IOException("Could not back up the live scene.");
            Vector2 previous = manager.enemyElementIconSize;
            Undo.RecordObject(manager, "Match enemy element icon to player"); manager.enemyElementIconSize = desired; EditorUtility.SetDirty(manager);
            if (manager.enemyElementIcon != null)
            {
                var rect = manager.enemyElementIcon.rectTransform;
                Undo.RecordObject(rect, "Resize enemy element icon"); rect.sizeDelta = desired;
                // Same placement formula as the runtime HUD; retain its right edge and vertical offset.
                if (rect.parent == fill.transform.parent)
                    rect.anchoredPosition = fill.rectTransform.anchoredPosition + new Vector2(-fill.rectTransform.rect.width * .5f - desired.x * .5f, 0) + manager.enemyElementIconOffset;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save combattest.");
            File.WriteAllText(Output, "PASS: enemy element icon matches active player size.\nPrevious local size: " + previous + "\nNew local size: " + desired.ToString("F4") + "\nRendered size in Canvas units: " + target.ToString("F4") + "\nEnemy parent scale: " + parentScale + "\nScene: " + path + "\nBackup: " + backup + "\n");
            Debug.Log("Enemy element icon resized: " + desired + " (same displayed size as player).");
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded && !scene.isDirty) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }
}
