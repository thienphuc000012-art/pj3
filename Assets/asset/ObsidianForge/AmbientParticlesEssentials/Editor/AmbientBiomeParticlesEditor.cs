using UnityEditor;
using UnityEngine;

namespace ObsidianForge.AmbientParticlesEssentials.Editor
{
    [CustomEditor(typeof(AmbientBiomeParticles))]
    public sealed class AmbientBiomeParticlesEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor profileEditor;
        private bool showStyle;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var effect = (AmbientBiomeParticles)target;
            if (effect.Profile == null) return;
            EditorGUILayout.Space();
            showStyle = EditorGUILayout.Foldout(showStyle,"Style brightness and independent layers",true);
            if (!showStyle) return;
            EditorGUILayout.HelpBox("These settings belong to the selected profile asset. Duplicate it for a private variant. Runtime changes through code: call RefreshSettings().",MessageType.Info);
            CreateCachedEditor(effect.Profile,null,ref profileEditor);
            profileEditor.OnInspectorGUI();
        }

        private void OnDisable()
        {
            if (profileEditor != null) DestroyImmediate(profileEditor);
        }
    }
}
