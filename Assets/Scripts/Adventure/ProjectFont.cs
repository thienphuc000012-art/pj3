using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ProjectFont
{
    static TMP_FontAsset font;
    static TMP_FontAsset previousDefault;
    static Font source;
    static readonly HashSet<TMP_Text> pending = new HashSet<TMP_Text>();
    static readonly List<TMP_Text> processing = new List<TMP_Text>();
    static ProjectFontUpdater updater;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged);
        if (font != null && TMP_Settings.defaultFontAsset == font)
            TMP_Settings.defaultFontAsset = previousDefault;
        font = null;
        pending.Clear();
        processing.Clear();
        var prefab = Resources.Load<AdventureCanvasRoot>("Adventure/UI/AdventureCanvas");
        source = prefab != null ? prefab.languageFont : null;
        if (source == null) return;
        var fallback = TMP_Settings.defaultFontAsset;
        previousDefault = fallback;
        font = TMP_FontAsset.CreateFontAsset(source);
        if (font == null) return;
        font.name = "Cormorant Garamond UI";
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.isMultiAtlasTexturesEnabled = true;
        if (fallback != null && fallback != font)
        {
            if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            font.fallbackFontAssetTable.Add(fallback);
        }
        TMP_Settings.defaultFontAsset = font;
        if (updater == null)
        {
            var host = new GameObject("Project Font Updates");
            Object.DontDestroyOnLoad(host);
            updater = host.AddComponent<ProjectFontUpdater>();
        }
        SceneManager.sceneLoaded += SceneLoaded;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TextChanged);
    }

    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects()) ApplyHierarchy(root);
    }

    static bool NeedsFontUpdate(TMP_Text text)
    {
        if (text == null || font == null) return false;
        if (text.font != font) return true;
        var material = text.fontSharedMaterial;
        return material == null || material.mainTexture != font.atlasTexture;
    }

    static void ApplyText(TMP_Text text)
    {
        if (!NeedsFontUpdate(text)) return;
        if (text.font != font) text.font = font;
        // A material using a different atlas renders unrelated pieces of glyphs.
        if (text.fontSharedMaterial == null || text.fontSharedMaterial.mainTexture != font.atlasTexture)
            text.fontSharedMaterial = font.material;
    }

    static void TextChanged(Object changed)
    {
        // TMP fires this event while rebuilding its mesh. Changing the font there
        // leaves the old glyph UVs on the new atlas until another rebuild.
        if (changed is TMP_Text text && NeedsFontUpdate(text)) pending.Add(text);
    }

    internal static void FlushPending()
    {
        if (pending.Count == 0) return;
        processing.Clear();
        processing.AddRange(pending);
        pending.Clear();
        foreach (var text in processing) ApplyText(text);
        processing.Clear();
    }

    public static void ApplyHierarchy(GameObject root)
    {
        if (root == null || font == null) return;
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            ApplyText(text);
        foreach (var text in root.GetComponentsInChildren<Text>(true))
            if (text.font != source) text.font = source;
    }
}

// Runs before the canvas rebuild rather than mutating a TMP object inside its text-changed event.
public sealed class ProjectFontUpdater : MonoBehaviour
{
    void Update() => ProjectFont.FlushPending();
}
