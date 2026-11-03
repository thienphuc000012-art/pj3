using UnityEditor;
using UnityEngine;

public static class AdventureScanSetup
{
    [MenuItem("Tools/Adventure/Scan/Select Shared Scan Settings")]
    public static void SelectSettings()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<AdventureScanSettings>("Assets/Resources/AdventureScanSettings.asset");
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Tools/Adventure/Scan/Add Scanner To Selected Player")]
    public static void AddScanner()
    {
        var player = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<PlayerScript>() : null;
        if (player == null) { Debug.LogWarning("Chọn object có PlayerScript trong map khám phá."); return; }
        var scanner = player.GetComponent<AdventureScanner>();
        if (scanner == null) scanner = Undo.AddComponent<AdventureScanner>(player.gameObject);
        Undo.RecordObject(scanner, "Assign scan settings");
        scanner.settings = AssetDatabase.LoadAssetAtPath<AdventureScanSettings>("Assets/Resources/AdventureScanSettings.asset");
        scanner.scanOrigin = player.transform;
        EditorUtility.SetDirty(scanner);
        Selection.activeObject = scanner;
    }

    [MenuItem("Tools/Adventure/Scan/Add Scan Target To Selection")]
    public static void AddTarget()
    {
        foreach (GameObject go in Selection.gameObjects)
            if (go.GetComponent<AdventureScanTarget>() == null) Undo.AddComponent<AdventureScanTarget>(go);
    }
}
