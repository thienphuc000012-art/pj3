using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated grounding regression for the actual Free Slash projectile prefabs.</summary>
[InitializeOnLoad]
public static class SwordSlashGroundValidation
{
    const string RequestPath = "Library/CombatValidation/SwordSlashGroundValidation.request";
    const string ResultPath = "Library/CombatValidation/SwordSlashGroundValidation.result.txt";
    const string Folder = "Assets/asset/Free Slash VFX/Prefabs/Projectiles/";
    const uint DebrisSeed = 958274u;
    static readonly string[] Elements = { "Earth", "Water", "Fire", "Eletric" };
    static readonly Vector3 Release = new Vector3(0f, 5f, 0f);
    static readonly Vector3 Destination = new Vector3(0f, 5f, 12f);
    static readonly Vector3 BladeAxis = new Vector3(.4f, .8f, .4f).normalized;

    static SwordSlashGroundValidation() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(RequestPath); }
        catch (IOException) { return; } // The request writer may still hold the marker open.
        Run();
    }

    [MenuItem("Tools/Adventure/Combat/Validate Sword Slash Ground Effects")]
    public static void Run() => Validate(false);

    public static void RunBatch() => Validate(true);

    static void Validate(bool rethrow)
    {
        Scene preview = default;
        var results = new List<string>();
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run slash grounding checks in Edit Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            preview = EditorSceneManager.NewPreviewScene();
            var staging = ObjectInScene("Isolated Ground Slash Staging", preview);
            staging.SetActive(false);
            var floor = ObjectInScene("Isolated Surface Layer 8 Floor", preview).AddComponent<BoxCollider>();
            floor.gameObject.layer = 8;
            floor.transform.position = new Vector3(0f, -.5f, 0f);
            floor.size = new Vector3(40f, 1f, 40f);
            AddExcludedColliders(preview);
            Physics.SyncTransforms();

            foreach (string element in Elements)
            {
                var prefab = Load(element);
                floor.enabled = true;
                Physics.SyncTransforms();
                ValidateOverFloor(prefab, staging.transform, floor, preview, element == "Earth");
                floor.enabled = false;
                Physics.SyncTransforms();
                ValidateWithoutFloor(prefab, staging.transform, preview);
                results.Add("PASS: " + prefab.name + ". At 5m flight height, layer-8 ground emitters stay on the surface despite movement/roll, ignore actors and other projectiles, stop emission when ground disappears, and never emit at airborne height when no floor exists.");
            }

            floor.enabled = true;
            Physics.SyncTransforms();
            ValidateHeadings(Load("Earth"), staging.transform, floor, preview);
            results.Add("PASS: Earth decals follow true +X/diagonal travel despite visual Euler correction, emit flat world-oriented stamps immediately, overlap by distance, and retain earlier stamp orientation while turning.");
            floor.transform.rotation = Quaternion.Euler(0f, 0f, 18f);
            Physics.SyncTransforms();
            var slopeEffect = Clone(Load("Earth"), staging.transform, preview);
            try
            {
                var flight = slopeEffect.AddComponent<CombatSwordSlashProjectile>();
                flight.ConfigureGrounding(1 << 8, 20f, -.04f);
                flight.Initialize(null, Destination, Vector3.zero, 2f, 1.5f, BladeAxis, new Vector3(0f, 0f, 61f), null);
                AssertGroundPose(slopeEffect, floor, -.04f);
                flight.Tick(.1f);
                AssertGroundPose(slopeEffect, floor, -.04f);
                results.Add("PASS: tilted BoxCollider aligns ground art to the hit normal, accepts negative surface offset, and preserves the 5m airborne flight height.");
            }
            finally { UnityEngine.Object.DestroyImmediate(slopeEffect); }

            results.Add("Only isolated preview colliders and prefab clones were created. No live scenes, prefab assets, save data or global Physics settings were changed.");
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
            Physics.SyncTransforms();
        }
    }

    static void ValidateOverFloor(GameObject prefab, Transform staging, BoxCollider floor, Scene preview, bool verifyDebris)
    {
        var effect = Clone(prefab, staging, preview);
        try
        {
            var flight = effect.AddComponent<CombatSwordSlashProjectile>();
            // Leave the defaults intact here: the normal mask must include Surface layer 8.
            flight.Initialize(null, Destination, Vector3.zero, 2f, 1.5f, BladeAxis, new Vector3(0f, 0f, 47f), null);
            AssertGroundPose(effect, floor, .02f);
            var decals = effect.GetComponentsInChildren<MaykerStudio.VFX.SlashDecal>(true);
            Check(decals.Length > 0 && decals.All(decal => !decal.enabled), "Combat must own every package SlashDecal.");
            var groundSystems = GroundSystems(effect);
            Check(groundSystems.Length > 0 && groundSystems.All(particles => particles.main.simulationSpace == ParticleSystemSimulationSpace.World),
                "The complete ground subtree must use World simulation before emission.");
            Check(decals.All(decal => decal.GetComponent<ParticleSystem>().isPlaying), "Ground effects never started over a valid floor.");
            Check(effect.GetComponent<ParticleSystem>().isPlaying, "The airborne slash was stopped by grounding setup.");

            ParticleSystem debris = null;
            var fixedDebrisPosition = new Vector3(.13f, .07f, -.11f);
            if (verifyDebris)
            {
                debris = groundSystems.FirstOrDefault(particles => particles.name.IndexOf("Debris", StringComparison.OrdinalIgnoreCase) >= 0);
                Check(debris != null, "Earth's ground-owned Debris system is missing.");
                // A long-lived stationary particle makes parent motion/roll drift measurable
                // without depending on random authored velocity or emission intervals.
                debris.SetParticles(new[] { new ParticleSystem.Particle
                {
                    position = fixedDebrisPosition, velocity = Vector3.zero,
                    startLifetime = 10f, remainingLifetime = 10f,
                    startSize = .1f, startColor = Color.white, randomSeed = DebrisSeed
                } }, 1);
                AssertDebris(debris, fixedDebrisPosition);
            }

            flight.Tick(.25f);
            Check(Vector3.Distance(effect.transform.position, Release + Vector3.forward * .5f) < .002f,
                "Ground ownership changed airborne projectile speed or height.");
            AssertGroundPose(effect, floor, .02f);
            if (debris != null) AssertDebris(debris, fixedDebrisPosition);
            effect.transform.SetPositionAndRotation(new Vector3(2f, 5f, 2f), Quaternion.Euler(37f, 121f, -68f));
            Physics.SyncTransforms();
            flight.Tick(0f);
            AssertGroundPose(effect, floor, .02f, Destination - effect.transform.position);
            if (debris != null) AssertDebris(debris, fixedDebrisPosition);

            var oldGroundPositions = decals.Select(decal => decal.transform.position).ToArray();
            var oldGroundRotations = decals.Select(decal => decal.transform.rotation).ToArray();
            floor.enabled = false;
            Physics.SyncTransforms();
            flight.Tick(.25f);
            AssertNoGroundEmission(effect);
            Check(effect.GetComponent<ParticleSystem>().isPlaying && !flight.HasArrived,
                "Losing the floor stopped airborne flight.");
            for (int i = 0; i < decals.Length; i++)
                Check(Vector3.Distance(decals[i].transform.position, oldGroundPositions[i]) < .002f &&
                    Quaternion.Angle(decals[i].transform.rotation, oldGroundRotations[i]) < .05f,
                    "A fading ground subtree followed the airborne parent after losing the floor.");
            if (debris != null) AssertDebris(debris, fixedDebrisPosition);
        }
        finally { UnityEngine.Object.DestroyImmediate(effect); }
    }

    static void ValidateWithoutFloor(GameObject prefab, Transform staging, Scene preview)
    {
        var effect = Clone(prefab, staging, preview);
        try
        {
            var flight = effect.AddComponent<CombatSwordSlashProjectile>();
            flight.ConfigureGrounding(1 << 8, 20f, .02f);
            flight.Initialize(null, Destination, Vector3.zero, 2f, 1.5f, BladeAxis, new Vector3(0f, 0f, -74f), null);
            AssertNoGroundEmission(effect);
            Check(GroundSystems(effect).All(particles => particles.particleCount == 0), "Ground particles burst before a valid surface existed.");
            flight.Tick(.25f);
            AssertNoGroundEmission(effect);
            Check(GroundSystems(effect).All(particles => particles.particleCount == 0), "A no-floor slash emitted airborne debris/puddles.");
            Check(effect.GetComponent<ParticleSystem>().isPlaying && !flight.HasArrived &&
                Vector3.Distance(effect.transform.position, Release + Vector3.forward * .5f) < .002f,
                "The airborne slash must keep travelling and playing without a floor.");
        }
        finally { UnityEngine.Object.DestroyImmediate(effect); }
    }

    static void ValidateHeadings(GameObject prefab, Transform staging, BoxCollider floor, Scene preview)
    {
        foreach (var direction in new[] { Vector3.right, new Vector3(1f, 0f, 1f).normalized })
        {
            var effect = Clone(prefab, staging, preview);
            try
            {
                var flight = effect.AddComponent<CombatSwordSlashProjectile>();
                var destination = Release + direction * 12f;
                flight.Initialize(null, destination, Vector3.zero, 2f, 1.5f, BladeAxis, new Vector3(23f, 31f, 47f), null);
                AssertGroundPose(effect, floor, .02f, direction);
                var decal = effect.GetComponentsInChildren<MaykerStudio.VFX.SlashDecal>(true).Single();
                var particles = decal.GetComponent<ParticleSystem>();
                var oldParticles = new ParticleSystem.Particle[Mathf.Max(1, particles.particleCount)];
                int oldCount = particles.GetParticles(oldParticles);
                Check(oldCount > 0, "A valid floor must receive the first stamp immediately.");
                flight.Tick(.25f);
                AssertGroundPose(effect, floor, .02f, direction);
                Check(Vector3.Distance(effect.transform.position, Release + direction * .5f) < .002f,
                    "Ground heading changed the projectile's actual travel vector.");
                effect.transform.position += new Vector3(1f, 0f, -1f);
                flight.Tick(0f);
                AssertGroundPose(effect, floor, .02f, destination - effect.transform.position);
                var after = new ParticleSystem.Particle[Mathf.Max(1, particles.particleCount)];
                int afterCount = particles.GetParticles(after);
                foreach (var old in oldParticles.Take(oldCount))
                    Check(after.Take(afterCount).Any(particle => particle.randomSeed == old.randomSeed &&
                        Vector3.Distance(particle.position, old.position) < .002f &&
                        Quaternion.Angle(Quaternion.Euler(particle.rotation3D), Quaternion.Euler(old.rotation3D)) < .05f),
                        "Changing the ground heading moved or rotated an already-emitted world stamp.");
            }
            finally { UnityEngine.Object.DestroyImmediate(effect); }
        }
    }

    static void AddExcludedColliders(Scene preview)
    {
        var actor = ObjectInScene("Ignored BattleUnit Collider", preview);
        actor.layer = 8;
        actor.transform.position = new Vector3(0f, 2.5f, 2f);
        var unit = actor.AddComponent<BattleUnit>();
        unit.SetPersistentHP(100);
        var actorCollider = actor.AddComponent<BoxCollider>();
        actorCollider.size = new Vector3(20f, 1f, 20f);

        var controllerObject = ObjectInScene("Ignored CharacterController", preview);
        controllerObject.layer = 8;
        controllerObject.transform.position = new Vector3(0f, 2f, 0f);
        var controller = controllerObject.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 1f;

        var otherVfx = ObjectInScene("Ignored Other Projectile Collider", preview);
        otherVfx.layer = 8;
        otherVfx.transform.position = new Vector3(0f, 3.5f, 0f);
        otherVfx.AddComponent<CombatSwordSlashProjectile>();
        otherVfx.AddComponent<BoxCollider>().size = new Vector3(6f, .3f, 6f);
    }

    static void AssertGroundPose(GameObject effect, BoxCollider floor, float offset, Vector3? travelDirection = null)
    {
        Check(floor.Raycast(new Ray(effect.transform.position + Vector3.up * .25f, Vector3.down), out var surface, 20.25f),
            "The isolated floor did not intersect the downward test ray.");
        foreach (var decal in effect.GetComponentsInChildren<MaykerStudio.VFX.SlashDecal>(true))
        {
            var expectedPosition = surface.point + surface.normal * offset;
            var heading = Vector3.ProjectOnPlane(travelDirection ?? Vector3.forward, surface.normal).normalized;
            var expectedRotation = Quaternion.LookRotation(heading, surface.normal) * decal.AuthoredGroundRotation;
            Check(Vector3.Distance(decal.transform.position, expectedPosition) < .003f,
                "Ground effect used the sword/actor height instead of the floor: " + decal.transform.position + ".");
            Check(Quaternion.Angle(decal.transform.rotation, expectedRotation) < .05f,
                "Ground art did not follow the true surface-projected travel heading.");
            var renderer = decal.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null)
            {
                var particles = decal.GetComponent<ParticleSystem>();
                var main = particles.main;
                Check(renderer.alignment == ParticleSystemRenderSpace.World && main.startRotation3D,
                    "A persistent ground stamp needs world renderer alignment and explicit 3D birth rotation.");
                foreach (float spin in new[] { main.startRotationZ.constantMin, main.startRotationZ.constantMax })
                {
                    var rotation = Quaternion.Euler(new Vector3(main.startRotationX.constantMax,
                        main.startRotationY.constantMax, spin) * Mathf.Rad2Deg);
                    Check(Vector3.Dot(rotation * Vector3.back, surface.normal) > .999f &&
                        Mathf.Abs(Vector3.Dot(rotation * Vector3.up, heading)) > .998f,
                        "The stamp's Quad plane is not flat on the floor or its long +Y axis crosses the travel path.");
                }
                float stampLength = Mathf.Max(.1f, (main.startSize3D ? main.startSizeY.constantMax : main.startSize.constantMax) * renderer.mesh.bounds.size.y);
                Check(particles.emission.rateOverDistance.constantMax + .0001f >= 4f / stampLength,
                    "Ground stamps are too far apart to overlap along the path.");
            }
        }
        Check(Mathf.Abs(effect.transform.position.y - 5f) < .002f, "Grounding moved the projectile down to the floor.");
    }

    static void AssertDebris(ParticleSystem debris, Vector3 position)
    {
        var particles = new ParticleSystem.Particle[Mathf.Max(1, debris.particleCount)];
        int count = debris.GetParticles(particles);
        Check(particles.Take(count).Any(particle => particle.randomSeed == DebrisSeed && Vector3.Distance(particle.position, position) < .002f),
            "Already-emitted ground debris followed a later projectile move/roll or was erased when the floor disappeared.");
    }

    static void AssertNoGroundEmission(GameObject effect) => Check(GroundSystems(effect).All(particles => !particles.isEmitting),
        "A ground-only Decal/Debris subtree is emitting without a valid floor.");

    static ParticleSystem[] GroundSystems(GameObject effect) => effect.GetComponentsInChildren<MaykerStudio.VFX.SlashDecal>(true)
        .SelectMany(decal => decal.GetComponentsInChildren<ParticleSystem>(true)).Distinct().ToArray();

    static GameObject Clone(GameObject prefab, Transform staging, Scene preview)
    {
        var clone = UnityEngine.Object.Instantiate(prefab, staging, false);
        clone.SetActive(false);
        foreach (var audio in clone.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var particles in clone.GetComponentsInChildren<ParticleSystem>(true))
        {
            var collision = particles.collision;
            collision.enabled = false;
            var trigger = particles.trigger;
            trigger.enabled = false;
        }
        clone.transform.SetParent(null, false);
        SceneManager.MoveGameObjectToScene(clone, preview);
        clone.transform.position = Release;
        return clone;
    }

    static GameObject Load(string element)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Slash Projectile VFX " + element + ".prefab");
        Check(prefab != null, "Missing package projectile: " + element);
        return prefab;
    }

    static GameObject ObjectInScene(string name, Scene preview)
    {
        var result = new GameObject(name);
        SceneManager.MoveGameObjectToScene(result, preview);
        return result;
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
