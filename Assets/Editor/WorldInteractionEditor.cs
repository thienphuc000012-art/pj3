using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Scene-authoring helpers only;
// existing CampaignSession owns gameplay/save state.
[CustomEditor(typeof(WorldInteraction))]
public class WorldInteractionEditor : Editor
{
    // ============================================================
    // CREATE MENU
    // ============================================================

    [MenuItem("GameObject/Adventure/Save and Rest Point", false, 10)]
    static void Rest()
        => Create(
            WorldInteractionKind.RestPoint,
            "Save Point"
        );


    [MenuItem("GameObject/Adventure/Chest", false, 11)]
    static void Chest()
        => Create(
            WorldInteractionKind.Chest,
            "Chest"
        );


    [MenuItem("GameObject/Adventure/Item Pickup", false, 12)]
    static void Pickup()
        => Create(
            WorldInteractionKind.Pickup,
            "Item Pickup"
        );


    [MenuItem("GameObject/Adventure/Enemy Encounter", false, 13)]
    static void Enemy()
        => Create(
            WorldInteractionKind.Encounter,
            "Enemy Encounter"
        );


    [MenuItem("GameObject/Adventure/Discovery Area", false, 14)]
    static void Discovery()
        => Create(
            WorldInteractionKind.Discovery,
            "Discovery Area"
        );


    // ============================================================
    // CREATE POINT
    // ============================================================

    static void Create(
        WorldInteractionKind kind,
        string label)
    {
        if (EditorApplication.isPlaying)
            return;


        GameObject obj =
            new GameObject(label);


        Undo.RegisterCreatedObjectUndo(
            obj,
            "Create adventure point"
        );


        // Nếu đang chọn một GameObject trong Scene
        // -> tạo Adventure Point làm con.
        if (Selection.activeTransform != null &&
            !EditorUtility.IsPersistent(
                Selection.activeTransform))
        {
            obj.transform.SetParent(
                Selection.activeTransform,
                false
            );
        }
        else if (
            SceneView.lastActiveSceneView != null)
        {
            obj.transform.position =
                SceneView.lastActiveSceneView.pivot;
        }


        WorldInteraction point =
            Undo.AddComponent<WorldInteraction>(obj);


        // ========================================================
        // SAVE ID
        // ========================================================

        point.persistentId =
            Guid.NewGuid().ToString("N");


        point.savedWorldId =
            "world:" + point.persistentId;


        // ========================================================
        // BASIC DATA
        // ========================================================

        point.kind = kind;


        point.displayName =
            kind == WorldInteractionKind.RestPoint
            ? "Lưu game / Hồi máu"
            : label;


        point.experience =
            kind == WorldInteractionKind.Encounter
            ? 100
            : kind == WorldInteractionKind.Chest
                ? 30
                : 0;


        // ========================================================
        // COMBAT SETUP
        // ========================================================

        if (kind == WorldInteractionKind.Encounter)
        {
            point.combatSetupId = "default";
        }


        EditorUtility.SetDirty(point);


        Selection.activeGameObject = obj;

        EditorGUIUtility.PingObject(obj);
    }


    // ============================================================
    // CUSTOM INSPECTOR
    // ============================================================

    public override void OnInspectorGUI()
    {
        serializedObject.Update();


        WorldInteraction point =
            (WorldInteraction)target;


        SerializedProperty displayName =
            serializedObject.FindProperty(
                "displayName"
            );


        SerializedProperty kindProperty =
            serializedObject.FindProperty(
                "kind"
            );


        SerializedProperty range =
            serializedObject.FindProperty(
                "range"
            );


        SerializedProperty experience =
            serializedObject.FindProperty(
                "experience"
            );


        SerializedProperty enemyPrefabs =
            serializedObject.FindProperty(
                "enemyPrefabs"
            );


        SerializedProperty combatSetupId =
            serializedObject.FindProperty(
                "combatSetupId"
            );


        // ========================================================
        // GENERAL
        // ========================================================

        EditorGUILayout.PropertyField(
            displayName,
            new GUIContent("Tên hiển thị")
        );


        EditorGUILayout.PropertyField(
            kindProperty,
            new GUIContent("Loại điểm")
        );


        EditorGUILayout.PropertyField(
            range,
            new GUIContent("Khoảng cách tương tác")
        );


        WorldInteractionKind kind =
            (WorldInteractionKind)
            kindProperty.enumValueIndex;


        // ========================================================
        // EXP + REWARDS
        // ========================================================

        if (kind != WorldInteractionKind.RestPoint)
        {
            EditorGUILayout.PropertyField(
                experience,
                new GUIContent("EXP nhận được")
            );


            DrawRewards();
        }


        // ========================================================
        // ENCOUNTER SETTINGS
        // ========================================================

        if (kind == WorldInteractionKind.Encounter)
        {
            EditorGUILayout.Space(6);


            EditorGUILayout.LabelField(
                "Enemy Encounter",
                EditorStyles.boldLabel
            );


            // ----------------------------------------------------
            // ENEMY PREFABS
            // ----------------------------------------------------

            EditorGUILayout.PropertyField(
                enemyPrefabs,
                new GUIContent("Enemy trong trận"),
                true
            );


            EditorGUILayout.HelpBox(
                "Kéo prefab có BattleUnit vào danh sách. " +
                "Mỗi phần tử là một enemy trong trận; " +
                "có thể dùng cùng prefab nhiều lần.",
                MessageType.Info
            );


            // ----------------------------------------------------
            // COMBAT SETUP ID
            // ----------------------------------------------------

            EditorGUILayout.Space(8);


            EditorGUILayout.LabelField(
                "Combat Scene Setup",
                EditorStyles.boldLabel
            );


            if (combatSetupId != null)
            {
                EditorGUILayout.PropertyField(
                    combatSetupId,
                    new GUIContent(
                        "Combat Setup ID"
                    )
                );


                EditorGUILayout.HelpBox(
                    "ID này phải giống Setup Id của " +
                    "BattleEncounterSetup trong scene combat.\n\n" +
                    "Ví dụ:\n" +
                    "mountain_dragon_01",
                    MessageType.Info
                );


                if (string.IsNullOrWhiteSpace(
                    combatSetupId.stringValue))
                {
                    EditorGUILayout.HelpBox(
                        "Combat Setup ID đang để trống.",
                        MessageType.Warning
                    );
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Không tìm thấy biến combatSetupId trong " +
                    "WorldInteraction.cs.",
                    MessageType.Error
                );
            }
        }


        // ========================================================
        // APPLY
        // ========================================================

        serializedObject.ApplyModifiedProperties();


        // ========================================================
        // ENEMY PREFAB VALIDATION
        // ========================================================

        if (kind == WorldInteractionKind.Encounter &&
            (
                point.enemyPrefabs.Count == 0 ||
                point.enemyPrefabs.Any(
                    x =>
                        x == null ||
                        !EditorUtility.IsPersistent(x)
                )
            ))
        {
            EditorGUILayout.HelpBox(
                "Cần gán prefab enemy từ Project " +
                "cho tất cả các ô.",
                MessageType.Warning
            );
        }


        // ========================================================
        // INFO
        // ========================================================

        EditorGUILayout.Space();


        EditorGUILayout.HelpBox(
            "Di chuyển điểm bằng Move Tool. " +
            "Thêm model làm object con để hiển thị trong map. " +
            "Vòng tròn Gizmos là phạm vi tương tác. " +
            "Đến gần rồi bấm E; điểm nghỉ cần chọn " +
            "Lưu / Hồi máu để ghi checkpoint.",
            MessageType.Info
        );


        // ========================================================
        // EDITOR BUTTONS
        // ========================================================

        using (
            new EditorGUI.DisabledScope(
                EditorApplication.isPlaying))
        {
            // ----------------------------------------------------
            // DUPLICATE
            // ----------------------------------------------------

            if (GUILayout.Button(
                "Nhân bản thành vị trí mới"))
            {
                GameObject copy =
                    Instantiate(
                        point.gameObject,
                        point.transform.parent
                    );


                Undo.RegisterCreatedObjectUndo(
                    copy,
                    "Duplicate adventure point"
                );


                copy.name =
                    point.gameObject.name +
                    " Copy";


                WorldInteraction copyPoint =
                    copy.GetComponent<
                        WorldInteraction>();


                copyPoint.persistentId =
                    Guid.NewGuid()
                        .ToString("N");


                copyPoint.savedWorldId =
                    "world:" +
                    copyPoint.persistentId;


                // Giữ nguyên combatSetupId.
                //
                // Nếu muốn encounter copy dùng battle setup
                // khác thì đổi Combat Setup ID trên Inspector.


                copy.transform.position +=
                    Vector3.right * 3f;


                EditorUtility.SetDirty(copyPoint);


                Selection.activeGameObject =
                    copy;
            }


            // ----------------------------------------------------
            // SNAP TO GROUND
            // ----------------------------------------------------

            if (GUILayout.Button(
                "Đặt xuống mặt đất"))
            {
                SnapToGround(point);
            }


            // ----------------------------------------------------
            // ADD ENEMY MODEL
            // ----------------------------------------------------

            if (kind ==
                    WorldInteractionKind.Encounter &&
                GUILayout.Button(
                    "Thêm model enemy vào map"))
            {
                AddEnemyModels(point);
            }


            // ----------------------------------------------------
            // STARTER INTERACTION
            // ----------------------------------------------------

            MapGameSetup setup =
                FindObjectsByType<MapGameSetup>(
                    FindObjectsSortMode.None
                )
                .FirstOrDefault(
                    x =>
                        x.gameObject.scene ==
                        point.gameObject.scene
                );


            if (setup != null &&
                setup.createStarterInteractions &&
                GUILayout.Button(
                    "Tắt các điểm mẫu tự sinh trong scene này"))
            {
                Undo.RecordObject(
                    setup,
                    "Disable starter interactions"
                );


                setup.createStarterInteractions =
                    false;


                EditorUtility.SetDirty(setup);


                PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        setup
                    );
            }
        }
    }


    // ============================================================
    // REWARDS
    // ============================================================

    void DrawRewards()
    {
        CampaignConfig config =
            Resources.Load<CampaignConfig>(
                "Adventure/CampaignConfig"
            );


        AdventureItem[] items =
            config != null
            ? config.items
                .Where(x => x != null)
                .ToArray()
            : new AdventureItem[0];


        SerializedProperty list =
            serializedObject.FindProperty(
                "rewards"
            );


        EditorGUILayout.LabelField(
            "Vật phẩm nhận được",
            EditorStyles.boldLabel
        );


        for (int i = 0;
             i < list.arraySize;
             i++)
        {
            SerializedProperty row =
                list.GetArrayElementAtIndex(i);


            SerializedProperty id =
                row.FindPropertyRelative(
                    "id"
                );


            SerializedProperty count =
                row.FindPropertyRelative(
                    "count"
                );


            EditorGUILayout.BeginHorizontal();


            string[] names =
                new[]
                {
                    "-- Chọn vật phẩm --"
                }
                .Concat(
                    items.Select(
                        x =>
                            x.displayName +
                            " [" +
                            x.id +
                            "]"
                    )
                )
                .ToArray();


            int current =
                Array.FindIndex(
                    items,
                    x =>
                        x.id ==
                        id.stringValue
                ) + 1;


            int chosen =
                EditorGUILayout.Popup(
                    current,
                    names
                );


            if (chosen != current)
            {
                id.stringValue =
                    chosen == 0
                    ? ""
                    : items[chosen - 1].id;
            }


            count.intValue =
                Mathf.Max(
                    1,
                    EditorGUILayout.IntField(
                        count.intValue,
                        GUILayout.Width(55)
                    )
                );


            bool remove =
                GUILayout.Button(
                    "X",
                    GUILayout.Width(24)
                );


            EditorGUILayout.EndHorizontal();


            if (remove)
            {
                list.DeleteArrayElementAtIndex(i);
                break;
            }


            if (current == 0)
            {
                EditorGUILayout.HelpBox(
                    "ID chưa hợp lệ: " +
                    id.stringValue +
                    ". Thêm item vào CampaignConfig rồi chọn lại.",
                    MessageType.Warning
                );
            }
        }


        if (GUILayout.Button(
            "Thêm vật phẩm"))
        {
            list.arraySize++;


            SerializedProperty row =
                list.GetArrayElementAtIndex(
                    list.arraySize - 1
                );


            row.FindPropertyRelative(
                    "id")
                .stringValue =
                    items.Length > 0
                    ? items[0].id
                    : "";


            row.FindPropertyRelative(
                    "count")
                .intValue = 1;
        }
    }


    // ============================================================
    // SNAP TO GROUND
    // ============================================================

    static void SnapToGround(
        WorldInteraction point)
    {
        PlayerScript player =
            FindFirstObjectByType<PlayerScript>();


        int mask =
            player != null &&
            player.surfaceLayer.value != 0

            ? player.surfaceLayer.value

            : Physics.DefaultRaycastLayers;


        RaycastHit[] hits =
            Physics.RaycastAll(
                point.transform.position +
                Vector3.up * 10f,

                Vector3.down,

                200f,

                mask,

                QueryTriggerInteraction.Ignore
            );


        foreach (
            RaycastHit hit
            in hits.OrderBy(
                x => x.distance))
        {
            if (hit.transform.IsChildOf(
                point.transform))
            {
                continue;
            }


            Undo.RecordObject(
                point.transform,
                "Ground adventure point"
            );


            point.transform.position =
                hit.point;


            PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    point.transform
                );


            return;
        }


        Debug.LogWarning(
            "Không tìm thấy mặt đất bên dưới điểm này.",
            point
        );
    }


    // ============================================================
    // ADD ENEMY VISUAL
    // ============================================================

    static void AddEnemyModels(
        WorldInteraction point)
    {
        if (point.transform.Find(
            "Enemy Visuals") != null)
        {
            Debug.LogWarning(
                "Đã có Enemy Visuals. " +
                "Xóa nhóm này trước khi tạo lại model.",
                point
            );


            return;
        }


        BattleUnit[] prefabs =
            point.enemyPrefabs
                .Where(
                    x =>
                        x != null &&
                        EditorUtility.IsPersistent(x)
                )
                .ToArray();


        if (prefabs.Length == 0)
            return;


        GameObject root =
            new GameObject(
                "Enemy Visuals"
            );


        Undo.RegisterCreatedObjectUndo(
            root,
            "Create enemy visuals"
        );


        root.transform.SetParent(
            point.transform,
            false
        );


        for (int i = 0;
             i < prefabs.Length;
             i++)
        {
            GameObject model =
                (GameObject)
                PrefabUtility.InstantiatePrefab(
                    prefabs[i].gameObject,
                    root.transform
                );


            Undo.RegisterCreatedObjectUndo(
                model,
                "Create enemy visual"
            );


            model.transform.localPosition =
                Vector3.right *
                (
                    i -
                    (prefabs.Length - 1) *
                    0.5f
                ) *
                1.5f;


            // Tắt scripts
            foreach (
                MonoBehaviour behaviour
                in model.GetComponentsInChildren<
                    MonoBehaviour>(true))
            {
                behaviour.enabled = false;


                PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        behaviour
                    );
            }


            // Tắt collider
            foreach (
                Collider collider
                in model.GetComponentsInChildren<
                    Collider>(true))
            {
                collider.enabled = false;


                PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        collider
                    );
            }


            // Rigidbody
            foreach (
                Rigidbody body
                in model.GetComponentsInChildren<
                    Rigidbody>(true))
            {
                body.isKinematic = true;


                PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        body
                    );
            }


            // Animator
            foreach (
                Animator animator
                in model.GetComponentsInChildren<
                    Animator>(true))
            {
                animator.applyRootMotion =
                    false;


                PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        animator
                    );
            }


            PrefabUtility
                .RecordPrefabInstancePropertyModifications(
                    model.transform
                );
        }
    }


    // ============================================================
    // SCENE LABEL
    // ============================================================

    void OnSceneGUI()
    {
        WorldInteraction point =
            (WorldInteraction)target;


        Handles.Label(
            point.transform.position +
            Vector3.up,

            point.displayName +
            " • " +
            point.kind
        );
    }
}