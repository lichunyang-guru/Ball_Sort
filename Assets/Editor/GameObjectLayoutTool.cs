using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GameObject布局工具 - 用于在Unity编辑器中自动排列游戏对象
/// 支持横向、纵向和网格布局
/// </summary>
public class GameObjectLayoutTool : EditorWindow
{
    private const string CacheKeyPrefix = "GameObjectLayoutTool.Cache.";

    // 布局模式枚举
    public enum LayoutMode
    {
        Horizontal,  // 横向布局
        Vertical,    // 纵向布局
        Grid         // 网格布局
    }

    // 对齐方式枚举
    public enum AlignmentMode
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    // 工具参数
    private Transform parentTransform;
    private LayoutMode layoutMode = LayoutMode.Horizontal;
    private AlignmentMode alignmentMode = AlignmentMode.MiddleCenter;
    private Vector3 spacing = new Vector3(1f, 1f, 0f);
    private Vector3 padding = new Vector3(0f, 0f, 0f);
    private int columns = 3;  // 网格布局的列数

    // 选中的GameObject列表
    private List<Transform> selectedObjects = new List<Transform>();
    private Vector2 scrollPosition;

    // 预览相关
    private bool showPreview = false;
    private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Vector3> previewPositions = new Dictionary<Transform, Vector3>();

    [Serializable]
    private class LayoutToolCache
    {
        public string parentTransformGlobalId;
        public LayoutMode layoutMode;
        public AlignmentMode alignmentMode;
        public Vector3 spacing;
        public Vector3 padding;
        public int columns;
        public List<string> selectedObjectGlobalIds = new List<string>();
    }

    [MenuItem("Tools/Custom Tools/GameObject Layout Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<GameObjectLayoutTool>("GameObject布局工具");
        window.minSize = new Vector2(400, 600);
    }

    private void OnEnable()
    {
        LoadCache();
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader();
        EditorGUILayout.Space(10);

        DrawLayoutSettings();
        EditorGUILayout.Space(10);

        DrawObjectSelection();
        EditorGUILayout.Space(10);

        DrawActionButtons();

        EditorGUILayout.EndScrollView();

        if (EditorGUI.EndChangeCheck())
        {
            SaveCache();
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("GameObject布局工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("此工具可以帮助您快速排列场景中的游戏对象，支持横向、纵向和网格布局。", MessageType.Info);
    }

    private void DrawLayoutSettings()
    {
        EditorGUILayout.LabelField("布局设置", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            // 布局模式
            layoutMode = (LayoutMode)EditorGUILayout.EnumPopup("布局模式", layoutMode);

            // 对齐方式
            alignmentMode = (AlignmentMode)EditorGUILayout.EnumPopup("对齐方式", alignmentMode);

            // 间距设置
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("间距设置", EditorStyles.miniLabel);

            if (layoutMode == LayoutMode.Horizontal)
            {
                spacing.x = EditorGUILayout.FloatField("水平间距", spacing.x);
                spacing.z = EditorGUILayout.FloatField("Z轴间距", spacing.z);
            }
            else if (layoutMode == LayoutMode.Vertical)
            {
                spacing.y = EditorGUILayout.FloatField("垂直间距", spacing.y);
                spacing.z = EditorGUILayout.FloatField("Z轴间距", spacing.z);
            }
            else // Grid
            {
                spacing = EditorGUILayout.Vector3Field("网格间距 (X/Y/Z)", spacing);
                columns = EditorGUILayout.IntSlider("列数", columns, 1, 10);
            }

            // 边距设置
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("边距设置", EditorStyles.miniLabel);
            padding = EditorGUILayout.Vector3Field("边距 (X/Y/Z)", padding);

            // 父对象设置（可选）
            EditorGUILayout.Space(5);
            parentTransform = (Transform)EditorGUILayout.ObjectField(
                "父对象（可选）",
                parentTransform,
                typeof(Transform),
                true
            );
        }
    }

    private void DrawObjectSelection()
    {
        EditorGUILayout.LabelField("对象选择", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("从选中对象获取", GUILayout.Height(30)))
            {
                GetObjectsFromSelection();
            }

            if (GUILayout.Button("从父对象获取子对象", GUILayout.Height(30)))
            {
                GetObjectsFromParent();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"已选择 {selectedObjects.Count} 个游戏对象：");

            // 显示选中的对象列表
            if (selectedObjects.Count > 0)
            {
                using (var scrollView = new EditorGUILayout.ScrollViewScope(new Vector2(0, 100), GUILayout.MaxHeight(100)))
                {
                    for (int i = 0; i < selectedObjects.Count; i++)
                    {
                        if (selectedObjects[i] == null)
                        {
                            selectedObjects.RemoveAt(i);
                            i--;
                            continue;
                        }

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(30));
                        GUI.enabled = false;
                        EditorGUILayout.ObjectField(selectedObjects[i], typeof(Transform), true);
                        GUI.enabled = true;

                        if (GUILayout.Button("✕", GUILayout.Width(20)))
                        {
                            selectedObjects.RemoveAt(i);
                            i--;
                            SaveCache();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("清空选择"))
            {
                selectedObjects.Clear();
                showPreview = false;
                ResetPreview();
                SaveCache();
            }
        }
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            // 预览按钮
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = selectedObjects.Count > 0;

            if (GUILayout.Button(showPreview ? "隐藏预览" : "显示预览", GUILayout.Height(30)))
            {
                TogglePreview();
            }

            if (GUILayout.Button("应用布局", GUILayout.Height(30)))
            {
                ApplyLayout();
            }

            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;

            if (showPreview)
            {
                EditorGUILayout.HelpBox("预览模式已开启，点击'应用布局'以确认更改。", MessageType.Warning);
            }
        }
    }

    private void GetObjectsFromSelection()
    {
        selectedObjects.Clear();

        GameObject[] selection = Selection.gameObjects;
        foreach (var go in selection)
        {
            selectedObjects.Add(go.transform);
        }

        // 根据层级顺序排序
        selectedObjects = selectedObjects.OrderBy(t => t.GetSiblingIndex()).ToList();

        if (showPreview)
        {
            UpdatePreview();
        }

        SaveCache();
    }

    private void GetObjectsFromParent()
    {
        if (parentTransform == null)
        {
            EditorUtility.DisplayDialog("提示", "请先设置父对象！", "确定");
            return;
        }

        selectedObjects.Clear();

        // 只获取第一级子对象
        foreach (Transform child in parentTransform)
        {
            selectedObjects.Add(child);
        }

        if (showPreview)
        {
            UpdatePreview();
        }

        SaveCache();
    }

    private void TogglePreview()
    {
        showPreview = !showPreview;

        if (showPreview)
        {
            SaveOriginalPositions();
            UpdatePreview();
        }
        else
        {
            ResetPreview();
        }

        SceneView.RepaintAll();
    }

    private void SaveOriginalPositions()
    {
        originalPositions.Clear();
        foreach (var obj in selectedObjects)
        {
            if (obj != null)
            {
                originalPositions[obj] = obj.position;
            }
        }
    }

    private void UpdatePreview()
    {
        if (!showPreview || selectedObjects.Count == 0) return;

        previewPositions.Clear();
        Vector3[] positions = CalculatePositions();

        for (int i = 0; i < selectedObjects.Count && i < positions.Length; i++)
        {
            if (selectedObjects[i] != null)
            {
                previewPositions[selectedObjects[i]] = positions[i];

                // 如果有父物体且对象是其子物体，使用localPosition
                if (parentTransform != null && selectedObjects[i].parent == parentTransform)
                {
                    selectedObjects[i].localPosition = parentTransform.InverseTransformPoint(positions[i]);
                }
                else
                {
                    selectedObjects[i].position = positions[i];
                }
            }
        }

        SceneView.RepaintAll();
    }

    private void ResetPreview()
    {
        foreach (var kvp in originalPositions)
        {
            if (kvp.Key != null)
            {
                kvp.Key.position = kvp.Value;
            }
        }

        previewPositions.Clear();
        SceneView.RepaintAll();
    }

    private void ApplyLayout()
    {
        if (selectedObjects.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先选择游戏对象！", "确定");
            return;
        }

        // 记录操作用于撤销
        Undo.RecordObjects(selectedObjects.ToArray(), "Apply GameObject Layout");

        Vector3[] positions = CalculatePositions();

        for (int i = 0; i < selectedObjects.Count && i < positions.Length; i++)
        {
            if (selectedObjects[i] != null)
            {
                // 如果有父物体且对象是其子物体，使用localPosition
                if (parentTransform != null && selectedObjects[i].parent == parentTransform)
                {
                    selectedObjects[i].localPosition = parentTransform.InverseTransformPoint(positions[i]);
                }
                else
                {
                    selectedObjects[i].position = positions[i];
                }
            }
        }

        // 保存新位置
        SaveOriginalPositions();
        showPreview = false;
        SaveCache();

        EditorUtility.DisplayDialog("成功", "布局已应用！", "确定");
    }

    private void LoadCache()
    {
        string json = EditorPrefs.GetString(GetCacheKey(), string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        LayoutToolCache cache;
        try
        {
            cache = JsonUtility.FromJson<LayoutToolCache>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"GameObject布局工具缓存读取失败，已忽略旧缓存：{exception.Message}");
            return;
        }

        if (cache == null)
        {
            return;
        }

        parentTransform = LoadTransformFromGlobalId(cache.parentTransformGlobalId);
        layoutMode = cache.layoutMode;
        alignmentMode = cache.alignmentMode;
        spacing = cache.spacing;
        padding = cache.padding;
        columns = Mathf.Clamp(cache.columns, 1, 10);

        selectedObjects.Clear();
        if (cache.selectedObjectGlobalIds != null)
        {
            foreach (string globalId in cache.selectedObjectGlobalIds)
            {
                Transform transform = LoadTransformFromGlobalId(globalId);
                if (transform != null && !selectedObjects.Contains(transform))
                {
                    selectedObjects.Add(transform);
                }
            }
        }
    }

    private void SaveCache()
    {
        LayoutToolCache cache = new LayoutToolCache
        {
            parentTransformGlobalId = GetGlobalId(parentTransform),
            layoutMode = layoutMode,
            alignmentMode = alignmentMode,
            spacing = spacing,
            padding = padding,
            columns = columns,
            selectedObjectGlobalIds = selectedObjects
                .Where(transform => transform != null)
                .Select(GetGlobalId)
                .Where(globalId => !string.IsNullOrEmpty(globalId))
                .ToList()
        };

        EditorPrefs.SetString(GetCacheKey(), JsonUtility.ToJson(cache));
    }

    private string GetCacheKey()
    {
        return CacheKeyPrefix + Application.dataPath;
    }

    private string GetGlobalId(UnityEngine.Object target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
    }

    private Transform LoadTransformFromGlobalId(string globalIdText)
    {
        if (string.IsNullOrEmpty(globalIdText))
        {
            return null;
        }

        if (!GlobalObjectId.TryParse(globalIdText, out GlobalObjectId globalId))
        {
            return null;
        }

        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as Transform;
    }

    private Vector3[] CalculatePositions()
    {
        Vector3[] positions = new Vector3[selectedObjects.Count];

        // 确定起始位置，明确这是局部坐标
        Vector3 startPos = GetStartPosition();

        switch (layoutMode)
        {
            case LayoutMode.Horizontal:
                CalculateHorizontalPositions(positions, startPos);
                break;
            case LayoutMode.Vertical:
                CalculateVerticalPositions(positions, startPos);
                break;
            case LayoutMode.Grid:
                CalculateGridPositions(positions, startPos);
                break;
        }

        return positions;
    }

    private Vector3 GetStartPosition()
    {
        // 以父物体世界坐标为中心（无父物体时以世界原点为中心）
        Vector3 startPos = parentTransform != null ? parentTransform.position : Vector3.zero;

        // 添加边距（含Z轴）
        startPos.x += padding.x;
        startPos.y -= padding.y;
        startPos.z += padding.z;

        return startPos;
    }

    private void CalculateHorizontalPositions(Vector3[] positions, Vector3 startPos)
    {
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            positions[i] = new Vector3(
                startPos.x + i * spacing.x,
                startPos.y,
                startPos.z + i * spacing.z
            );
        }

        ApplyAlignment(positions, LayoutMode.Horizontal);
    }

    private void CalculateVerticalPositions(Vector3[] positions, Vector3 startPos)
    {
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            positions[i] = new Vector3(
                startPos.x,
                startPos.y - i * spacing.y,
                startPos.z + i * spacing.z
            );
        }

        ApplyAlignment(positions, LayoutMode.Vertical);
    }

    private void CalculateGridPositions(Vector3[] positions, Vector3 startPos)
    {
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            int row = i / columns;
            int col = i % columns;

            // 网格模式下Z轴按行递增
            positions[i] = new Vector3(
                startPos.x + col * spacing.x,
                startPos.y - row * spacing.y,
                startPos.z + row * spacing.z
            );
        }

        ApplyAlignment(positions, LayoutMode.Grid);
    }

    private void ApplyAlignment(Vector3[] positions, LayoutMode mode)
    {
        if (positions.Length == 0) return;

        // 计算边界
        Bounds bounds = CalculateBounds(positions);

        // 父物体世界坐标（无父物体时为世界原点）
        Vector3 center = parentTransform != null ? parentTransform.position : Vector3.zero;
        Vector3 offset = Vector3.zero;

        // X轴对齐
        switch (alignmentMode)
        {
            case AlignmentMode.TopLeft:
            case AlignmentMode.MiddleLeft:
            case AlignmentMode.BottomLeft:
                offset.x = center.x - bounds.min.x;
                break;

            case AlignmentMode.TopCenter:
            case AlignmentMode.MiddleCenter:
            case AlignmentMode.BottomCenter:
                offset.x = center.x - bounds.center.x;
                break;

            case AlignmentMode.TopRight:
            case AlignmentMode.MiddleRight:
            case AlignmentMode.BottomRight:
                offset.x = center.x - bounds.max.x;
                break;
        }

        // Y轴对齐
        switch (alignmentMode)
        {
            case AlignmentMode.TopLeft:
            case AlignmentMode.TopCenter:
            case AlignmentMode.TopRight:
                offset.y = center.y - bounds.max.y;
                break;

            case AlignmentMode.MiddleLeft:
            case AlignmentMode.MiddleCenter:
            case AlignmentMode.MiddleRight:
                offset.y = center.y - bounds.center.y;
                break;

            case AlignmentMode.BottomLeft:
            case AlignmentMode.BottomCenter:
            case AlignmentMode.BottomRight:
                offset.y = center.y - bounds.min.y;
                break;
        }

        // Z轴始终居中对齐于父物体
        offset.z = center.z - bounds.center.z;

        // 应用偏移（所有计算均在世界坐标系下进行）
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] += offset;
        }
    }

    private Bounds CalculateBounds(Vector3[] positions)
    {
        if (positions.Length == 0) return new Bounds();

        Bounds bounds = new Bounds(positions[0], Vector3.zero);

        foreach (var pos in positions)
        {
            bounds.Encapsulate(pos);
        }

        return bounds;
    }

    private void OnDisable()
    {
        if (showPreview)
        {
            ResetPreview();
        }

        SaveCache();
    }

    private void OnDestroy()
    {
        if (showPreview)
        {
            ResetPreview();
        }

        SaveCache();
    }
}
