using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Explicit asset setup only. A menu command or request file must authorize each run.
[InitializeOnLoad]
public static class BattleEntryWalkSetup
{
    const string RequestPath = "Library/CombatValidation/BattleEntryWalkSetup.request";
    const string ReportPath = "Library/CombatValidation/BattleEntryWalkSetup.result.txt";
    const string StateName = "BattleEntryWalk";
    const string ControllerFolder = "Assets/Adventure/Prefabs/player/Mage/Anim/animcontroller/";
    const string ClipFolder = "Assets/animation/walk start/";

    static readonly Mapping[] Mappings =
    {
        new Mapping("p2.controller", "Sword And Shield Walk.fbx"),
        new Mapping("p2 wizar.controller", "Catwalk Walk Forward.fbx"),
        new Mapping("conbatp1.controller", "Catwalk Walk Forward 02.fbx")
    };

    static bool running;
    static double nextCheck;

    static BattleEntryWalkSetup() => EditorApplication.update += CheckRequest;

    static bool EditorBusy => EditorApplication.isCompiling || EditorApplication.isUpdating ||
        EditorApplication.isPlayingOrWillChangePlaymode;

    static void CheckRequest()
    {
        if (running || EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1f;
        if (EditorBusy || !File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        Setup();
    }

    [MenuItem("Tools/Adventure/Combat/Setup Battle Entry Walk")]
    public static void Setup()
    {
        if (running || EditorBusy)
        {
            Debug.LogWarning("Run Battle Entry Walk setup in Edit Mode after importing and compiling finish.");
            return;
        }

        running = true;
        string backupFolder = null;
        bool changesStarted = false;
        var report = new StringBuilder();
        try
        {
            foreach (var mapping in Mappings) Preflight(mapping);

            backupFolder = "Library/CombatValidation/BattleEntryWalkSetup-backup-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff");
            BackupAssets(backupFolder);
            report.AppendLine("Backup: " + Path.GetFullPath(backupFolder));
            changesStarted = true;

            foreach (var mapping in Mappings) ConfigureImport(mapping);
            foreach (var mapping in Mappings) ConfigureController(mapping, report);
            foreach (var mapping in Mappings) Validate(mapping, report);

            report.Insert(0, "PASS: battle entry walk clips and isolated Animator states configured.\n");
            report.AppendLine("Only the three walk import settings and mapped controller assets were saved.");
            report.AppendLine("Report: " + Path.GetFullPath(ReportPath));
            WriteReport(report);
            Debug.Log("Battle Entry Walk setup passed. Report: " + ReportPath);
        }
        catch (Exception exception)
        {
            report.Insert(0, "FAIL: " + exception + "\n");
            if (changesStarted)
            {
                try
                {
                    RestoreAssets(backupFolder);
                    report.AppendLine("Restored all target assets from the backup after setup failed.");
                }
                catch (Exception restoreException)
                {
                    report.AppendLine("Automatic restore failed: " + restoreException);
                    report.AppendLine("Backup retained at: " + Path.GetFullPath(backupFolder));
                }
            }
            WriteReport(report);
            Debug.LogException(exception);
        }
        finally
        {
            running = false;
        }
    }

    static void Preflight(Mapping mapping)
    {
        var controller = LoadController(mapping);
        var importer = LoadImporter(mapping);
        if (EditorUtility.IsDirty(controller) || EditorUtility.IsDirty(importer))
            throw new InvalidOperationException("Save or revert unsaved target asset edits before setup: " + mapping.ControllerPath);
        if (controller.layers.Length == 0 || controller.layers[0].name != "Base Layer")
            throw new InvalidOperationException("Expected an existing Base Layer in " + mapping.ControllerPath);
        var machine = controller.layers[0].stateMachine;
        if (machine == null || machine.defaultState == null)
            throw new InvalidOperationException("Expected an existing default state in " + mapping.ControllerPath);
        if (AssetDatabase.LoadAllAssetsAtPath(mapping.ControllerPath).Any(asset => asset != null && EditorUtility.IsDirty(asset)))
            throw new InvalidOperationException("The target Animator has unsaved state edits: " + mapping.ControllerPath);
        if (importer.animationType != ModelImporterAnimationType.Human || !importer.importAnimation)
            throw new InvalidOperationException("The source must already import Humanoid animation: " + mapping.ClipPath);
        var clips = importer.clipAnimations;
        if (clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips.Length != 1)
            throw new InvalidOperationException("Expected exactly one source clip in " + mapping.ClipPath);

        var states = machine.states.Where(child => child.state != null && child.state.name == StateName).ToArray();
        if (states.Length > 1)
            throw new InvalidOperationException("Duplicate " + StateName + " states in " + mapping.ControllerPath);
        if (states.Length == 1)
        {
            var state = states[0].state;
            if (state == machine.defaultState || state.transitions.Length != 0 || state.behaviours.Length != 0 ||
                !(state.motion is AnimationClip) || AssetDatabase.GetAssetPath(state.motion) != mapping.ClipPath)
                throw new InvalidOperationException("Existing " + StateName + " has a different configuration. Review it before setup: " + mapping.ControllerPath);
        }
        foreach (string path in mapping.BackupPaths)
            if (!File.Exists(path)) throw new FileNotFoundException("Required asset or metadata is missing.", path);
    }

    static void ConfigureImport(Mapping mapping)
    {
        var importer = LoadImporter(mapping);
        var clips = importer.clipAnimations;
        if (clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.loopTime = true;
            clip.loopPose = true;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clip.events = Array.Empty<AnimationEvent>();
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static void ConfigureController(Mapping mapping, StringBuilder report)
    {
        var controller = LoadController(mapping);
        var machine = controller.layers[0].stateMachine;
        var existing = machine.states.FirstOrDefault(child => child.state != null && child.state.name == StateName).state;
        if (existing != null)
        {
            report.AppendLine("Kept existing isolated state: " + mapping.ControllerPath + " / Base Layer." + StateName);
            return;
        }

        var defaultState = machine.defaultState;
        float x = machine.states.Max(child => child.position.x) + 260f;
        var state = machine.AddState(StateName, new Vector3(x, 120f, 0f));
        state.motion = LoadClip(mapping);
        state.speed = 1f;
        state.writeDefaultValues = true;
        if (machine.defaultState != defaultState)
            throw new InvalidOperationException("Adding the entry state changed the default state: " + mapping.ControllerPath);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        report.AppendLine("Created isolated state: " + mapping.ControllerPath + " / Base Layer." + StateName);
    }

    static void Validate(Mapping mapping, StringBuilder report)
    {
        var clip = LoadClip(mapping);
        var importer = LoadImporter(mapping);
        var settings = importer.clipAnimations.Single();
        if (!clip.isHumanMotion || clip.legacy || !clip.isLooping || clip.length <= 0f || clip.events.Length != 0 ||
            !settings.loopTime || !settings.loopPose || !settings.lockRootRotation || !settings.lockRootHeightY ||
            !settings.lockRootPositionXZ || !settings.keepOriginalOrientation || !settings.keepOriginalPositionY ||
            !settings.keepOriginalPositionXZ)
            throw new InvalidOperationException("Humanoid, loop, in-place, or event validation failed: " + mapping.ClipPath);

        var machine = LoadController(mapping).layers[0].stateMachine;
        var state = machine.states.Single(child => child.state != null && child.state.name == StateName).state;
        if (state.motion != clip || state == machine.defaultState || state.transitions.Length != 0 || state.behaviours.Length != 0)
            throw new InvalidOperationException("The entry state must be isolated, non-default and use the intended clip: " + mapping.ControllerPath);
        report.AppendLine("PASS: " + mapping.ClipPath + " -> " + mapping.ControllerPath + " / Base Layer." + StateName +
            " (Humanoid, looping, baked root rotation/Y/XZ, no animation events, no state transitions or behaviours).");
    }

    static AnimatorController LoadController(Mapping mapping)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(mapping.ControllerPath);
        if (controller == null) throw new FileNotFoundException("Animator Controller is missing.", mapping.ControllerPath);
        return controller;
    }

    static ModelImporter LoadImporter(Mapping mapping)
    {
        var importer = AssetImporter.GetAtPath(mapping.ClipPath) as ModelImporter;
        if (importer == null) throw new FileNotFoundException("Walk FBX importer is missing.", mapping.ClipPath);
        return importer;
    }

    static AnimationClip LoadClip(Mapping mapping)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(mapping.ClipPath).OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        if (clips.Length != 1) throw new InvalidOperationException("Expected one imported walk clip in " + mapping.ClipPath);
        return clips[0];
    }

    static void BackupAssets(string folder)
    {
        foreach (var mapping in Mappings)
            foreach (string path in mapping.BackupPaths)
            {
                string destination = Path.Combine(folder, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(path, destination, false);
            }
    }

    static void RestoreAssets(string folder)
    {
        foreach (var mapping in Mappings)
            foreach (string path in mapping.BackupPaths)
                File.Copy(Path.Combine(folder, path), path, true);
        foreach (var mapping in Mappings)
        {
            AssetDatabase.ImportAsset(mapping.ClipPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(mapping.ControllerPath, ImportAssetOptions.ForceUpdate);
        }
    }

    static void WriteReport(StringBuilder report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report.ToString());
    }

    sealed class Mapping
    {
        public readonly string ControllerPath;
        public readonly string ClipPath;
        public IEnumerable<string> BackupPaths => new[] { ControllerPath, ControllerPath + ".meta", ClipPath + ".meta" };

        public Mapping(string controller, string clip)
        {
            ControllerPath = ControllerFolder + controller;
            ClipPath = ClipFolder + clip;
        }
    }
}
