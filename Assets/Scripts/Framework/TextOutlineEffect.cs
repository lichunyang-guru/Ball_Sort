using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Text描边效果（Luna兼容）
/// 通过向8个方向复制顶点并着色来模拟Outline效果
/// 替代Unity内置Outline组件，在Luna H5环境下正常工作
/// </summary>
[RequireComponent(typeof(Text))]
[DisallowMultipleComponent]
public class TextOutlineEffect : BaseMeshEffect
{
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField] private float outlineWidth = 1f;

    public Color OutlineColor
    {
        get { return outlineColor; }
        set
        {
            outlineColor = value;
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    public float OutlineWidth
    {
        get { return outlineWidth; }
        set
        {
            outlineWidth = value;
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    // 8方向偏移（上下左右 + 四个对角线），缓存避免GC
    private static readonly Vector2[] directions = new Vector2[]
    {
        new Vector2(-1, 0),
        new Vector2(1, 0),
        new Vector2(0, -1),
        new Vector2(0, 1),
        new Vector2(-1, -1),
        new Vector2(1, -1),
        new Vector2(-1, 1),
        new Vector2(1, 1)
    };

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || outlineWidth <= 0f) return;

        int originalCount = vh.currentVertCount;
        if (originalCount == 0) return;

        List<UIVertex> originalVerts = new List<UIVertex>(originalCount);
        for (int i = 0; i < originalCount; i++)
        {
            UIVertex v = new UIVertex();
            vh.PopulateUIVertex(ref v, i);
            originalVerts.Add(v);
        }

        vh.Clear();

        Color32 outColor32 = outlineColor;

        // 先添加8方向描边副本（底层）
        for (int d = 0; d < directions.Length; d++)
        {
            float offsetX = directions[d].x * outlineWidth;
            float offsetY = directions[d].y * outlineWidth;

            for (int i = 0; i < originalCount; i++)
            {
                UIVertex v = originalVerts[i];
                v.position.x += offsetX;
                v.position.y += offsetY;
                v.color = outColor32;
                vh.AddVert(v);
            }
        }

        // 再添加原始顶点（顶层）
        for (int i = 0; i < originalCount; i++)
        {
            vh.AddVert(originalVerts[i]);
        }

        // 重建三角形索引
        int totalCopies = directions.Length + 1;
        for (int copy = 0; copy < totalCopies; copy++)
        {
            int baseIdx = copy * originalCount;
            for (int i = 0; i < originalCount; i += 4)
            {
                int idx = baseIdx + i;
                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx + 2, idx + 3, idx);
            }
        }
    }
}
