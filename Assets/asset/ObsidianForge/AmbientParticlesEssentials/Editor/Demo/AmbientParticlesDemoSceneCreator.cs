using System.IO;
using ObsidianForge.AmbientParticlesEssentials.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ObsidianForge.AmbientParticlesEssentials.Editor.Demo
{
    internal static class AmbientParticlesDemoSceneCreator
    {
        private const string RootPath = "Assets/ObsidianForge/AmbientParticlesEssentials";
        private const string DustProfilePath = RootPath + "/Profiles/Dust_Default.asset";
        private const string PollenProfilePath = RootPath + "/Profiles/Pollen_Default.asset";
        private const string AshProfilePath = RootPath + "/Profiles/Ash_Default.asset";
        private const string EnvironmentMaterialPath = RootPath + "/Materials/AmbientDemoEnvironment.mat";
        private const string PrefabPath = RootPath + "/Prefabs/AmbientBiomeParticles.prefab";
        private const string ScenePath = RootPath + "/Demo/AmbientParticlesDemo.unity";

        [MenuItem("Tools/Obsidian Forge/Ambient Particles Essentials/Create Ambient Particles Demo")]
        private static void CreateAmbientParticlesDemo()
        {
            EnsureFolders();
            var dustProfile = GetOrCreateProfile(DustProfilePath, profile => profile.ConfigureFloatingDustDefaults());
            var pollenProfile = GetOrCreateProfile(PollenProfilePath, profile => profile.ConfigurePollenAndSporesDefaults());
            var ashProfile = GetOrCreateProfile(AshProfilePath, profile => profile.ConfigureAmbientAshDefaults());
            AssignLayerMaterials(dustProfile, "Dust", new[] { 0, 2, 5, 1 });
            AssignLayerMaterials(pollenProfile, "Pollen", new[] { 1, 2, 3, 2 });
            AssignLayerMaterials(ashProfile, "Ash", new[] { 0, 4, 3, 4 });
            var environmentMaterial = GetOrCreateEnvironmentMaterial();
            CreateOrUpdatePrefab(pollenProfile);
            CreateScene(dustProfile, pollenProfile, ashProfile, environmentMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        private static void CreateScene(
            AmbientParticleProfile dustProfile,
            AmbientParticleProfile pollenProfile,
            AmbientParticleProfile ashProfile,
            Material environmentMaterial)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraRoot = new GameObject("Demo Camera Rig");
            var cameraObject = new GameObject("Demo Camera");
            cameraObject.transform.SetParent(cameraRoot.transform, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2.2f, -17f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.04f, 1f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 110f;
            camera.fieldOfView = 62f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.02f, 0.035f);
            cameraObject.AddComponent<DemoOrbitCamera>();

            var lightingRoot = new GameObject("Lighting");
            var lightObject = new GameObject("Soft Directional Light");
            lightObject.transform.SetParent(lightingRoot.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 0.35f;
            directionalLight.color = new Color(0.55f, 0.66f, 0.9f);

            var fillLightObject = new GameObject("Warm Dust Accent");
            fillLightObject.transform.SetParent(lightingRoot.transform, false);
            fillLightObject.transform.position = new Vector3(0f, 4f, -5f);
            var fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 18f;
            fillLight.intensity = 1.1f;
            fillLight.color = new Color(1f, 0.58f, 0.25f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.12f);

            CreateMetricGrid();

            var demoRoot = new GameObject("Ambient Particles Demo");
            var ambientParticles = demoRoot.AddComponent<AmbientBiomeParticles>();
            ambientParticles.FollowTarget = cameraObject.transform;
            ambientParticles.ParticleMaterial = null;
            ambientParticles.SetProfile(pollenProfile);
            ambientParticles.WindTrailMaterial = GetOrCreateTrailMaterial("Wind",3);
            var demo = demoRoot.AddComponent<AmbientParticlesDemo>();
            demo.SetProfiles(new[] { pollenProfile, dustProfile, ashProfile });
            EditorUtility.SetDirty(demo);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateMetricGrid()
        {
            var root = new GameObject("Metric Grid");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            root.AddComponent<DemoEndlessGround>();

            const string gridMaterialPath = RootPath + "/Materials/AmbientDemoGrid.mat";
            var gridMaterial = AssetDatabase.LoadAssetAtPath<Material>(gridMaterialPath);
            if (gridMaterial == null)
            {
                gridMaterial = new Material(Shader.Find("Obsidian Forge/Demo Metric Grid"));
                AssetDatabase.CreateAsset(gridMaterial,gridMaterialPath);
            }
            gridMaterial.shader = Shader.Find("Obsidian Forge/Demo Metric Grid");
            gridMaterial.SetColor("_BaseColor",new Color(.42f,.5f,.6f,1f));
            EditorUtility.SetDirty(gridMaterial);
            floor.GetComponent<Renderer>().sharedMaterial = gridMaterial;
        }

        private static void CreateOrUpdatePrefab(AmbientParticleProfile profile)
        {
            var temporary = new GameObject("AmbientBiomeParticles");
            var ambientParticles = temporary.AddComponent<AmbientBiomeParticles>();
            ambientParticles.ParticleMaterial = null;
            ambientParticles.SetProfile(profile);
            ambientParticles.WindTrailMaterial = GetOrCreateTrailMaterial("Wind",3);
            PrefabUtility.SaveAsPrefabAsset(temporary, PrefabPath);
            Object.DestroyImmediate(temporary);
        }

        private static AmbientParticleProfile GetOrCreateProfile(string path, System.Action<AmbientParticleProfile> configure)
        {
            var profile = AssetDatabase.LoadAssetAtPath<AmbientParticleProfile>(path);
            if (profile != null)
            {
                configure(profile);
                EditorUtility.SetDirty(profile);
                return profile;
            }

            profile = ScriptableObject.CreateInstance<AmbientParticleProfile>();
            configure(profile);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static void AssignLayerMaterials(AmbientParticleProfile profile, string prefix, int[] forms)
        {
            var shader = Shader.Find("Obsidian Forge/Ambient Particle");
            if (shader == null) throw new System.InvalidOperationException("Ambient Particle shader has not imported yet.");
            for (var i = 0; i < profile.LayerCount; i++)
            {
                var path = RootPath + "/Materials/" + prefix + "_Layer" + (i+1) + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
                material.shader = shader;
                material.SetFloat("_Form",forms[i]);
                material.SetFloat("_Brightness",1f);
                profile.GetLayer(i).material = material;
                profile.GetLayer(i).trailMaterial = null;
                EditorUtility.SetDirty(material);
            }
            EditorUtility.SetDirty(profile);
        }

        private static Material GetOrCreateTrailMaterial(string prefix, int style)
        {
            var path = RootPath+"/Materials/"+prefix+"_Trail.mat";
            var shader = Shader.Find("Obsidian Forge/Ambient Trail");
            if (shader == null) throw new System.InvalidOperationException("Ambient Trail shader has not imported yet.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader = shader; material.SetFloat("_Style",style); material.SetFloat("_Brightness",1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateEnvironmentMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Obsidian Forge/Demo Metric Grid");
                material = new Material(shader) { name = "Ambient Demo Environment" };
                AssetDatabase.CreateAsset(material, EnvironmentMaterialPath);
            }

            var color = new Color(0.05f, 0.075f, 0.12f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", new Color(0.006f, 0.012f, 0.025f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(RootPath + "/Demo");
            EnsureFolder(RootPath + "/Profiles");
            EnsureFolder(RootPath + "/Materials");
            EnsureFolder(RootPath + "/Textures");
            EnsureFolder(RootPath + "/Prefabs");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var child = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
