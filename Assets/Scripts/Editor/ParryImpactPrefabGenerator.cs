using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class ParryImpactPrefabGenerator
{
    private const string OutputFolder = "Assets/Resources/Combat";
    private const string TextureFolder = OutputFolder + "/Textures";
    private const string MaterialFolder = OutputFolder + "/Materials";
    private const string PrefabPath = OutputFolder + "/PF_Parry_Impact.prefab";
    private const int GeneratorVersion = 4;
    private const int ClawTrailAssetVersion = 1;
    private static string GeneratorVersionKey => "ParryImpactPrefabGenerator.Version." + Application.dataPath;
    private static string ClawTrailVersionKey => "ClawTrailMaterialGenerator.Version." + Application.dataPath;

    static ParryImpactPrefabGenerator()
    {
        EditorApplication.delayCall += CreatePrefabIfMissing;
        EditorApplication.delayCall += CreateClawTrailMaterialIfNeeded;
    }

    private static void CreatePrefabIfMissing()
    {
        if (EditorPrefs.GetInt(GeneratorVersionKey, 0) >= GeneratorVersion &&
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        BuildPrefab();
    }

    [MenuItem("Tools/Combat/Rebuild Realistic Parry Impact VFX")]
    private static void RebuildPrefab()
    {
        BuildPrefab();
    }

    private static void BuildPrefab()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(OutputFolder);
        EnsureFolder(TextureFolder);
        EnsureFolder(MaterialFolder);

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogError("[Parry VFX] URP Particles/Unlit shader was not found; the parry prefab was not generated.");
            return;
        }

        Texture2D sparkTexture = CreateTexture("T_Parry_Spark.png", 64, 16, SparkAlpha);
        // The mesh already defines the ring silhouette. Keep this texture solid so
        // the alpha texture does not make the mesh ring look like a hairline.
        Texture2D ringTexture = CreateTexture("T_Parry_Ring.png", 64, 64, OpaqueAlpha, true);
        Texture2D flashTexture = CreateTexture("T_Parry_Flash.png", 64, 64, FlashAlpha);
        Texture2D dustTexture = CreateTexture("T_Parry_Dust.png", 32, 32, DustAlpha);
        Texture2D clawTrailTexture = CreateTexture("T_Enemy_Claw_Trail.png", 32, 16, ClawTrailAlpha, true);

        Material sparkMaterial = CreateMaterial("M_Parry_Sparks.mat", shader, sparkTexture, new Color(1f, .96f, .86f, 1f));
        Material ringMaterial = CreateMaterial("M_Parry_Shockwave.mat", shader, ringTexture, Color.white);
        Material flashMaterial = CreateMaterial("M_Parry_Flash.mat", shader, flashTexture, new Color(1f, .98f, .92f, 1f));
        Material dustMaterial = CreateMaterial("M_Parry_Dust.mat", shader, dustTexture, new Color(.82f, .84f, .84f, 1f));
        CreateMaterial("M_Enemy_Claw_Trail.mat", shader, clawTrailTexture, Color.white);
        Mesh ringMesh = CreateRingMesh();

        GameObject root = new GameObject("PF_Parry_Impact");
        try
        {
            CreateSparkSystem(root.transform, sparkMaterial);
            CreateShockwaveSystem(root.transform, ringMaterial, ringMesh);
            CreateFlashSystem(root.transform, flashMaterial);
            CreateDustSystem(root.transform, dustMaterial);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetInt(GeneratorVersionKey, GeneratorVersion);
        Debug.Log("[Parry VFX] Generated Assets/Resources/Combat/PF_Parry_Impact.prefab with sparks, white shockwave, flash, and soft dust.");
    }

    private static void CreateClawTrailMaterialIfNeeded()
    {
        string materialPath = MaterialFolder + "/M_Enemy_Claw_Trail.mat";
        if (EditorPrefs.GetInt(ClawTrailVersionKey, 0) >= ClawTrailAssetVersion &&
            AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null) return;

        EnsureFolder("Assets/Resources");
        EnsureFolder(OutputFolder);
        EnsureFolder(TextureFolder);
        EnsureFolder(MaterialFolder);

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogError("[Claw Trail VFX] URP Particles/Unlit shader was not found; the trail material was not updated.");
            return;
        }

        Texture2D trailTexture = CreateTexture("T_Enemy_Claw_Trail.png", 32, 16, ClawTrailAlpha, true);
        CreateMaterial("M_Enemy_Claw_Trail.mat", shader, trailTexture, Color.white);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetInt(ClawTrailVersionKey, ClawTrailAssetVersion);
        Debug.Log("[Claw Trail VFX] Updated the fine, feathered URP trail material.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static Texture2D CreateTexture(string fileName, int width, int height, Func<float, float, float> alpha, bool overwrite = false)
    {
        string path = TextureFolder + "/" + fileName;
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null && !overwrite) return existing;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = Path.GetFileNameWithoutExtension(fileName),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < height; y++)
        {
            float v = (y + .5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v))));
            }
        }
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static float SparkAlpha(float u, float v)
    {
        float x = u * 2f - 1f;
        float y = v * 2f - 1f;
        float taper = Mathf.Pow(Mathf.Clamp01(1f - x * x), .45f);
        float core = Mathf.Exp(-y * y * 34f);
        float faintEdge = .16f * Mathf.Exp(-y * y * 5f);
        return Mathf.Clamp01(taper * (core + faintEdge));
    }

    private static float OpaqueAlpha(float u, float v) => 1f;

    private static float ClawTrailAlpha(float u, float v)
    {
        float across = Mathf.Abs(v - .5f);
        float sharpCore = Mathf.Exp(-across * across * 720f) * .88f;
        float softFeather = Mathf.Exp(-across * across * 82f) * .2f;
        float endTaper = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 12f);
        return Mathf.Clamp01((sharpCore + softFeather) * endTaper);
    }

    private static float FlashAlpha(float u, float v)
    {
        float x = u * 2f - 1f;
        float y = v * 2f - 1f;
        float radiusSquared = x * x + y * y;
        return Mathf.Exp(-radiusSquared * 5.5f) * Mathf.Clamp01((1.1f - Mathf.Sqrt(radiusSquared)) * 9f);
    }

    private static float DustAlpha(float u, float v)
    {
        float x = u * 2f - 1f;
        float y = v * 2f - 1f;
        return Mathf.Pow(Mathf.Clamp01(1f - x * x - y * y), 1.8f);
    }

    private static Material CreateMaterial(string fileName, Shader shader, Texture2D texture, Color tint)
    {
        string path = MaterialFolder + "/" + fileName;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(fileName) };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh CreateRingMesh()
    {
        const string path = OutputFolder + "/M_Parry_RingMesh.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

        const int segments = 48;
        var vertices = new Vector3[segments * 2];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle);
            float y = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(x * .9f, y * .9f, 0f);
            vertices[i * 2 + 1] = new Vector3(x, y, 0f);
            uv[i * 2] = uv[i * 2 + 1] = new Vector2((x + 1f) * .5f, (y + 1f) * .5f);

            int next = (i + 1) % segments;
            int t = i * 6;
            triangles[t] = i * 2;
            triangles[t + 1] = i * 2 + 1;
            triangles[t + 2] = next * 2;
            triangles[t + 3] = next * 2;
            triangles[t + 4] = i * 2 + 1;
            triangles[t + 5] = next * 2 + 1;
        }

        if (mesh == null)
            mesh = new Mesh { name = "M_Parry_RingMesh" };
        else
            mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) == null)
            AssetDatabase.CreateAsset(mesh, path);
        else
            EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static GameObject CreateParticleChild(Transform parent, string name)
    {
        var child = new GameObject(name, typeof(ParticleSystem), typeof(ParticleSystemRenderer));
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child;
    }

    private static void ConfigureMain(ParticleSystem ps, float duration, float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax, int maxParticles)
    {
        var main = ps.main;
        main.duration = duration;
        main.loop = false;
        main.prewarm = false;
        main.playOnAwake = true;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.83f, .85f, .87f, 1f), Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.gravityModifier = 0f;
        main.maxParticles = maxParticles;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
    }

    private static void SetBurst(ParticleSystem ps, short min, short max)
    {
        ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, min, max) });
    }

    private static void SetFadeGradient(ParticleSystem ps, Color color, float startAlpha, float endAlpha)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(endAlpha, 1f) });
        var module = ps.colorOverLifetime;
        module.enabled = true;
        module.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void SetSizeCurve(ParticleSystem ps, float endScale)
    {
        var module = ps.sizeOverLifetime;
        module.enabled = true;
        module.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(.7f, Mathf.Lerp(1f, endScale, .7f)), new Keyframe(1f, endScale)));
    }

    private static void CreateSparkSystem(Transform root, Material material)
    {
        var ps = CreateParticleChild(root, "PS_Metal_Sparks").GetComponent<ParticleSystem>();
        ConfigureMain(ps, .25f, .14f, .32f, 4.5f, 10f, .04f, .075f, 32);
        SetBurst(ps, 18, 26);
        SetFadeGradient(ps, new Color(1f, .96f, .86f), 1f, 0f);
        SetSizeCurve(ps, 0f);
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 42f;
        shape.radius = .03f;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3.4f;
        renderer.velocityScale = .18f;
        renderer.material = material;
    }

    private static void CreateShockwaveSystem(Transform root, Material material, Mesh mesh)
    {
        var ps = CreateParticleChild(root, "PS_White_Shockwave").GetComponent<ParticleSystem>();
        ConfigureMain(ps, .34f, .3f, .34f, 0f, 0f, .34f, .42f, 1);
        SetBurst(ps, 1, 1);
        SetFadeGradient(ps, new Color(.98f, .99f, 1f), .7f, 0f);
        SetSizeCurve(ps, 4.4f);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.material = material;
    }

    private static void CreateFlashSystem(Transform root, Material material)
    {
        var ps = CreateParticleChild(root, "PS_Impact_Flash").GetComponent<ParticleSystem>();
        ConfigureMain(ps, .12f, .085f, .12f, 0f, 0f, .48f, .62f, 1);
        SetBurst(ps, 1, 1);
        SetFadeGradient(ps, Color.white, 1f, 0f);
        SetSizeCurve(ps, .55f);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = material;
    }

    private static void CreateDustSystem(Transform root, Material material)
    {
        var ps = CreateParticleChild(root, "PS_Dust").GetComponent<ParticleSystem>();
        ConfigureMain(ps, .5f, .3f, .5f, .35f, 1f, .07f, .13f, 10);
        SetBurst(ps, 5, 8);
        SetFadeGradient(ps, new Color(.88f, .9f, .92f), .5f, 0f);
        SetSizeCurve(ps, .6f);
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .07f;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = material;
    }
}
