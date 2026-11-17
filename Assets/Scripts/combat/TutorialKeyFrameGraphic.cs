using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialKeyFrameGraphic : MaskableGraphic
{
    readonly List<Rect> boxes = new List<Rect>();
    public float lineWidth = .85f;
    public int BoxCount => boxes.Count;
    public void SetBoxes(List<Rect> value) { boxes.Clear(); boxes.AddRange(value); SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); float width = Mathf.Max(lineWidth, 1f / Mathf.Max(.01f, canvas != null ? canvas.scaleFactor : 1f));
        foreach (var box in boxes)
        {
            Quad(mesh, new Rect(box.xMin, box.yMin, box.width, width));
            Quad(mesh, new Rect(box.xMin, box.yMax - width, box.width, width));
            Quad(mesh, new Rect(box.xMin, box.yMin + width, width, Mathf.Max(0, box.height - width * 2)));
            Quad(mesh, new Rect(box.xMax - width, box.yMin + width, width, Mathf.Max(0, box.height - width * 2)));
        }
    }
    void Quad(VertexHelper mesh, Rect rect)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector2(rect.xMin, rect.yMin), color, Vector2.zero); mesh.AddVert(new Vector2(rect.xMin, rect.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector2(rect.xMax, rect.yMax), color, Vector2.zero); mesh.AddVert(new Vector2(rect.xMax, rect.yMin), color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}
