using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class TobyMaterialValidation
{
    const string Request = "Library/CombatValidation/TobyMaterials.request";
    static TobyMaterialValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        var report = new StringBuilder();
        try
        {
            const string root = "Assets/asset/Toby Fredson/";
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { root.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => new[] { "RHEWPScene_Bioms", "RHEWPScene_Demo", "RHEWPScene_Demo Dawn" }.Contains(Path.GetFileNameWithoutExtension(p))).ToArray();
            if (scenes.Length != 3) throw new Exception("Expected exactly three requested scenes");
            var paths = scenes.SelectMany(p => AssetDatabase.GetDependencies(p, true)).Distinct().ToArray();
            int count = 0, errors = 0;
            foreach (var path in paths.Where(p => p.EndsWith(".mat")))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                // The publisher's URP package still contains the legacy pollen particle material.
                if (path.StartsWith(root) && mat.shader != null && mat.shader.name.StartsWith("Legacy Shaders/Particles/"))
                {
                    var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (shader == null) throw new Exception("URP particle shader missing");
                    var backup = "Library/CombatValidation/TobyLegacyBackup/" + path;
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    var texture = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    var color = mat.HasProperty("_TintColor") ? mat.GetColor("_TintColor") : Color.white;
                    bool additive = mat.shader.name.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0;
                    mat.shader = shader; mat.shaderKeywords = new string[0];
                    mat.SetTexture("_BaseMap", texture); mat.SetColor("_BaseColor", color);
                    mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", additive ? 2 : 0);
                    mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                    mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Cull", 0);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000;
                    mat.SetOverrideTag("RenderType", "Transparent");
                    EditorUtility.SetDirty(mat); report.AppendLine("Converted legacy particle: " + path);
                }
                count++;
                bool bad = mat.shader == null || !mat.shader.isSupported || ShaderUtil.ShaderHasError(mat.shader);
                if (bad) errors++;
                report.AppendLine((bad ? "ERROR " : "OK ") + path + " => " + (mat.shader == null ? "MISSING" : mat.shader.name));
                if (mat.shader != null && ShaderUtil.ShaderHasError(mat.shader))
                    foreach (var message in ShaderUtil.GetShaderMessages(mat.shader)) report.AppendLine(message.message);
            }
            AssetDatabase.SaveAssets();
            report.AppendLine("Scenes: " + scenes.Length + "; materials: " + count + "; errors: " + errors);
            report.AppendLine("Visual scene verification is still required.");
        }
        catch (Exception e) { report.AppendLine(e.ToString()); }
        File.WriteAllText("Library/CombatValidation/TobyMaterials.result.txt", report.ToString());
    }
}
