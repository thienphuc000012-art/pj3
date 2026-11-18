using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

/// <summary>Repairs the two dynamic font caches and checks the actual Windows Player compiler.</summary>
[InitializeOnLoad]
public static class PlayerBuildRepair
{
    const string Request = "Library/CombatValidation/PlayerBuildRepair.request";
    const string Output = "Tools/BuildValidation";
    static readonly string[] Fonts = { "Assets/MangaIntro/Fonts/MangaIntro SDF.asset", "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset" };
    static StringBuilder report;
    static readonly List<string> errors = new List<string>();
    static bool pending;
    static double nextStep;
    static PlayerBuildRepair() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || AnimationMode.InAnimationMode()) return;
        if (pending)
        {
            if (EditorApplication.timeSinceStartup < nextStep) return;
            pending = false;
            try { ValidateAndCompile(); } catch (Exception error) { Fail(error); }
            return;
        }
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        try { Repair(); } catch (Exception error) { Fail(error); }
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Capture(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || text.Contains("generated inconsistent result")) errors.Add(text);
    }
    static void Write() { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Validation.txt", report.ToString()); }
    static void Fail(Exception error)
    {
        Application.logMessageReceived -= Capture; pending = false;
        report ??= new StringBuilder(); report.AppendLine("FAILED: " + error); foreach (string message in errors) report.AppendLine(message);
        Write(); Debug.LogException(error);
    }
    [MenuItem("Tools/Build/Repair Fonts and Validate Windows Player Scripts")]
    public static void Repair()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode && !AnimationMode.InAnimationMode(), "Use Edit Mode outside animation preview.");
        report = new StringBuilder(); errors.Clear(); Application.logMessageReceived -= Capture; Application.logMessageReceived += Capture;
        string backup = Output + "/BeforeFontRepair-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        string intro = string.Concat(MangaIntroLayout.Create().SelectMany(chapter => chapter.panels).SelectMany(panel => panel.dialogue).Select(line => line.text));
        string characters = new string(Enumerable.Range(32, 95).Concat(Enumerable.Range(0x1EA0, 0x5A)).Select(value => (char)value).ToArray()) +
            "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúýĂăĐđĨĩŨũƠơƯư•…–—" + intro;
        characters = new string(characters.Where(character => !char.IsControl(character)).Distinct().OrderBy(character => character).ToArray());
        foreach (string path in Fonts)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); Check(font != null && font.sourceFontFile != null, "Font/source missing: " + path);
            string name = Path.GetFileNameWithoutExtension(path); File.Copy(path, backup + "/" + name + ".asset"); File.Copy(path + ".meta", backup + "/" + name + ".asset.meta");
            string guid = AssetDatabase.AssetPathToGUID(path);
            var serialized = new SerializedObject(font); var clear = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            Check(clear != null, "Missing TMP clear-on-build property."); clear.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
            font.ClearFontAssetData(false);
            // Clear-on-build can leave extra atlas sub-assets outside the font's atlas list.
            // These are generated caches; keep the primary atlas and its stable file ID.
            foreach (var atlas in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>())
                if (!font.atlasTextures.Contains(atlas)) UnityEngine.Object.DestroyImmediate(atlas, true);
            Check(font.TryAddCharacters(characters, out string missing), "Missing font characters: " + path + " | " + missing);
            Check(font.atlasTexture != null && font.atlasTexture.width > 1 && font.characterTable.Count > 0, "Font cache was not regenerated: " + path);
            foreach (var atlas in font.atlasTextures.Take(font.atlasTextureCount)) { Check(atlas != null && atlas.width > 1, "Invalid active atlas."); EditorUtility.SetDirty(atlas); }
            if (font.material != null) EditorUtility.SetDirty(font.material);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            Check(AssetDatabase.AssetPathToGUID(path) == guid, "Font GUID must not change.");
            report.AppendLine("REPAIRED: " + path + " | glyphs=" + font.glyphTable.Count + " | atlases=" + font.atlasTextureCount + " | Dynamic kept; Clear Dynamic Data On Build=false; GUID retained.");
        }
        report.AppendLine("Backups: " + backup);
        report.AppendLine("Waiting for TMP post-render imports before the Player compilation check."); Write();
        pending = true; nextStep = EditorApplication.timeSinceStartup + 3;
        EditorApplication.QueuePlayerLoopUpdate(); UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
    static void ValidateAndCompile()
    {
        foreach (string path in Fonts)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Check(!new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild").boolValue, "Clear-on-build must stay off.");
            string before = Hash(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Check(Hash(path) == before, "Repeated imports must not rewrite font data: " + path);
            var reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Check(reloaded.characterTable.Count > 0 && reloaded.HasCharacters("Nhiệm vụ • Tàn tích thức tỉnh"), "Vietnamese glyphs must survive reimport.");
            report.AppendLine("PASS: two forced native imports; font bytes stable and Vietnamese glyphs available: " + path);
        }
        Check(errors.Count == 0, "Font repair/import emitted an error: " + string.Join("\n", errors));
        report.AppendLine("Compiling non-Editor Windows64 Player scripts..."); Write();
        string output = "Library/BuildValidation/Windows64PlayerScripts"; Directory.CreateDirectory(output);
        var settings = new ScriptCompilationSettings { target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.None };
        var result = PlayerBuildInterface.CompilePlayerScripts(settings, output);
        Check(result.assemblies != null && result.assemblies.Count > 0 && File.Exists(output + "/Assembly-CSharp.dll"), "The Player compiler returned no game assembly.");
        Check(errors.Count == 0, "Player compiler errors: " + string.Join("\n", errors));
        report.AppendLine("PASS: actual Windows64 Player script compiler, " + result.assemblies.Count + " assemblies; Assembly-CSharp.dll produced without Editor-only APIs.");
        report.AppendLine("No Play Mode, real campaign save, scene layout or original build output modified. This check compiles Player scripts; it does not package all scenes into an executable.");
        Application.logMessageReceived -= Capture; Write(); Debug.Log("Player build repair PASS: " + Output + "/Validation.txt");
    }
}
