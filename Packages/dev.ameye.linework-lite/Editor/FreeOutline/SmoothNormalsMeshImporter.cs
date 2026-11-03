using System.Collections.Generic;
using LineworkLite.FreeOutline;
using UnityEditor;
using UnityEngine;

namespace LineworkLite.Editor.FreeOutline
{
    public class SmoothNormalsMeshImporter : AssetPostprocessor
    {
        public void OnPostprocessModel(GameObject gameObject)
        {
            if (gameObject == null || assetImporter == null) return;

            // Only models explicitly opted in to Linework may be modified.
            // In particular, preserve UV8 authored by unrelated packages.
            bool smoothNormals = false;
            foreach (var label in AssetDatabase.GetLabels(assetImporter))
            {
                if (label != FreeOutlineUtils.SmoothNormalsLabel) continue;
                smoothNormals = true;
                break;
            }
            if (!smoothNormals) return;

            var meshes = GetMeshesForGameobject(gameObject);

            foreach (var mesh in meshes)
            {
                if (mesh.vertexCount == 0) continue;
                var uvs = SmoothNormalsBaker.ComputeSmoothedNormals(mesh);
                if (uvs != null && uvs.Length == mesh.vertexCount)
                {
                    mesh.SetUVs(7, uvs);
                }
                else
                {
                    Debug.LogWarning($"Smoothed normals not computed for mesh {mesh.name}.", gameObject);
                }
            }
        }

        private static HashSet<Mesh> GetMeshesForGameobject(GameObject gameObject)
        {
            var meshes = new HashSet<Mesh>();
            foreach (var filter in gameObject.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
            foreach (var renderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null) meshes.Add(renderer.sharedMesh);
            return meshes;
        }
    }
}
