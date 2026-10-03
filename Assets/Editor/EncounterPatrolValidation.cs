using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
[InitializeOnLoad]
public static class EncounterPatrolValidation
{
    const string Folder = "Library/CombatValidation/";
    static EncounterPatrolValidation() { EditorApplication.update += Poll; }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Poll()
    {
        if (!File.Exists(Folder + "EncounterPatrol.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "EncounterPatrol.request");
        GameObject actor = null, wall = null, floor = null;
        NavMeshData data = null; NavMeshDataInstance instance = default;
        try
        {
            var origin = new Vector3(30000, 0, 30000);
            var sources = new List<NavMeshBuildSource> {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(24, .2f, 24), transform = Matrix4x4.TRS(origin - Vector3.up * .1f, Quaternion.identity, Vector3.one), area = 0 },
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(2, 4, 8), transform = Matrix4x4.TRS(origin + Vector3.up * 2, Quaternion.identity, Vector3.one), area = 0 }
            };
            var settings = NavMesh.GetSettingsByIndex(0);
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin, new Vector3(30, 15, 30)), Vector3.zero, Quaternion.identity);
            Check(data != null, "Synthetic NavMesh build failed"); instance = NavMesh.AddNavMeshData(data);
            var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(origin + Vector3.left * 7, origin + Vector3.right * 7, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 3, "Ground path did not route around wall");
            actor = new GameObject("Patrol validation actor"); actor.transform.position = origin + Vector3.up * 10;
            var ai = actor.AddComponent<EnemyEncounterBehaviour>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var blocked = typeof(EnemyEncounterBehaviour).GetMethod("Blocked", flags);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = actor.transform.position + Vector3.forward * 2;
            wall.transform.localScale = new Vector3(4, 4, .5f); Physics.SyncTransforms();
            Check((bool)blocked.Invoke(ai, new object[] { actor.transform.position, Vector3.forward, 3f }), "Flight sweep missed obstacle");
            Check(!(bool)blocked.Invoke(ai, new object[] { actor.transform.position, Vector3.back, 3f }), "Clear flight route incorrectly blocked");
            wall.transform.SetParent(actor.transform);
            Check(!(bool)blocked.Invoke(ai, new object[] { actor.transform.position, Vector3.forward, 3f }), "Flight sweep collided with its own model");
            UnityEngine.Object.DestroyImmediate(wall); wall = null;
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = origin - Vector3.up * .1f; floor.transform.localScale = new Vector3(24, .2f, 24);
            actor.transform.position = origin + Vector3.left * 7;
            Physics.SyncTransforms();
            var agent = actor.AddComponent<NavMeshAgent>();
            Check(NavMesh.SamplePosition(actor.transform.position, out var start, 1, NavMesh.AllAreas), "No starting polygon");
            agent.Warp(start.position);
            typeof(EnemyEncounterBehaviour).GetField("agent", flags).SetValue(ai, agent);
            typeof(EnemyEncounterBehaviour).GetField("home", flags).SetValue(ai, actor.transform.position);
            ai.travelMode = EnemyEncounterBehaviour.TravelMode.GroundAndFlying;
            var change = typeof(EnemyEncounterBehaviour).GetMethod("TrySwitchTravelMode", flags);
            var advance = typeof(EnemyEncounterBehaviour).GetMethod("UpdateFlightTransition", flags);
            Check((bool)change.Invoke(ai, null) && !agent.enabled, "Takeoff did not release agent");
            typeof(EnemyEncounterBehaviour).GetField("transitionProgress", flags).SetValue(ai, 1f);
            advance.Invoke(ai, null);
            Check(ai.IsAirborne && !agent.enabled, "Takeoff did not complete");
            Check((bool)change.Invoke(ai, null), "Landing rejected valid ground");
            typeof(EnemyEncounterBehaviour).GetField("transitionProgress", flags).SetValue(ai, 1f);
            advance.Invoke(ai, null);
            Check(!ai.IsAirborne && agent.enabled && agent.isOnNavMesh, "Landing did not restore navigation");
            File.WriteAllText(Folder + "EncounterPatrol.result.txt", "PASS: ground/air takeoff and landing with agent handoff; ground NavMesh routes around obstacle; flying sweep blocks geometry, accepts clear path, ignores own model. Gameplay detection/attack and map-specific bake still require Play Mode verification.");
        }
        catch (Exception e) { File.WriteAllText(Folder + "EncounterPatrol.result.txt", e.ToString()); }
        finally
        {
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            if (instance.valid) instance.Remove();
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }
}
