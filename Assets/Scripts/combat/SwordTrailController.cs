using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SwordBladeTrail : MonoBehaviour
{
    [Header("2 Điểm Định Hình Lưỡi Kiếm")]
    public Transform pointBase; // Gốc lưỡi kiếm (ngay trên tay cầm)
    public Transform pointTip;  // Mũi kiếm

    [Header("Cấu Hình Tà Ảnh")]
    public float trailTime = 0.15f;         // Thời gian tà ảnh biến mất (giây)
    public float minVertexDistance = 0.02f; // Khoảng cách tối thiểu giữa 2 khung hình
    public Material trailMaterial;

    private bool isEmitting = false;
    private Mesh trailMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private class FrameSnapshot
    {
        public Vector3 basePos;
        public Vector3 tipPos;
        public float timeCreated;
    }

    private List<FrameSnapshot> snapshots = new List<FrameSnapshot>();

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        trailMesh = new Mesh();
        meshFilter.mesh = trailMesh;

        if (trailMaterial != null)
            meshRenderer.material = trailMaterial;
    }

    public void StartTrail()
    {
        isEmitting = true;
        snapshots.Clear();
        trailMesh.Clear();
    }

    public void StopTrail()
    {
        isEmitting = false;
    }

    void LateUpdate()
    {
        if (pointBase == null || pointTip == null) return;

        // Xóa các điểm đã quá thời gian tồn tại
        snapshots.RemoveAll(s => Time.time - s.timeCreated > trailTime);

        if (isEmitting)
        {
            bool shouldAdd = false;
            if (snapshots.Count == 0)
            {
                shouldAdd = true;
            }
            else
            {
                float distBase = Vector3.Distance(snapshots[0].basePos, pointBase.position);
                float distTip = Vector3.Distance(snapshots[0].tipPos, pointTip.position);
                if (distBase > minVertexDistance || distTip > minVertexDistance)
                {
                    shouldAdd = true;
                }
            }

            if (shouldAdd)
            {
                snapshots.Insert(0, new FrameSnapshot
                {
                    basePos = pointBase.position,
                    tipPos = pointTip.position,
                    timeCreated = Time.time
                });
            }
        }

        GenerateMesh();
    }

    void GenerateMesh()
    {
        trailMesh.Clear();
        if (snapshots.Count < 2) return;

        Vector3[] vertices = new Vector3[snapshots.Count * 2];
        Vector2[] uvs = new Vector2[snapshots.Count * 2];
        int[] triangles = new int[(snapshots.Count - 1) * 6];

        for (int i = 0; i < snapshots.Count; i++)
        {
            vertices[i * 2] = transform.InverseTransformPoint(snapshots[i].basePos);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(snapshots[i].tipPos);

            float uvRatio = (float)i / (snapshots.Count - 1);
            uvs[i * 2] = new Vector2(uvRatio, 0);
            uvs[i * 2 + 1] = new Vector2(uvRatio, 1);

            if (i < snapshots.Count - 1)
            {
                int triIndex = i * 6;
                int vertIndex = i * 2;

                triangles[triIndex] = vertIndex;
                triangles[triIndex + 1] = vertIndex + 1;
                triangles[triIndex + 2] = vertIndex + 2;

                triangles[triIndex + 3] = vertIndex + 2;
                triangles[triIndex + 4] = vertIndex + 1;
                triangles[triIndex + 5] = vertIndex + 3;
            }
        }

        trailMesh.vertices = vertices;
        trailMesh.uv = uvs;
        trailMesh.triangles = triangles;
    }
}