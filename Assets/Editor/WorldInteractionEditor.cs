using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Scene-authoring helpers only; the existing campaign system owns gameplay/save state.
[CustomEditor(typeof(WorldInteraction))]
public class WorldInteractionEditor : Editor
{
    [MenuItem("GameObject/Adventure/Save and Rest Point", false, 10)]
    static void Rest() => Create(WorldInteractionKind.RestPoint, "Save Point");
    [MenuItem("GameObject/Adventure/Chest", false, 11)]
    static void Chest() => Create(WorldInteractionKind.Chest, "Chest");
    [MenuItem("GameObject/Adventure/Item Pickup", false, 12)]
    static void Pickup() => Create(WorldInteractionKind.Pickup, "Item Pickup");
    [MenuItem("GameObject/Adventure/Enemy Encounter", false, 13)]
    static void Enemy() => Create(WorldInteractionKind.Encounter, "Enemy Encounter");
    [MenuItem("GameObject/Adventure/Discovery Area", false, 14)]
    static void Discovery() => Create(WorldInteractionKind.Discovery, "Discovery Area");

    static void Create(WorldInteractionKind kind, string label)
    {
        if (EditorApplication.isPlaying) return;
        var obj = new GameObject(label);
        Undo.RegisterCreatedObjectUndo(obj, "Create adventure point");
        if (Selection.activeTransform != null && !EditorUtility.IsPersistent(Selection.activeTransform))
        {
            obj.transform.SetParent(Selection.activeTransform, false);
        }
        else if (SceneView.lastActiveSceneView != null)
            obj.transform.position = SceneView.lastActiveSceneView.pivot;
        var point = Undo.AddComponent<WorldInteraction>(obj);
        point.persistentId = Guid.NewGuid().ToString("N");
        point.savedWorldId = "world:" + point.persistentId;
        point.kind = kind;
        point.displayName = kind == WorldInteractionKind.RestPoint ? "Lưu game / Hồi máu" : label;
        point.experience = kind == WorldInteractionKind.Encounter ? 100 : kind == WorldInteractionKind.Chest ? 30 : 0;
        Selection.activeGameObject = obj;
        EditorGUIUtility.PingObject(obj);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var point = (WorldInteraction)target;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Tên hiển thị"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("kind"), new GUIContent("Loại điểm"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("range"), new GUIContent("Khoảng cách tương tác"));
        var kind = (WorldInteractionKind)serializedObject.FindProperty("kind").enumValueIndex;
        if (kind != WorldInteractionKind.RestPoint)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("experience"), new GUIContent("EXP nhận được"));
            DrawRewards();
        }
        if (kind == WorldInteractionKind.Encounter)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enemyPrefabs"), new GUIContent("Enemy trong trận"), true);
            EditorGUILayout.HelpBox("Kéo prefab có BattleUnit vào danh sách. Mỗi phần tử là một enemy trong trận; có thể dùng cùng prefab nhiều lần.", MessageType.Info);
        }
        serializedObject.ApplyModifiedProperties();
        if (kind == WorldInteractionKind.Encounter && (point.enemyPrefabs.Count == 0 || point.enemyPrefabs.Any(x => x == null || !EditorUtility.IsPersistent(x))))
            EditorGUILayout.HelpBox("Cần gán prefab enemy từ Project cho tất cả các ô.", MessageType.Warning);
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Di chuyển điểm bằng Move Tool. Thêm model làm object con để hiển thị trong map. Vòng tròn Gizmos là phạm vi tương tác. Đến gần rồi bấm E; điểm nghỉ cần chọn Lưu / Hồi máu để ghi checkpoint.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Nhân bản thành vị trí mới"))
            {
                var copy = Instantiate(point.gameObject, point.transform.parent);
                Undo.RegisterCreatedObjectUndo(copy, "Duplicate adventure point");
                copy.name = point.gameObject.name + " Copy";
                copy.GetComponent<WorldInteraction>().persistentId = Guid.NewGuid().ToString("N");
                copy.GetComponent<WorldInteraction>().savedWorldId = "world:" + copy.GetComponent<WorldInteraction>().persistentId;
                copy.transform.position += Vector3.right * 3;
                Selection.activeGameObject = copy;
            }
            if (GUILayout.Button("Đặt xuống mặt đất")) SnapToGround(point);
            if (kind == WorldInteractionKind.Encounter && GUILayout.Button("Thêm model enemy vào map")) AddEnemyModels(point);
            var setup = FindObjectsByType<MapGameSetup>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == point.gameObject.scene);
            if (setup != null && setup.createStarterInteractions && GUILayout.Button("Tắt các điểm mẫu tự sinh trong scene này"))
            {
                Undo.RecordObject(setup, "Disable starter interactions");
                setup.createStarterInteractions = false;
                EditorUtility.SetDirty(setup);
                PrefabUtility.RecordPrefabInstancePropertyModifications(setup);
            }
        }
    }

    void DrawRewards()
    {
        var config = Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
        var items = config != null ? config.items.Where(x => x != null).ToArray() : new AdventureItem[0];
        var list = serializedObject.FindProperty("rewards");
        EditorGUILayout.LabelField("Vật phẩm nhận được", EditorStyles.boldLabel);
        for (int i = 0; i < list.arraySize; i++)
        {
            var row = list.GetArrayElementAtIndex(i);
            var id = row.FindPropertyRelative("id");
            var count = row.FindPropertyRelative("count");
            EditorGUILayout.BeginHorizontal();
            var names = new[] { "-- Chọn vật phẩm --" }.Concat(items.Select(x => x.displayName + " [" + x.id + "]")).ToArray();
            int current = Array.FindIndex(items, x => x.id == id.stringValue) + 1;
            int chosen = EditorGUILayout.Popup(current, names);
            if (chosen != current) id.stringValue = chosen == 0 ? "" : items[chosen - 1].id;
            count.intValue = Mathf.Max(1, EditorGUILayout.IntField(count.intValue, GUILayout.Width(55)));
            bool remove = GUILayout.Button("X", GUILayout.Width(24));
            EditorGUILayout.EndHorizontal();
            if (remove) { list.DeleteArrayElementAtIndex(i); break; }
            if (current == 0) EditorGUILayout.HelpBox("ID chưa hợp lệ: " + id.stringValue + ". Thêm item vào CampaignConfig rồi chọn lại.", MessageType.Warning);
        }
        if (GUILayout.Button("Thêm vật phẩm"))
        {
            list.arraySize++;
            var row = list.GetArrayElementAtIndex(list.arraySize - 1);
            row.FindPropertyRelative("id").stringValue = items.Length > 0 ? items[0].id : "";
            row.FindPropertyRelative("count").intValue = 1;
        }
    }

    static void SnapToGround(WorldInteraction point)
    {
        var player = FindFirstObjectByType<PlayerScript>();
        int mask = player != null && player.surfaceLayer.value != 0 ? player.surfaceLayer.value : Physics.DefaultRaycastLayers;
        var hits = Physics.RaycastAll(point.transform.position + Vector3.up * 10, Vector3.down, 200, mask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits.OrderBy(x => x.distance))
        {
            if (hit.transform.IsChildOf(point.transform)) continue;
            Undo.RecordObject(point.transform, "Ground adventure point");
            point.transform.position = hit.point;
            PrefabUtility.RecordPrefabInstancePropertyModifications(point.transform);
            return;
        }
        Debug.LogWarning("Không tìm thấy mặt đất bên dưới điểm này.", point);
    }

    static void AddEnemyModels(WorldInteraction point)
    {
        if (point.transform.Find("Enemy Visuals") != null)
        {
            Debug.LogWarning("Đã có Enemy Visuals. Xóa nhóm này trước khi tạo lại model.", point);
            return;
        }
        var prefabs = point.enemyPrefabs.Where(x => x != null && EditorUtility.IsPersistent(x)).ToArray();
        if (prefabs.Length == 0) return;
        var root = new GameObject("Enemy Visuals");
        Undo.RegisterCreatedObjectUndo(root, "Create enemy visuals");
        root.transform.SetParent(point.transform, false);
        for (int i = 0; i < prefabs.Length; i++)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i].gameObject, root.transform);
            Undo.RegisterCreatedObjectUndo(model, "Create enemy visual");
            model.transform.localPosition = Vector3.right * (i - (prefabs.Length - 1) * .5f) * 1.5f;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) { behaviour.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour); }
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) { collider.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(collider); }
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; PrefabUtility.RecordPrefabInstancePropertyModifications(body); }
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) { animator.applyRootMotion = false; PrefabUtility.RecordPrefabInstancePropertyModifications(animator); }
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        }
    }

    void OnSceneGUI()
    {
        var point = (WorldInteraction)target;
        Handles.Label(point.transform.position + Vector3.up, point.displayName + " • " + point.kind);
    }
}
