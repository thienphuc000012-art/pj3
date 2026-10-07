using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class UltimateCinematicValidation
{
    const string Folder = "Library/CombatValidation/";
    static UltimateCinematicValidation() { EditorApplication.update += Poll; }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Poll()
    {
        if (!File.Exists(Folder + "UltimateCinematic.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "UltimateCinematic.request");
        GameObject root = null;
        UltimateCinematic cinematic = null;
        try
        {
            root = new GameObject("Cinematic validation (temporary)");
            root.hideFlags = HideFlags.DontSave;
            cinematic = root.AddComponent<UltimateCinematic>();
            BattleUnit MakeUnit(string name)
            {
                var go = new GameObject(name, typeof(MeshRenderer), typeof(BattleUnit));
                go.transform.SetParent(root.transform);
                return go.GetComponent<BattleUnit>();
            }
            var caster = MakeUnit("Caster"); var target = MakeUnit("Target");
            var ally = MakeUnit("Ally"); var otherEnemy = MakeUnit("Other enemy");
            var alreadyHidden = MakeUnit("Already hidden");
            alreadyHidden.GetComponent<Renderer>().forceRenderingOff = true;
            Canvas MakeCanvas(string name, bool enabled)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
                go.transform.SetParent(root.transform);
                var canvas = go.GetComponent<Canvas>(); canvas.enabled = enabled; return canvas;
            }
            var hud = MakeCanvas("HUD", true); var disabledHud = MakeCanvas("Disabled HUD", false);
            var slash = MakeCanvas("Slash", true);
            var slashGraphic = new GameObject("Slash graphic", typeof(RectTransform), typeof(CanvasRenderer), typeof(ScreenSlashGraphic));
            slashGraphic.transform.SetParent(slash.transform, false);
            slash.sortingOrder = 30000;
            var units = new[] { caster, target, ally, otherEnemy, alreadyHidden };
            int ticket = cinematic.Begin(caster, target, units);
            Require(!hud.enabled && !disabledHud.enabled && slash.enabled, "HUD/slash visibility");
            Require(!caster.GetComponent<Renderer>().forceRenderingOff && !target.GetComponent<Renderer>().forceRenderingOff, "Caster/target hidden");
            Require(ally.GetComponent<Renderer>().forceRenderingOff && otherEnemy.GetComponent<Renderer>().forceRenderingOff, "Other units still visible");
            Require(units.All(u => u.gameObject.activeInHierarchy && u.enabled), "Gameplay objects deactivated");
            var box = root.GetComponentsInChildren<Canvas>().Single(c => c.name == "Ultimate Cinematic Letterbox");
            Require(box.renderMode == RenderMode.ScreenSpaceOverlay && box.sortingOrder > slash.sortingOrder, "Letterbox must cover slash and camera effects");
            var bars = box.GetComponentsInChildren<Image>().Where(i => i.color == Color.black).ToArray();
            Require(bars.Length == 2 && bars.All(i => i.rectTransform.anchorMin.x == 0 && i.rectTransform.anchorMax.x == 1 && i.rectTransform.offsetMin == Vector2.zero && i.rectTransform.offsetMax == Vector2.zero), "Full-width opaque bars");
            var lateHud = MakeCanvas("Damage UI created during action", true);
            Canvas.ForceUpdateCanvases();
            Require(!lateHud.enabled, "New UI escaped suppression");
            cinematic.End(ticket);
            Require(hud.enabled && !disabledHud.enabled && lateHud.enabled && slash.enabled, "Canvas restoration");
            Require(!ally.GetComponent<Renderer>().forceRenderingOff && alreadyHidden.GetComponent<Renderer>().forceRenderingOff && !box.gameObject.activeSelf, "Renderer/letterbox restoration");
            int next = cinematic.Begin(caster, target, units);
            cinematic.End(ticket);
            Require(!hud.enabled, "Old completion cancelled new cinematic");
            typeof(UltimateCinematic).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(cinematic, null);
            Require(hud.enabled && !ally.GetComponent<Renderer>().forceRenderingOff, "Disable cleanup");
            File.WriteAllText(Folder + "UltimateCinematic.result.txt", "PASS: HUD and late-created UI hidden; slash retained; caster/target retained; other units visually hidden without disabling gameplay; opaque responsive bars above slash; original states restored on completion/disable; stale ticket ignored. Editor component test, not full combat Play Mode.");
        }
        catch (Exception e) { File.WriteAllText(Folder + "UltimateCinematic.result.txt", e.ToString()); }
        finally
        {
            if (cinematic != null) cinematic.End();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
