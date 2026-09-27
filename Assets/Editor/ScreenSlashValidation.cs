using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class ScreenSlashValidation
{
    static ScreenSlashValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string folder = "Library/CombatValidation/";
        if (!File.Exists(folder + "ScreenSlash.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(folder + "ScreenSlash.request");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            ValidateSpaceCutShader();
            var action = AssetDatabase.LoadAssetAtPath<ActionData>("Assets/Scripts/combat/ActionData/AttackSlashCombo.asset");
            if (!action.screenSlash) throw new Exception("Combo slash disabled");
            var root = new GameObject("Slash validation"); SceneManager.MoveGameObjectToScene(root, scene);
            var effect = root.AddComponent<ScreenSlashVfx>();
            ValidateHoldAndTime(root);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var populate = typeof(ScreenSlashGraphic).GetMethod("OnPopulateMesh", flags);
            foreach (int hit in new[] { 0, 1 })
            {
                effect.Play(action, hit);
                var graphic = root.GetComponentInChildren<ScreenSlashGraphic>();
                if (graphic.raycastTarget || root.GetComponentsInChildren<Canvas>().Length != 1) throw new Exception("Overlay blocks input or duplicates Canvas");
                float angle = (float)typeof(ScreenSlashGraphic).GetField("angle", flags).GetValue(graphic);
                if (Mathf.Abs(angle - (hit == 0 ? -24 : 24)) > .01f) throw new Exception("Hit direction incorrect");
                foreach (var size in new[] { new Vector2(1920,1080), new Vector2(2560,1080), new Vector2(1080,1920) })
                {
                    graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = Vector2.zero;
                    graphic.rectTransform.sizeDelta = size;
                    typeof(ScreenSlashGraphic).GetField("age", flags).SetValue(graphic, .08f);
                    using (var vh = new VertexHelper())
                    {
                        populate.Invoke(graphic, new object[] { vh });
                        if (vh.currentVertCount == 0) throw new Exception("Missing slash mesh");
                        var vertex = new UIVertex();
                        for (int i = 0; i < vh.currentVertCount; i++)
                        {
                            vh.PopulateUIVertex(ref vertex, i);
                            if (float.IsNaN(vertex.position.x) || float.IsInfinity(vertex.position.y)) throw new Exception("Invalid slash vertex");
                        }
                    }
                }
                if (action.screenSlashCount > 1)
                {
                    int early;
                    using (var vh = new VertexHelper())
                    {
                        typeof(ScreenSlashGraphic).GetField("age", flags).SetValue(graphic, .01f);
                        populate.Invoke(graphic, new object[] { vh }); early = vh.currentVertCount;
                    }
                    using (var vh = new VertexHelper())
                    {
                        typeof(ScreenSlashGraphic).GetField("age", flags).SetValue(graphic, action.screenSlashBurstTime * .7f);
                        populate.Invoke(graphic, new object[] { vh });
                        if (vh.currentVertCount <= early || vh.currentVertCount > 24 * 84) throw new Exception("Burst did not build up or exceeded mesh budget");
                    }
                    using (var vh = new VertexHelper())
                    {
                        typeof(ScreenSlashGraphic).GetField("age", flags).SetValue(graphic, action.screenSlashBurstTime + .05f);
                        populate.Invoke(graphic, new object[] { vh });
                        if (vh.currentVertCount == 0) throw new Exception("Finisher is missing");
                    }
                }
                if (action.screenSplitHold)
                {
                    using (var vh = new VertexHelper())
                    {
                        typeof(ScreenSlashGraphic).GetField("age", flags).SetValue(graphic, action.screenSlashDuration * .79f);
                        populate.Invoke(graphic, new object[] { vh });
                        if (vh.currentVertCount == 0) throw new Exception("Held cuts disappeared before closing phase");
                    }
                }
                typeof(ScreenSlashGraphic).GetField("age", flags).SetValue(graphic, action.screenSlashDuration + 1);
                typeof(ScreenSlashGraphic).GetMethod("Update", flags).Invoke(graphic, null);
                using (var vh = new VertexHelper())
                {
                    populate.Invoke(graphic, new object[] { vh });
                    if (vh.currentVertCount != 0) throw new Exception("Slash did not clear");
                }
            }
            File.WriteAllText(folder + "ScreenSlash.result.txt", "PASS: GPU split/restoration; early burst and held cuts; final closure envelope; time restoration on expiry/disable/external pause; both directions; bounded mesh; aspect ratios; clears on expiry.");
        }
        catch (Exception e) { File.WriteAllText(folder + "ScreenSlash.result.txt", e.ToString()); }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void ValidateHoldAndTime(GameObject root)
    {
        if (ScreenSpaceCut.Closure(1.28f, 1.6f, .8f) != 1f ||
            Mathf.Abs(ScreenSpaceCut.Closure(1.44f, 1.6f, .8f) - .5f) > .001f ||
            ScreenSpaceCut.Closure(1.6f, 1.6f, .8f) != 0f)
            throw new Exception("Incorrect hold/rejoin timing");
        var cut = root.AddComponent<ScreenSpaceCut>();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        float savedScale = Time.timeScale, savedFixed = Time.fixedDeltaTime;
        try
        {
            // Exercise the same ownership/release path without entering gameplay.
            for (int mode = 0; mode < 3; mode++)
            {
                typeof(ScreenSpaceCut).GetField("previousScale", flags).SetValue(cut, .8f);
                typeof(ScreenSpaceCut).GetField("previousFixed", flags).SetValue(cut, .02f);
                typeof(ScreenSpaceCut).GetField("ownsTime", flags).SetValue(cut, true);
                typeof(ScreenSpaceCut).GetMethod("ApplyTime", flags).Invoke(cut, new object[] { .22f });
                if (Mathf.Abs(Time.timeScale - .176f) > .001f) throw new Exception("Slow motion not applied");
                if (mode == 0)
                {
                    typeof(ScreenSpaceCut).GetField("playing", flags).SetValue(cut, true);
                    typeof(ScreenSpaceCut).GetField("duration", flags).SetValue(cut, 1f);
                    typeof(ScreenSpaceCut).GetField("beginTime", flags).SetValue(cut, Time.unscaledTime - 2f);
                    typeof(ScreenSpaceCut).GetMethod("Update", flags).Invoke(cut, null);
                }
                else if (mode == 1) cut.enabled = false;
                else { Time.timeScale = 0; cut.Stop(); }
                if (Mathf.Abs(Time.timeScale - (mode == 2 ? 0 : .8f)) > .001f ||
                    Mathf.Abs(Time.fixedDeltaTime - .02f) > .00001f)
                    throw new Exception("Time ownership was not released correctly");
            }
        }
        finally { cut.Stop(); Time.timeScale = savedScale; Time.fixedDeltaTime = savedFixed; UnityEngine.Object.DestroyImmediate(cut); }
    }

    static void ValidateSpaceCutShader()
    {
        var shader = Resources.Load<Shader>("CombatSpaceCut");
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new Exception("Space cut shader failed to compile");
        var material = new Material(shader);
        var pattern = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        var source = new RenderTexture(256,256,0);
        var destination = new RenderTexture(256,256,0);
        RTHandle a = null, b = null;
        var read = new Texture2D(256,256,TextureFormat.RGBA32,false);
        var previous = RenderTexture.active;
        try
        {
            for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
                pattern.SetPixel(x,y, (x/16+y/16)%2 == 0 ? new Color(.15f,.3f,.45f) : new Color(.85f,.7f,.5f));
            pattern.Apply(); source.Create(); destination.Create();
            Graphics.Blit(pattern,source);
            a = RTHandles.Alloc(source); b = RTHandles.Alloc(destination);
            material.SetVector("_CutViewport",new Vector4(256,256,1f/256,1f/256));
            material.SetColor("_EdgeTint",new Color(.72f,.86f,.94f));
            var lines = new Vector4[24]; lines[0] = new Vector4(.4f,.916515f,0,18);
            material.SetVectorArray("_Cuts", lines);
            Color32[] baseline = null;
            for (int step = 0; step < 3; step++)
            {
                material.SetInt("_CutCount",step == 1 ? 1 : 0);
                var cmd = new CommandBuffer();
                try { Blitter.BlitCameraTexture(cmd,a,b,material,0); Graphics.ExecuteCommandBuffer(cmd); }
                finally { cmd.Release(); }
                RenderTexture.active = destination;
                read.ReadPixels(new Rect(0,0,256,256),0,0); read.Apply();
                var pixels = read.GetPixels32();
                if (step == 0) baseline = pixels;
                else
                {
                    int changed = 0;
                    for (int i = 0; i < pixels.Length; i++)
                        if (Mathf.Abs(pixels[i].r-baseline[i].r) > 3 || Mathf.Abs(pixels[i].b-baseline[i].b) > 3) changed++;
                    if (step == 1 && changed < 1000) throw new Exception("Space cut did not displace camera pixels");
                    if (step == 2 && changed != 0) throw new Exception("Space cut did not fully restore image");
                }
                File.WriteAllBytes("Library/CombatValidation/SpaceCut-" + step + ".png",read.EncodeToPNG());
            }
            if (ShaderUtil.ShaderHasError(shader)) throw new Exception("Space cut shader GPU compile error");
        }
        finally
        {
            RenderTexture.active = previous;
            a?.Release(); b?.Release(); source.Release(); destination.Release();
            UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(destination);
            UnityEngine.Object.DestroyImmediate(pattern); UnityEngine.Object.DestroyImmediate(read); UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
