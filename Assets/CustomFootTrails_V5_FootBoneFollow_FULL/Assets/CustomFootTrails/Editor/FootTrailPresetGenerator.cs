#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace CustomFootTrails.Editor
{
    /// <summary>Builds 16 original foot trail prefabs and their materials in the editor.</summary>
    public static class FootTrailPresetGenerator
    {
        // Locate this generator wherever the user imported the package.
        // The previous version assumed Assets/CustomFootTrails and failed
        // whenever the ZIP was extracted inside an additional folder.
        private static string Root;
        private static string Generated;

        private static string LocatePackageRoot()
        {
            // First: locate the actual location of this Editor script.
            string[] scripts = AssetDatabase.FindAssets("FootTrailPresetGenerator t:MonoScript");
            const string scriptSuffix = "/Editor/FootTrailPresetGenerator.cs";
            foreach (string guid in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!path.EndsWith(scriptSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string root = path.Substring(0, path.Length - scriptSuffix.Length);
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(
                        root + "/Textures/FT_SoftCloud.png") != null)
                    return root;
            }

            // Fallback: look for the PNG itself, without assuming an install folder.
            string[] textures = AssetDatabase.FindAssets("FT_SoftCloud t:Texture2D");
            const string textureSuffix = "/Textures/FT_SoftCloud.png";
            foreach (string guid in textures)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!path.EndsWith(textureSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string root = path.Substring(0, path.Length - textureSuffix.Length);
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(
                        root + scriptSuffix) != null)
                    return root;
            }

            return null;
        }

        private struct Style
        {
            public string name, primaryTexture, accentTexture;
            public Color primary, accent;
            public int cloudCount, accentCount;
            public float width, rise, lifetime;
            public bool luminous;
            public Style(string name, string pTex, string aTex, string c1, string c2,
                int clouds, int accents, float width, float rise, float lifetime, bool luminous)
            {
                this.name = name;
                primaryTexture = pTex;
                accentTexture = aTex;
                ColorUtility.TryParseHtmlString(c1, out primary);
                ColorUtility.TryParseHtmlString(c2, out accent);
                cloudCount = clouds;
                accentCount = accents;
                this.width = width;
                this.rise = rise;
                this.lifetime = lifetime;
                this.luminous = luminous;
            }
        }

        private static readonly Style[] Styles =
        {
            new Style("01_Earth_Dust",     "FT_SoftCloud", "FT_SoftGlow", "#A78A6C", "#EAC8A1", 7, 3, 0.28f, .35f, .75f, false),
            new Style("02_Crimson_Ash",    "FT_SoftCloud", "FT_Streak",    "#8F373F", "#FF6961", 6, 6, 0.22f, .50f, .85f, true),
            new Style("03_Fire_Embers",    "FT_SoftGlow",  "FT_Streak",    "#F46C28", "#FFD16B", 6, 12,0.17f, .95f, .60f, true),
            new Style("04_Frost_Mist",     "FT_SoftCloud", "FT_Star",      "#A8DCEB", "#D9FCFF", 6, 7, 0.30f, .28f, 1.05f,true),
            new Style("05_Arcane_Cyan",    "FT_SoftGlow",  "FT_Star",      "#2EDCEB", "#C0FEFF", 4, 11,0.22f, .65f, .85f, true),
            new Style("06_Violet_Wisp",    "FT_SoftCloud", "FT_Star",      "#8550CF", "#DAB8FF", 5, 10,0.26f, .62f, .95f, true),
            new Style("07_Poison_Spore",   "FT_SoftCloud", "FT_SoftGlow", "#5DA65C", "#D8EF63", 6, 10,0.23f, .46f,1.05f, true),
            new Style("08_Holy_Motes",     "FT_SoftGlow",  "FT_Star",      "#FFE7A2", "#FFFFFF", 5, 12,0.19f, .66f, .90f, true),
            new Style("09_Shadow_Smoke",   "FT_SoftCloud", "FT_SoftGlow", "#615975", "#8B62C5", 7, 6, 0.30f, .34f,1.15f, true),
            new Style("10_Forest_Leaves",  "FT_SoftCloud", "FT_Leaf",      "#7D8B58", "#C8A55D", 4, 8, 0.24f, .52f,1.00f, false),
            new Style("11_Rose_Petals",    "FT_SoftCloud", "FT_Petal",     "#B65C69", "#FAABB6", 3, 10,0.24f, .57f,1.12f, false),
            new Style("12_Starfall",       "FT_SoftGlow",  "FT_Star",      "#627DFA", "#FFFFFF", 5, 14,0.17f, .78f, .80f, true),
            new Style("13_Golden_Trail",   "FT_SoftGlow",  "FT_Streak",    "#E3AD51", "#FFF6B7", 5, 13,0.21f, .58f, .72f, true),
            new Style("14_Lunar_Dust",     "FT_SoftCloud", "FT_Star",      "#90A3CC", "#D4DDFB", 6, 8, 0.28f, .37f,1.12f, true),
            new Style("15_Emerald_Magic",  "FT_SoftGlow",  "FT_Leaf",      "#3AC79A", "#B4FFD5", 5, 10,0.21f, .59f, .95f, true),
            new Style("16_Thunder_Sparks", "FT_SoftGlow",  "FT_Streak",    "#508FFB", "#C8EDFF", 3, 16,0.16f,1.10f, .55f, true)
        };

        [MenuItem("Tools/Foot Trails/Generate 16 Original Presets")]
        public static void Generate()
        {
            Root = LocatePackageRoot();
            if (string.IsNullOrEmpty(Root))
            {
                Debug.LogError("Foot Trails: cannot locate the generator and the PNG textures together. " +
                    "Keep Editor, Shaders, Scripts and Textures under the same CustomFootTrails folder.");
                return;
            }
            Generated = Root + "/Generated";
            Debug.Log("Foot Trails: using package folder: " + Root);

            EnsureFolder(Generated);
            EnsureFolder(Generated + "/Materials");
            EnsureFolder(Generated + "/Prefabs");

            bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            Shader shader = Shader.Find(urp
                ? "FootTrails/ParticleURP"
                : "FootTrails/ParticleBuiltIn");

            if (shader == null)
            {
                Debug.LogError("Foot Trails: shader not found. Check the Shaders folder and console errors.");
                return;
            }

            int built = 0;
            foreach (Style style in Styles)
            {
                Texture2D cloudTexture = LoadTexture(style.primaryTexture);
                Texture2D accentTexture = LoadTexture(style.accentTexture);
                if (cloudTexture == null || accentTexture == null)
                {
                    Debug.LogError("Foot Trails: missing PNG texture in " + style.name +
                        " (checked: " + Root + "/Textures/" + style.primaryTexture + ".png and " +
                        Root + "/Textures/" + style.accentTexture + ".png)");
                    continue;
                }

                Material cloudMat = MakeMaterial(style.name + "_Cloud", cloudTexture, shader, false, 1f);
                Material accentMat = MakeMaterial(style.name + "_Accent", accentTexture, shader,
                    style.luminous, style.luminous ? 2.3f : 1.0f);

                GameObject root = new GameObject("FT_" + style.name);
                FootTrailEffect effect = root.AddComponent<FootTrailEffect>();

                ParticleSystem cloud = CreateLayer(root.transform, "Soft_Trail", cloudMat, style,
                    false);
                ParticleSystem accents = CreateLayer(root.transform, "Accents", accentMat, style,
                    true);
                effect.Configure(cloud, style.cloudCount, accents, style.accentCount);

                string path = Generated + "/Prefabs/FT_" + style.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                UnityEngine.Object.DestroyImmediate(root);
                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Foot Trails", "Created " + built +
                " original prefabs in " + Generated + "/Prefabs", "OK");
            Debug.Log("Foot Trails: generated " + built + " presets. Attach one instance to each foot bone.");
        }

        private static Texture2D LoadTexture(string textureName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(
                Root + "/Textures/" + textureName + ".png");
        }

        private static Material MakeMaterial(string name, Texture2D texture, Shader shader,
            bool additive, float intensity)
        {
            string path = Generated + "/Materials/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
                mat.shader = shader;

            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_TintColor", Color.white);
            mat.SetFloat("_DstBlend", additive ? 1f : 10f);
            mat.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static ParticleSystem CreateLayer(Transform root, string name,
            Material mat, Style style, bool accent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(root, false);
            ParticleSystem ps = child.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 2f;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                style.lifetime * (accent ? .45f : .7f),
                style.lifetime * (accent ? 1.1f : 1.5f));
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                style.rise * (accent ? .4f : .12f),
                style.rise * (accent ? 1.45f : .62f));
            main.startSize = new ParticleSystem.MinMaxCurve(
                style.width * (accent ? .19f : .65f),
                style.width * (accent ? .65f : 1.4f));
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = (style.name.Contains("Leaves") || style.name.Contains("Petals"))
                ? 0.045f : (accent ? -0.005f : -0.02f);
            main.startColor = accent ? style.accent : style.primary;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = accent ? 42f : 55f;
            shape.radius = style.width * (accent ? .32f : .45f);
            shape.rotation = new Vector3(-90f, 0f, 0f); // aim cone upwards

            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new [] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new [] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(accent ? .95f : .65f, .14f),
                    new GradientAlphaKey(accent ? .65f : .45f, .52f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, accent ? .35f : .6f),
                new Keyframe(.25f, 1f),
                new Keyframe(1f, accent ? .05f : 1.5f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var velocity = ps.velocityOverLifetime;
            // Set all three axes to the same MinMaxCurve mode: TwoConstants.
            // Unity reports "Particle Velocity curves must all be in the same mode"
            // if Y stays at its default Constant mode while X/Z use TwoConstants.
            velocity.enabled = false;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.10f, 0.10f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.10f, 0.10f);
            velocity.enabled = true;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = accent ? 1f : 0f;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static void EnsureFolder(string fullPath)
        {
            string[] parts = fullPath.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string path = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(path))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = path;
            }
        }
    }
}
#endif
