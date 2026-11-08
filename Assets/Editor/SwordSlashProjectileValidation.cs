using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit checks on isolated clones. Never loads or saves a live scene or changes an ActionData asset.</summary>
[InitializeOnLoad]
public static class SwordSlashProjectileValidation
{
    const string RequestPath = "Library/CombatValidation/SwordSlashProjectileValidation.request";
    const string ResultPath = "Library/CombatValidation/SwordSlashProjectileValidation.result.txt";
    const string PrefabFolder = "Assets/asset/Free Slash VFX/Prefabs/";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly string[] Elements = { "Water", "Fire", "Eletric", "Earth" };
    static readonly Vector3 Release = new Vector3(1.2f, 2.4f, -3.6f);
    static readonly Vector3 Direction = new Vector3(2f, .3f, 5f).normalized;
    static readonly Vector3 BladeAxis = new Vector3(.2f, .9f, -.3f).normalized;
    static readonly Vector3 TargetOffset = new Vector3(.3f, 1.2f, -.4f);
    static readonly Vector3 Destination = Release + Direction * 8f;

    static SwordSlashProjectileValidation() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        Run();
    }

    [MenuItem("Tools/Adventure/Combat/Validate Sword Slash Projectiles")]
    public static void Run() => Validate(false);

    public static void RunBatch() => Validate(true);

    static void Validate(bool rethrow)
    {
        Scene preview = default;
        var results = new List<string>();
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run projectile validation in Edit Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            preview = EditorSceneManager.NewPreviewScene();
            var staging = new GameObject("Isolated Slash Projectile Staging");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, preview);
            var target = Actor("Isolated Slash Target", Destination - TargetOffset, preview);
            ValidateRotations();
            foreach (string element in Elements)
            {
                var prefab = Load("Projectiles/Slash Projectile VFX " + element + ".prefab");
                ValidateFlight(prefab, staging.transform, target, preview);
                results.Add("PASS: " + prefab.name + ". Constant-speed flight along local +Z, blade roll on +X, demo movement disabled, particles explicitly started, one arrival callback after travel, emission stopped and live trails retained.");
            }
            var fire = Load("Projectiles/Slash Projectile VFX Fire.prefab");
            ValidateFallbackAndCancellation(fire, staging.transform, target, preview);
            ValidateManager(fire, target, preview);
            results.Add("PASS: missing-target fallback, one cancellation on repeated disable, and no post-cancel impact.");
            results.Add("PASS: actual CombatManager spawn and ActionData classification. Standalone VfxType.None projectile delegates damage to arrival, main Shoot/Beam retains main-impact ownership, ordinary melee Slash VFX stays stationary.");
            results.Add("Isolated Edit Mode checks with manual Tick/particle simulation. No live scenes, prefab assets, ActionData assets, combat singletons or saves changed.");
            File.WriteAllLines(ResultPath, results);
            Debug.Log(string.Join("\n", results));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            results.Add("FAIL: " + exception);
            File.WriteAllLines(ResultPath, results);
            Debug.LogException(exception);
            if (rethrow) throw;
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static void ValidateRotations()
    {
        foreach (var direction in new[] { Direction, Vector3.up, Vector3.down, Vector3.right })
        {
            var rotation = CombatSwordSlashProjectile.FlightRotation(direction, BladeAxis, Vector3.zero);
            Check(Vector3.Dot(rotation * Vector3.forward, direction) > .9999f, "Flight rotation does not keep local +Z on its flight path.");
            var projectedBlade = Vector3.ProjectOnPlane(BladeAxis, direction).normalized;
            Check(Vector3.Dot(rotation * Vector3.right, projectedBlade) > .9999f, "Local +X lost the release sword's roll.");
            var corrected = CombatSwordSlashProjectile.FlightRotation(direction, BladeAxis, new Vector3(0f, 0f, 23f));
            Check(Quaternion.Angle(corrected, rotation * Quaternion.Euler(0f, 0f, 23f)) < .01f,
                "ActionData local rotation is not applied after the flight frame.");
        }
        var parallel = CombatSwordSlashProjectile.FlightRotation(Vector3.up, Vector3.up, Vector3.zero);
        Check(Vector3.Dot(parallel * Vector3.forward, Vector3.up) > .9999f, "Parallel blade/path axes produced an invalid flight rotation.");
    }

    static void ValidateFlight(GameObject prefab, Transform staging, BattleUnit target, Scene preview)
    {
        Check(CombatSwordSlashProjectile.IsPackageProjectile(prefab), "Package projectile was not detected: " + prefab.name);
        target.SetPersistentHP(100);
        var effect = Clone(prefab, staging, preview);
        try
        {
            int impacts = 0;
            int cancellations = 0;
            var flight = effect.AddComponent<CombatSwordSlashProjectile>();
            flight.Cancelled += () => cancellations++;
            flight.Initialize(target, Destination, TargetOffset, 4f, 1.5f, BladeAxis, Vector3.zero,
                () => { impacts++; target.TakeDamage(11, false); });
            Check(effect.GetComponentsInChildren<MaykerStudio.Demo.Projectile>(true).All(demo => !demo.enabled),
                "The demo Projectile can still move the combat projectile.");
            var root = effect.GetComponent<ParticleSystem>();
            Check(root != null && root.isPlaying, "Root particles were not explicitly started.");
            Check(effect.GetComponentsInChildren<ParticleSystem>(true).All(particles => particles.main.stopAction == ParticleSystemStopAction.None),
                "A particle Stop Action can destroy still-alive child trails.");
            var airborne = effect.GetComponentsInChildren<ParticleSystem>(true)
                .Where(particles => particles.GetComponentInParent<MaykerStudio.VFX.SlashDecal>(true) == null).ToArray();
            foreach (var particles in airborne)
            {
                particles.Simulate(.2f, false, false, false);
                particles.Play(false);
            }
            Check(airborne.Sum(particles => particles.particleCount) > 0,
                "The started projectile emitted no particles.");
            Check(impacts == 0 && target.currentHP == 100 && !flight.HasArrived, "Damage resolved before the projectile travelled.");
            for (int frame = 1; frame <= 20; frame++)
            {
                flight.Tick(.1f);
                var expected = Vector3.MoveTowards(Release, Destination, frame * .4f);
                Check(Vector3.Distance(effect.transform.position, expected) < .002f, "Projectile stalled or moved twice at frame " + frame + ": " + prefab.name);
                Check(Vector3.Dot(effect.transform.forward, Direction) > .999f, "Projectile lost its local +Z flight orientation.");
                if (frame < 20) Check(impacts == 0 && target.currentHP == 100, "Impact damage occurred before reaching the target.");
            }
            Check(flight.HasArrived && impacts == 1 && target.currentHP == 89, "Arrival did not resolve exactly one damage callback.");
            Check(effect != null && effect.activeSelf && root.IsAlive(true), "Arrival removed the projectile before its existing trails could finish.");
            AssertStopped(effect);
            flight.Tick(.2f);
            flight.Tick(50f);
            Check(impacts == 1 && cancellations == 0 && target.currentHP == 89, "A completed projectile applied a duplicate impact or cancellation.");
            Check(Vector3.Distance(effect.transform.position, Destination) < .002f, "Projectile drifted after arrival.");
        }
        finally { UnityEngine.Object.DestroyImmediate(effect); }
    }

    static void ValidateFallbackAndCancellation(GameObject prefab, Transform staging, BattleUnit target, Scene preview)
    {
        var effect = Clone(prefab, staging, preview);
        try
        {
            int impacts = 0;
            var flight = effect.AddComponent<CombatSwordSlashProjectile>();
            flight.Initialize(null, Destination, TargetOffset, 4f, 1.5f, BladeAxis, Vector3.zero, () => impacts++);
            flight.Tick(2f);
            Check(flight.HasArrived && impacts == 1 && Vector3.Distance(effect.transform.position, Destination) < .002f,
                "A missing target did not use its fallback destination.");
        }
        finally { UnityEngine.Object.DestroyImmediate(effect); }
        effect = Clone(prefab, staging, preview);
        try
        {
            int impacts = 0;
            int cancellations = 0;
            var flight = effect.AddComponent<CombatSwordSlashProjectile>();
            flight.Cancelled += () => cancellations++;
            flight.Initialize(target, Destination, TargetOffset, 4f, 1.5f, BladeAxis, Vector3.zero, () => impacts++);
            flight.Tick(.1f);
            // Ordinary MonoBehaviour lifecycle is not dispatched in an Edit Mode preview.
            var disable = typeof(CombatSwordSlashProjectile).GetMethod("OnDisable", PrivateInstance);
            Check(disable != null, "Projectile disable cleanup is missing.");
            disable.Invoke(flight, null);
            disable.Invoke(flight, null);
            var stoppedPosition = effect.transform.position;
            flight.Tick(50f);
            Check(cancellations == 1 && impacts == 0 && !flight.HasArrived && effect.transform.position == stoppedPosition,
                "A cancelled flight moved, damaged its target or notified cancellation twice.");
            AssertStopped(effect);
        }
        finally { UnityEngine.Object.DestroyImmediate(effect); }
    }

    static void ValidateManager(GameObject projectilePrefab, BattleUnit target, Scene preview)
    {
        var legacy = Load("Slash VFX.prefab");
        Check(!CombatSwordSlashProjectile.IsPackageProjectile(legacy), "A stationary Slash VFX was misidentified as a package projectile.");
        var host = new GameObject("Isolated Slash CombatManager");
        host.SetActive(false);
        SceneManager.MoveGameObjectToScene(host, preview);
        var manager = host.AddComponent<CombatManager>();
        manager.enabled = false;
        var attacker = Actor("Isolated Slash Attacker", Release, preview);
        attacker.isPlayer = true;
        var action = ScriptableObject.CreateInstance<ActionData>();
        var tracked = (HashSet<GameObject>)typeof(CombatManager).GetField("combatVfxInstances", PrivateInstance).GetValue(manager);
        var pending = (List<GameObject>)typeof(CombatManager).GetField("pendingVfxImpacts", PrivateInstance).GetValue(manager);
        try
        {
            action.swordSlashVfxPrefab = projectilePrefab;
            action.swordSlashTravelsToTarget = true;
            action.swordSlashTravelSpeed = 4f;
            action.swordSlashVfxLifetime = 1.5f;
            action.swordSlashVfxScale = Vector3.one;
            action.projectileTargetOffset = TargetOffset;
            action.vfxType = ActionData.VfxType.None;
            Check(Classify("HasTravelingSwordSlash", action) && Classify("UsesSwordSlashImpactDamage", action) && Classify("UsesVfxImpactDamage", action),
                "VfxType.None package projectile was classified as stationary or immediate damage.");
            Spawn(manager, attacker, target, action);
            Check(tracked.Count == 1 && pending.Count == 1, "Standalone projectile did not register exactly one pending arrival.");
            var spawned = tracked.Single();
            Check(spawned.scene == preview && spawned.activeInHierarchy, "Combat spawn escaped the isolated preview scene or stayed inactive.");
            var flight = spawned.GetComponent<CombatSwordSlashProjectile>();
            Check(flight != null && spawned.GetComponent<ParticleSystem>().isPlaying, "Combat spawn omitted projectile movement/particle initialization.");
            flight.Tick(.1f);
            Check(Vector3.Distance(spawned.transform.position, Release + Direction * .4f) < .002f,
                "The CombatManager-spawned standalone slash did not move toward its target.");
            ClearClones(tracked, pending);

            foreach (var mainType in new[] { ActionData.VfxType.Shoot, ActionData.VfxType.Beam })
            {
                action.vfxType = mainType;
                action.vfxPrefab = legacy;
                Check(Classify("HasTravelingSwordSlash", action) && Classify("UsesVfxImpactDamage", action) && !Classify("UsesSwordSlashImpactDamage", action),
                    "An optional slash stole damage ownership from the main Shoot/Beam VFX.");
            }
            Spawn(manager, attacker, target, action);
            Check(tracked.Count == 1 && pending.Count == 0 && tracked.Single().GetComponent<CombatSwordSlashProjectile>() != null,
                "An optional moving slash added a duplicate damage callback.");
            ClearClones(tracked, pending);

            action.vfxType = ActionData.VfxType.None;
            action.vfxPrefab = null;
            action.swordSlashVfxPrefab = legacy;
            Check(!Classify("HasTravelingSwordSlash", action) && !Classify("UsesVfxImpactDamage", action), "Normal melee Slash VFX was changed into impact damage.");
            Spawn(manager, attacker, target, action);
            Check(tracked.Count == 1 && pending.Count == 0 && tracked.Single().GetComponent<CombatSwordSlashProjectile>() == null,
                "A normal melee slash received projectile movement or deferred damage.");
            Check(Vector3.Distance(tracked.Single().transform.position, Release) < .002f, "Normal melee Slash VFX moved from its release point.");
        }
        finally
        {
            ClearClones(tracked, pending);
            UnityEngine.Object.DestroyImmediate(action);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    static void Spawn(CombatManager manager, BattleUnit attacker, BattleUnit target, ActionData action)
    {
        var method = typeof(CombatManager).GetMethod("SpawnSwordSlashVFX", PrivateInstance);
        Check(method != null, "CombatManager sword-slash spawn method is missing.");
        method.Invoke(manager, new object[] { attacker, target, action });
    }

    static bool Classify(string name, ActionData action)
    {
        var method = typeof(CombatManager).GetMethod(name, PrivateStatic);
        Check(method != null, "CombatManager classifier missing: " + name);
        return (bool)method.Invoke(null, new object[] { action });
    }

    static void ClearClones(HashSet<GameObject> tracked, List<GameObject> pending)
    {
        foreach (var clone in tracked.ToArray()) if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
        tracked.Clear();
        pending.Clear();
    }

    static GameObject Clone(GameObject prefab, Transform staging, Scene preview)
    {
        var clone = UnityEngine.Object.Instantiate(prefab, staging, false);
        foreach (var audio in clone.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var particles in clone.GetComponentsInChildren<ParticleSystem>(true))
        {
            var collision = particles.collision;
            collision.enabled = false;
            var trigger = particles.trigger;
            trigger.enabled = false;
        }
        clone.SetActive(false);
        clone.transform.SetParent(null, false);
        SceneManager.MoveGameObjectToScene(clone, preview);
        clone.transform.position = Release;
        clone.SetActive(true);
        return clone;
    }

    static BattleUnit Actor(string name, Vector3 position, Scene preview)
    {
        var actor = new GameObject(name);
        actor.SetActive(false);
        SceneManager.MoveGameObjectToScene(actor, preview);
        var unit = actor.AddComponent<BattleUnit>();
        unit.maxHP = 100;
        unit.baseDef = 0;
        unit.SetPersistentHP(100);
        actor.transform.position = position;
        actor.SetActive(true);
        return unit;
    }

    static GameObject Load(string relativePath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + relativePath);
        Check(prefab != null, "Slash prefab missing: " + relativePath);
        return prefab;
    }

    static void AssertStopped(GameObject effect)
    {
        Check(effect.GetComponentsInChildren<ParticleSystem>(true).All(particles => !particles.isEmitting), "An arrived/cancelled projectile is still emitting particles.");
        Check(effect.GetComponentsInChildren<MaykerStudio.VFX.SlashDecal>(true).All(decal => !decal.enabled), "SlashDecal can restart emission after arrival/cancellation.");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
