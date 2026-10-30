using UnityEngine;
using UnityEditor;

/// <summary>
/// UIEdgeAdaptiveScaler 自定义编辑器
/// 在Scene视图中绘制边距辅助线，方便未运行时直观观察边距配置
/// Inspector中显示当前缩放因子
/// </summary>
[CustomEditor(typeof(UIEdgeAdaptiveScaler))]
public class UIEdgeAdaptiveScalerEditor : Editor
{
    private static readonly Color widthEdgeColor = new Color(1f, 0.4f, 0.1f, 0.6f);
    private static readonly Color heightEdgeColor = new Color(0.2f, 0.6f, 1f, 0.6f);
    private static readonly Color safeAreaColor = new Color(0.2f, 1f, 0.3f, 0.15f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UIEdgeAdaptiveScaler scaler = (UIEdgeAdaptiveScaler)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("运行时信息", EditorStyles.boldLabel);

        GUI.enabled = false;
        EditorGUILayout.FloatField("当前缩放因子", scaler.CurrentScaleFactor);

        Vector2 canvasSize = scaler.GetCanvasSize();
        EditorGUILayout.Vector2Field("Canvas尺寸", canvasSize);

        scaler.GetEdgeDistances(out float left, out float right, out float bottom, out float top);
        EditorGUILayout.LabelField("原始边距（scale=1时）");
        EditorGUILayout.Vector2Field("  左 / 右", new Vector2(left, right));
        EditorGUILayout.Vector2Field("  下 / 上", new Vector2(bottom, top));
        GUI.enabled = true;

        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private void OnSceneGUI()
    {
        UIEdgeAdaptiveScaler scaler = (UIEdgeAdaptiveScaler)target;
        if (!scaler.ShowEdgeGizmos) return;

        RectTransform selfRT = scaler.SelfRT;
        RectTransform canvasRT = scaler.RootCanvasRT;
        if (selfRT == null || canvasRT == null) return;

        Vector2 canvasSize = canvasRT.rect.size;
        if (canvasSize.x <= 0f || canvasSize.y <= 0f) return;

        // Canvas世界空间四角
        Vector3[] canvasCorners = new Vector3[4];
        canvasRT.GetWorldCorners(canvasCorners);
        Vector3 canvasWorldMin = canvasCorners[0];
        Vector3 canvasWorldMax = canvasCorners[2];
        Vector3 canvasWorldSize = canvasWorldMax - canvasWorldMin;

        // UI单位到世界单位的缩放比
        float scaleX = canvasWorldSize.x / canvasSize.x;
        float scaleY = canvasWorldSize.y / canvasSize.y;

        // UI世界空间四角（当前实际位置和尺寸）
        Vector3[] uiCorners = new Vector3[4];
        selfRT.GetWorldCorners(uiCorners);
        Vector3 uiWorldMin = uiCorners[0];
        Vector3 uiWorldMax = uiCorners[2];

        float edgeW = scaler.MinEdgeWidth;
        float edgeH = scaler.MinEdgeHeight;
        float z = canvasWorldMin.z;

        UIEdgeAdaptiveScaler.CheckMode mode = scaler.CurrentCheckMode;

        // 绘制宽度边距区域（从UI左右边缘向外延伸minEdgeWidth的区域）
        if (mode == UIEdgeAdaptiveScaler.CheckMode.Width || mode == UIEdgeAdaptiveScaler.CheckMode.Both)
        {
            float edgeWorldW = edgeW * scaleX;

            // UI左侧边距区域
            DrawFilledRect(
                new Vector3(uiWorldMin.x - edgeWorldW, uiWorldMin.y, z),
                new Vector3(uiWorldMin.x, uiWorldMax.y, z),
                widthEdgeColor);

            // UI右侧边距区域
            DrawFilledRect(
                new Vector3(uiWorldMax.x, uiWorldMin.y, z),
                new Vector3(uiWorldMax.x + edgeWorldW, uiWorldMax.y, z),
                widthEdgeColor);
        }

        // 绘制高度边距区域（从UI上下边缘向外延伸minEdgeHeight的区域）
        if (mode == UIEdgeAdaptiveScaler.CheckMode.Height || mode == UIEdgeAdaptiveScaler.CheckMode.Both)
        {
            float edgeWorldH = edgeH * scaleY;

            // UI下侧边距区域
            DrawFilledRect(
                new Vector3(uiWorldMin.x, uiWorldMin.y - edgeWorldH, z),
                new Vector3(uiWorldMax.x, uiWorldMin.y, z),
                heightEdgeColor);

            // UI上侧边距区域
            DrawFilledRect(
                new Vector3(uiWorldMin.x, uiWorldMax.y, z),
                new Vector3(uiWorldMax.x, uiWorldMax.y + edgeWorldH, z),
                heightEdgeColor);
        }

        // 绘制UI原始边界线框（绿色）
        DrawWireRect(uiWorldMin, uiWorldMax, Color.green);

        // 绘制Canvas边界线框（白色虚线）
        DrawWireRect(canvasWorldMin, canvasWorldMax, new Color(1f, 1f, 1f, 0.3f));

        // 标注边距数值
        scaler.GetEdgeDistances(out float left, out float right, out float bottom, out float top);
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };

        if (mode == UIEdgeAdaptiveScaler.CheckMode.Width || mode == UIEdgeAdaptiveScaler.CheckMode.Both)
        {
            float midY = (uiWorldMin.y + uiWorldMax.y) * 0.5f;
            labelStyle.normal.textColor = left < edgeW ? Color.red : Color.green;
            Handles.Label(new Vector3(uiWorldMin.x - edgeW * scaleX * 0.5f, midY, z), $"L:{left:F0}", labelStyle);
            labelStyle.normal.textColor = right < edgeW ? Color.red : Color.green;
            Handles.Label(new Vector3(uiWorldMax.x + edgeW * scaleX * 0.5f, midY, z), $"R:{right:F0}", labelStyle);
        }

        if (mode == UIEdgeAdaptiveScaler.CheckMode.Height || mode == UIEdgeAdaptiveScaler.CheckMode.Both)
        {
            float midX = (uiWorldMin.x + uiWorldMax.x) * 0.5f;
            labelStyle.normal.textColor = bottom < edgeH ? Color.red : Color.green;
            Handles.Label(new Vector3(midX, uiWorldMin.y - edgeH * scaleY * 0.5f, z), $"B:{bottom:F0}", labelStyle);
            labelStyle.normal.textColor = top < edgeH ? Color.red : Color.green;
            Handles.Label(new Vector3(midX, uiWorldMax.y + edgeH * scaleY * 0.5f, z), $"T:{top:F0}", labelStyle);
        }
    }

    /// <summary>
    /// 绘制半透明填充矩形
    /// </summary>
    private static void DrawFilledRect(Vector3 min, Vector3 max, Color color)
    {
        Vector3[] verts = new Vector3[4];
        verts[0] = new Vector3(min.x, min.y, min.z);
        verts[1] = new Vector3(min.x, max.y, min.z);
        verts[2] = new Vector3(max.x, max.y, min.z);
        verts[3] = new Vector3(max.x, min.y, min.z);

        Handles.DrawSolidRectangleWithOutline(verts, color, new Color(color.r, color.g, color.b, 0.8f));
    }

    /// <summary>
    /// 绘制线框矩形
    /// </summary>
    private static void DrawWireRect(Vector3 min, Vector3 max, Color color)
    {
        Color prevColor = Handles.color;
        Handles.color = color;

        Vector3 bl = new Vector3(min.x, min.y, min.z);
        Vector3 tl = new Vector3(min.x, max.y, min.z);
        Vector3 tr = new Vector3(max.x, max.y, min.z);
        Vector3 br = new Vector3(max.x, min.y, min.z);

        Handles.DrawLine(bl, tl);
        Handles.DrawLine(tl, tr);
        Handles.DrawLine(tr, br);
        Handles.DrawLine(br, bl);

        Handles.color = prevColor;
    }
}