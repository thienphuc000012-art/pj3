using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionData))]
public sealed class ActionDataDescriptionEditor : Editor
{
    GameLanguage previewLanguage;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var action = (ActionData)target;
        if (action.type == ActionData.ActionType.Item) DrawPropertiesExcluding(serializedObject, "m_Script");
        else DrawPropertiesExcluding(serializedObject, "m_Script", "description");
        serializedObject.ApplyModifiedProperties();
        if (action.type == ActionData.ActionType.Item) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Description — tự tạo từ chỉ số", EditorStyles.boldLabel);
        previewLanguage = (GameLanguage)EditorGUILayout.EnumPopup("Preview Language", previewLanguage);
        string description = CombatActionDescription.Build(action, previewLanguage);
        float height = EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(description), Mathf.Max(200, EditorGUIUtility.currentViewWidth - 46));
        EditorGUILayout.SelectableLabel(description, EditorStyles.wordWrappedLabel, GUILayout.Height(height + 12));
    }
}
