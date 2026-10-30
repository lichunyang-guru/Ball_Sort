using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Text加粗效果（Luna兼容）
/// 通过向四个方向微量偏移顶点副本来模拟Bold效果
/// 挂载到带有Text组件的GameObject上即可生效
/// </summary>
[RequireComponent(typeof(Text))]
[DisallowMultipleComponent]
public class TextBoldEffect : BaseMeshEffect
{
    [SerializeField] private float boldStrength = 0.5f;

    public float BoldStrength
    {
        get { return boldStrength; }
        set
        {
            boldStrength = value;
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || boldStrength <= 0f) return;

        int originalCount = vh.currentVertCount;
        if (originalCount == 0) return;

        List<UIVertex> verts = new List<UIVertex>(originalCount);
        for (int i = 0; i < originalCount; i++)
        {
            UIVertex v = new UIVertex();
            vh.PopulateUIVertex(ref v, i);
            verts.Add(v);
        }

        // 四方向偏移来模拟加粗
        Vector2[] offsets = new Vector2[]
        {
            new Vector2(-boldStrength, 0),
            new Vector2(boldStrength, 0),
            new Vector2(0, -boldStrength),
            new Vector2(0, boldStrength)
        };

        vh.Clear();

        // 先添加偏移副本（底层）
        for (int d = 0; d < offsets.Length; d++)
        {
            Vector2 offset = offsets[d];
            for (int i = 0; i < originalCount; i++)
            {
                UIVertex v = verts[i];
                v.position.x += offset.x;
                v.position.y += offset.y;
                vh.AddVert(v);
            }
        }

        // 再添加原始顶点（顶层，保证文字清晰）
        for (int i = 0; i < originalCount; i++)
        {
            vh.AddVert(verts[i]);
        }

        // 重建三角形索引
        // 每个字符4个顶点，2个三角形（6个索引）
        int totalCopies = offsets.Length + 1;
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
