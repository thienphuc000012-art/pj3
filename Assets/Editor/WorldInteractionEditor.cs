using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Scene-authoring helpers only;
// existing CampaignSession owns gameplay/save state.
[CustomEditor(typeof(WorldInteraction))]
public class WorldInteractionEditor : Editor
{
    private const string DefaultChestPrefabPath = "Assets/asset/chest/B-01/Free Treasure Chest/Prefabs/Gold Chest.prefab";
    private const string DefaultChestOpenAnimationPath = "Assets/asset/chest/B-01/Free Treasure Chest/open.anim";

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


    [MenuItem("GameObject/Adventure/Dialogue NPC", false, 15)]
    static void DialogueNpc()
        => Create(
            WorldInteractionKind.DialogueNPC,
            "Dialogue NPC"
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
        if (kind == WorldInteractionKind.Encounter) Undo.AddComponent<EnemyEncounterBehaviour>(obj);
        if (kind == WorldInteractionKind.RestPoint) RestPointSetup.Configure(point);
        if (kind == WorldInteractionKind.Chest) AssignDefaultChestAssets(point);


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

        if (kind == WorldInteractionKind.Chest) UpdatePickupModel(point);


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
            serializedObject.FindProperty("questMarkerId"),
            new GUIContent("Quest Marker ID")
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

        if (kind == WorldInteractionKind.MapTransition)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Chuyển sang Map", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("targetMapScene"),
                new GUIContent("Scene đích"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("targetMapPosition"),
                new GUIContent("Target Map Position"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("targetMapEuler"),
                new GUIContent("Góc xoay khi đến"));
            EditorGUILayout.HelpBox(
                "Target Map Position là tọa độ World trong scene đích. Chọn một điểm đứng an toàn trên địa hình của scene đó rồi nhập X, Y, Z tại đây.",
                MessageType.Info);
        }


        // ========================================================
        // EXP + REWARDS
        // ========================================================

        if (kind == WorldInteractionKind.DialogueNPC)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Story & Quest Dialogue", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogue"), new GUIContent("First One-Time Dialogue"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("progressionDialogues"), new GUIContent("Locked Follow-Up Dialogues"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultDialogue"), new GUIContent("Repeatable Default Dialogue"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("questRepeatableDialogues"), new GUIContent("Thoại lặp theo nhiệm vụ"), true);
            EditorGUILayout.HelpBox(
                "Ưu tiên thoại đầu tiên → Locked Follow-Up Dialogues đủ điều kiện → Thoại lặp theo nhiệm vụ → Default Dialogue. Trong Thoại lặp theo nhiệm vụ, gán Quest, In Progress Dialogue (đã nhận nhưng chưa xong) và Completed Dialogue (đã xong, kể cả đã nhận thưởng). Các mục này luôn được lặp, không bị Play Once trong asset chặn. Nếu có nhiều nhiệm vụ thì kiểm tra từ trên xuống. Chưa nhận Quest hoặc bỏ trống thoại của trạng thái hiện tại thì dùng mục tiếp theo hoặc Default Dialogue. Follow-up chỉ mở khi điều kiện Required Completed Quest IDs hoặc Required Completed Dialogue IDs đã hoàn tất.",
                MessageType.Info);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("NPC 3D Model & Animation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueNpcModelPrefab"), new GUIContent("NPC Model Prefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueNpcModelPosition"), new GUIContent("Model Local Position"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueNpcModelRotation"), new GUIContent("Model Local Rotation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueNpcModelScale"), new GUIContent("Model Local Scale"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueFaceHeight"), new GUIContent("Fallback Face Height"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueTalkAnimationState"), new GUIContent("NPC Talk State"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueNpcIdleAnimationStates"), new GUIContent("NPC Idle States"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueNpcIdleMinSeconds"), new GUIContent("Idle Switch Min Seconds"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueNpcIdleMaxSeconds"), new GUIContent("Idle Switch Max Seconds"));
            EditorGUILayout.HelpBox(
                "Gán prefab model NPC; model được đặt làm con của điểm hội thoại. NPC Talk State là tên state trong Animator (ví dụ Base Layer.Talking). Thêm nhiều state vào NPC Idle States (ví dụ Base Layer.Idle, Base Layer.LookAround) để NPC đổi ngẫu nhiên khi không giao tiếp. Nếu model có rig Humanoid, camera tự lấy bone Head; nếu không, dùng Fallback Face Height. Trong Dialogue Asset, Player Talk Animation State chạy khi Player nói; Player Dialogue Idle Animation State giữ Player ở idle khi NPC nói. Hội thoại không dừng thời gian game.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Tạo asset bằng Create > Adventure > Story Dialogue. Thêm các node có ID riêng; node không có Choice sẽ tiếp tục khi bấm màn hình. Text của Choice là câu người chơi nói. Mỗi lựa chọn có thể nhận nhiệm vụ. Thêm quest asset vào Campaign Config > Quests để tiến độ còn dùng được sau khi đổi map hoặc tải lại game. Có thể đặt EXP và Item Rewards trên quest; thưởng chỉ được nhận một lần khi hoàn thành.",
                MessageType.Info);
        }

        if (kind != WorldInteractionKind.RestPoint && kind != WorldInteractionKind.DialogueNPC)
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

        if (kind == WorldInteractionKind.Pickup || kind == WorldInteractionKind.Chest)
        {
            EditorGUILayout.Space(6);
            bool chest = kind == WorldInteractionKind.Chest;
            EditorGUILayout.LabelField(chest ? "Model 3D và animation của rương" : "Model 3D của item", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupModelPrefab"), new GUIContent(chest ? "Chest Model Prefab" : "Model Prefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupModelPosition"), new GUIContent("Vị trí model"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupModelRotation"), new GUIContent("Góc xoay model"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupModelScale"), new GUIContent("Kích thước model"));
            if (chest)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("chestOpenAnimation"), new GUIContent("Chest open animation"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("chestPlayerAnimationState"), new GUIContent("Animation openchest của player"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("chestUseAnimationEvent"), new GUIContent("Mở bằng Animation Event"));
                if (!serializedObject.FindProperty("chestUseAnimationEvent").boolValue)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("chestOpenMoment"), new GUIContent("Mốc mở rương trong animation"));
                else EditorGUILayout.HelpBox("Thêm Animation Event tên AnimEvent_OpenChest tại frame player chạm rương trong clip openchest.", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("requireFrontAccess"), new GUIContent("Chỉ mở từ phía trước"));
                if (serializedObject.FindProperty("requireFrontAccess").boolValue)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("chestFrontAccessHalfAngle"), new GUIContent("Góc tiếp cận phía trước"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("chestOpenDuration"), new GUIContent("Thời gian mở rương"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("chestLidPath"), new GUIContent("Tên / path nắp rương"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("chestAnimateLidFallback"), new GUIContent("Mở nắp dự phòng"));
                if (serializedObject.FindProperty("chestAnimateLidFallback").boolValue)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("chestLidOpenRotation"), new GUIContent("Góc mở nắp"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupAnimationTimeout"), new GUIContent("Giới hạn chờ animation"));
                EditorGUILayout.HelpBox("Chest mới tự dùng Gold Chest và clip open. Hướng forward của object rương xác định mặt trước. Clip open hiện chưa có đường cong chuyển động, nên nắp Top_Lod được mở bằng góc dự phòng. Rương đã mở sẽ biến mất và tiếp tục được ẩn sau khi tải save.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupAnimationState"), new GUIContent("Animation nhặt item"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupUseAnimationEvent"), new GUIContent("Nhặt bằng Animation Event"));
                if (!serializedObject.FindProperty("pickupUseAnimationEvent").boolValue)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupMoment"), new GUIContent("Mốc nhặt trong animation"));
                else EditorGUILayout.HelpBox("Thêm Animation Event tên AnimEvent_PickupItem tại frame tay chạm item trong clip pickupitem.", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupAnimationTimeout"), new GUIContent("Giới hạn chờ animation"));
                EditorGUILayout.HelpBox("Kéo prefab model từ Project vào đây. Model hiển thị trong Scene và tự ẩn khi nhặt item. Phần Rewards quyết định vật phẩm nhận được.", MessageType.Info);
            }
        }
        bool changed = serializedObject.ApplyModifiedProperties();
        if ((kind == WorldInteractionKind.Pickup || kind == WorldInteractionKind.Chest) && !EditorApplication.isPlaying)
        {
            bool useDefaultChest = kind == WorldInteractionKind.Chest && GUILayout.Button("Dùng Gold Chest mặc định");
            if (useDefaultChest) AssignDefaultChestAssets(point);
            bool updateModel = GUILayout.Button(kind == WorldInteractionKind.Chest ? "Cập nhật model rương" : "Cập nhật model 3D");
            if (changed || updateModel) UpdatePickupModel(point);
            if (useDefaultChest) UpdatePickupModel(point);
        }
        if (kind == WorldInteractionKind.Encounter && point.GetComponent<EnemyEncounterBehaviour>() == null && GUILayout.Button("Add Patrol / Chase Behaviour"))
            Undo.AddComponent<EnemyEncounterBehaviour>(point.gameObject);


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
            (kind == WorldInteractionKind.DialogueNPC
                ? "Đến gần NPC rồi bấm E để nói chuyện. Dùng Dialogue Asset để viết nhánh hội thoại và gắn lựa chọn nhận nhiệm vụ."
                : "Di chuyển điểm bằng Move Tool. " +
            "Thêm model làm object con để hiển thị trong map. " +
            "Vòng tròn Gizmos là phạm vi tương tác. " +
            "Đến gần rồi bấm E; điểm nghỉ cần chọn " +
            "Lưu / Hồi máu để ghi checkpoint."),
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

    static void AssignDefaultChestAssets(WorldInteraction point)
    {
        if (point == null) return;
        Undo.RecordObject(point, "Assign default chest assets");
        point.pickupModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultChestPrefabPath);
        point.chestOpenAnimation = AssetDatabase.LoadAssetAtPath<AnimationClip>(DefaultChestOpenAnimationPath);
        point.chestPlayerAnimationState = "Base Layer.openchest";
        point.chestLidPath = "Top_Lod";
        point.chestAnimateLidFallback = true;
        point.chestLidOpenRotation = new Vector3(-105f, 0f, 0f);
        EditorUtility.SetDirty(point);
    }

    static void UpdatePickupModel(WorldInteraction point)
    {
        var prefab = point.pickupModelPrefab;
        if (prefab != null && (!EditorUtility.IsPersistent(prefab) || prefab.GetComponentInChildren<WorldInteraction>(true) != null))
        {
            Debug.LogWarning("Hãy chọn prefab model từ Project, không chứa WorldInteraction để tránh tạo điểm nhặt lồng nhau.", point);
            return;
        }
        Undo.RecordObject(point, "Update pickup model");
        if (point.pickupModelInstance != null && point.pickupModelSource != prefab)
        {
            Undo.DestroyObjectImmediate(point.pickupModelInstance);
            point.pickupModelInstance = null;
        }
        if (point.pickupModelInstance == null && prefab != null)
        {
            point.pickupModelInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, point.transform);
            Undo.RegisterCreatedObjectUndo(point.pickupModelInstance, "Create pickup model");
            point.pickupModelInstance.name = point.kind == WorldInteractionKind.Chest ? "Chest Model" : "Pickup Model";
        }
        point.pickupModelSource = prefab;
        if (point.pickupModelInstance != null)
        {
            Undo.RecordObject(point.pickupModelInstance.transform, "Move pickup model");
            point.ApplyPickupModelTransform();
            PrefabUtility.RecordPrefabInstancePropertyModifications(point.pickupModelInstance.transform);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(point);
        EditorUtility.SetDirty(point);
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
