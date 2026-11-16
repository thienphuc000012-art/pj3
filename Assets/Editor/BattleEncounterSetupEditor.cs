using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleEncounterSetup)), CanEditMultipleObjects]
public sealed class BattleEncounterSetupEditor : Editor
{
    SerializedObject cameraProperties;
    CameraManager[] sceneCameraManagers;
    bool showCameraSettings = true;

    void OnEnable() { EditorApplication.hierarchyChanged += InvalidateSceneManagers; }
    void InvalidateSceneManagers() => sceneCameraManagers = null;

    bool ShowLegacyCameras()
    {
        if (sceneCameraManagers == null)
            sceneCameraManagers = FindObjectsByType<CameraManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return targets.OfType<BattleEncounterSetup>().Any(setup => sceneCameraManagers.Any(manager =>
            manager != null && manager.gameObject.scene == setup.gameObject.scene && !manager.DynamicCombatCameraEnabled));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        bool showLegacy = ShowLegacyCameras();
        var property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (!showLegacy && (property.propertyPath == nameof(BattleEncounterSetup.battleStartCamera) ||
                property.propertyPath == nameof(BattleEncounterSetup.enemyAoECamera) ||
                property.propertyPath == nameof(BattleEncounterSetup.enemyPhase2Camera))) continue;
            using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                EditorGUILayout.PropertyField(property, true);
            if (property.propertyPath == "cameraSettings") DrawCameraSettings(property);
        }
        serializedObject.ApplyModifiedProperties();
    }

    void DrawCameraSettings(SerializedProperty property)
    {
        if (property.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox("Các encounter đang dùng cấu hình camera khác nhau. Chọn một encounter để chỉnh chi tiết.", MessageType.Info);
            return;
        }
        var profile = property.objectReferenceValue as CombatCameraSettings;
        using (new EditorGUI.DisabledScope(targets.Length != 1 || Application.isPlaying))
            if (GUILayout.Button(profile != null ? "Tạo bản camera riêng từ cấu hình này..." : "Tạo Camera Settings riêng cho encounter..."))
            {
                var setup = (BattleEncounterSetup)target;
                var source = profile != null ? profile : FindDefaultSettings(setup);
                string name = string.IsNullOrWhiteSpace(setup.setupId) ? "Encounter" : setup.setupId;
                string path = EditorUtility.SaveFilePanelInProject("Lưu cấu hình camera encounter", name + "_CameraSettings", "asset", "Chọn nơi lưu cấu hình camera riêng.");
                if (!string.IsNullOrEmpty(path))
                {
                    var created = CreateProfileCopy(source);
                    created.name = System.IO.Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(created, path);
                    property.objectReferenceValue = created;
                    profile = created;
                    EditorGUIUtility.PingObject(created);
                }
            }
        if (profile == null)
        {
            EditorGUILayout.HelpBox("Đang dùng CombatCameraSettings chung trên CombatCameraDirector. Tạo bản riêng để chỉnh riêng encounter này.", MessageType.Info);
            return;
        }
        showCameraSettings = EditorGUILayout.Foldout(showCameraSettings, "Camera Settings — " + profile.name, true);
        if (!showCameraSettings) return;
        EditorGUILayout.HelpBox("Áp dụng cho Dynamic Combat Camera. Thay đổi được lưu vào asset camera đang gán; các encounter dùng cùng asset sẽ dùng chung thông số.", MessageType.Info);
        if (cameraProperties == null || cameraProperties.targetObject != profile)
        {
            cameraProperties?.Dispose(); cameraProperties = new SerializedObject(profile);
        }
        using (new EditorGUI.IndentLevelScope())
        {
            cameraProperties.Update();
            DrawPropertiesExcluding(cameraProperties, "m_Script");
            cameraProperties.ApplyModifiedProperties();
        }
    }

    internal static CombatCameraSettings FindDefaultSettings(BattleEncounterSetup setup)
    {
        var director = FindObjectsByType<CombatCameraDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(d => d.gameObject.scene == setup.gameObject.scene && d.ActiveSettings != null);
        return director != null ? director.ActiveSettings : AssetDatabase.LoadAssetAtPath<CombatCameraSettings>("Assets/Adventure/CombatCameraSettings.asset");
    }
    internal static CombatCameraSettings CreateProfileCopy(CombatCameraSettings source) =>
        source != null ? Instantiate(source) : CreateInstance<CombatCameraSettings>();
    void OnDisable()
    {
        EditorApplication.hierarchyChanged -= InvalidateSceneManagers;
        cameraProperties?.Dispose(); cameraProperties = null;
    }
}
