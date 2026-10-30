using UnityEngine;

/// <summary>
/// UI边距自适应缩放组件
/// 当UI元素与屏幕边界的宽度或高度间距不足时，自动等比缩放以维持最小边距
/// 实时检测，支持配置检测维度（宽度/高度/全选）和缩放限制
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
[ExecuteInEditMode]
public class UIEdgeAdaptiveScaler : MonoBehaviour
{
    /// <summary>
    /// 检测模式：仅宽度、仅高度、或同时检测
    /// </summary>
    public enum CheckMode
    {
        Width,
        Height,
        Both
    }

    [Header("边距配置（UI单位）")]
    [Tooltip("UI元素离最近的左右屏幕边界需要保持的最小边距")]
    [SerializeField] private float minEdgeWidth = 50f;
    [Tooltip("UI元素离最近的上下屏幕边界需要保持的最小边距")]
    [SerializeField] private float minEdgeHeight = 50f;

    [Header("检测模式")]
    [SerializeField] private CheckMode checkMode = CheckMode.Both;

    [Header("缩放限制")]
    [SerializeField][Range(0.1f, 3f)] private float minScaleLimit = 0.5f;
    [SerializeField][Range(0.1f, 3f)] private float maxScaleLimit = 1.5f;

    [Header("编辑器预览")]
    [Tooltip("在Scene视图中显示边距辅助线")]
    [SerializeField] private bool showEdgeGizmos = true;

    private RectTransform selfRT;
    private RectTransform rootCanvasRT;
    [SerializeField][HideInInspector] private Vector3 originalScale = Vector3.one;
    [SerializeField][HideInInspector] private Vector2 originalSize;
    private Vector2 lastCanvasSize;
    private float currentScaleFactor = 1f;
    private bool initialized;

    /// <summary>
    /// 供编辑器Inspector显示当前缩放因子（只读）
    /// </summary>
    public float CurrentScaleFactor => currentScaleFactor;
    public RectTransform SelfRT => selfRT;
    public RectTransform RootCanvasRT => rootCanvasRT;
    public float MinEdgeWidth => minEdgeWidth;
    public float MinEdgeHeight => minEdgeHeight;
    public Vector2 OriginalSize => originalSize;
    public CheckMode CurrentCheckMode => checkMode;
    public bool ShowEdgeGizmos => showEdgeGizmos;

    /// <summary>
    /// 供编辑器调用，获取UI四边到Canvas边界的距离
    /// </summary>
    public void GetEdgeDistances(out float left, out float right, out float bottom, out float top)
    {
        if (selfRT == null || rootCanvasRT == null)
        {
            left = right = bottom = top = 0f;
            return;
        }
        CalcEdgeDistances(GetCanvasSize(), out left, out right, out bottom, out top);
    }

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (initialized) return;

        selfRT = GetComponent<RectTransform>();

        // originalScale/originalSize 通过序列化持久保存，避免被自身缩放污染
        // 仅在首次（值为零/默认）时记录，或通过右键菜单手动重置
        if (originalSize == Vector2.zero || originalScale == Vector3.zero)
        {
            CaptureOriginal();
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Canvas root = canvas.rootCanvas;
            rootCanvasRT = root != null ? root.GetComponent<RectTransform>() : canvas.GetComponent<RectTransform>();
        }

        initialized = true;
    }

    /// <summary>
    /// 记录当前localScale和rect.size作为原始基准值
    /// </summary>
    private void CaptureOriginal()
    {
        if (selfRT == null) selfRT = GetComponent<RectTransform>();
        originalScale = selfRT.localScale;
        if (originalScale.x == 0f || originalScale.y == 0f || originalScale.z == 0f)
            originalScale = Vector3.one;
        originalSize = selfRT.rect.size;
    }

    /// <summary>
    /// 右键菜单：以当前localScale和size重新记录原始基准值
    /// </summary>
    [ContextMenu("重新捕获原始Scale和Size")]
    public void RecaptureOriginal()
    {
        CaptureOriginal();
        currentScaleFactor = 1f;
        lastCanvasSize = Vector2.zero;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        ApplyScale(true);
    }

    private void OnEnable()
    {
        Init();
        ApplyScale(true);
    }

    private void Update()
    {
        Vector2 canvasSize = GetCanvasSize();
        if (canvasSize != lastCanvasSize)
        {
            ApplyScale(false);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!initialized) return;
        ApplyScale(false);
    }

    /// <summary>
    /// 获取Canvas的UI空间尺寸
    /// </summary>
    public Vector2 GetCanvasSize()
    {
        if (rootCanvasRT != null)
        {
            Vector2 size = rootCanvasRT.rect.size;
            if (size.x > 0f && size.y > 0f)
                return size;
        }
        return new Vector2(Screen.width, Screen.height);
    }

    /// <summary>
    /// 通过GetWorldCorners获取UI实际四边到Canvas边界的距离（UI单位）
    /// 自动处理anchor、pivot、position等所有情况
    /// </summary>
    private readonly Vector3[] uiCornersCache = new Vector3[4];
    private readonly Vector3[] canvasCornersCache = new Vector3[4];

    private void CalcEdgeDistances(Vector2 canvasSize, out float left, out float right, out float bottom, out float top)
    {
        // GetWorldCorners: 0=左下, 1=左上, 2=右上, 3=右下
        selfRT.GetWorldCorners(uiCornersCache);
        rootCanvasRT.GetWorldCorners(canvasCornersCache);

        // 世界空间到UI单位的转换比
        float worldToUIX = canvasSize.x / (canvasCornersCache[2].x - canvasCornersCache[0].x);
        float worldToUIY = canvasSize.y / (canvasCornersCache[2].y - canvasCornersCache[0].y);

        // 用世界坐标差值转换为UI单位距离
        left = (uiCornersCache[0].x - canvasCornersCache[0].x) * worldToUIX;
        right = (canvasCornersCache[2].x - uiCornersCache[2].x) * worldToUIX;
        bottom = (uiCornersCache[0].y - canvasCornersCache[0].y) * worldToUIY;
        top = (canvasCornersCache[2].y - uiCornersCache[2].y) * worldToUIY;
    }

    /// <summary>
    /// 计算并应用缩放
    /// </summary>
    private void ApplyScale(bool force)
    {
        Vector2 canvasSize = GetCanvasSize();
        if (canvasSize.x <= 0f || canvasSize.y <= 0f) return;

        if (!force && lastCanvasSize == canvasSize) return;
        lastCanvasSize = canvasSize;

        float uiWidth = originalSize.x;
        float uiHeight = originalSize.y;
        if (uiWidth <= 0f || uiHeight <= 0f) return;

        // 先恢复原始缩放，确保GetWorldCorners返回的是未缩放的边界
        Vector3 currentScale = selfRT.localScale;
        selfRT.localScale = originalScale;

        CalcEdgeDistances(canvasSize, out float left, out float right, out float bottom, out float top);

        // 恢复当前缩放（后面会重新设置）
        selfRT.localScale = currentScale;

        float scaleByWidth = 1f;
        float scaleByHeight = 1f;

        // 宽度方向：基于pivot计算每侧允许的最大缩放因子
        // 缩放以pivot为锚点：左侧扩展量 = uiWidth * (s-1) * pivot.x，右侧 = uiWidth * (s-1) * (1-pivot.x)
        // 边距不足时需要缩小，边距充裕时可以放大
        if (checkMode == CheckMode.Width || checkMode == CheckMode.Both)
        {
            float pivotX = selfRT.pivot.x;
            // 左侧：缩放后左边距 = left - uiWidth * (s-1) * pivotX = minEdgeWidth → s = 1 + (left - minEdgeWidth) / (uiWidth * pivotX)
            float sLeft = (pivotX > 0f)
                ? 1f + (left - minEdgeWidth) / (uiWidth * pivotX)
                : float.MaxValue;
            // 右侧：缩放后右边距 = right - uiWidth * (s-1) * (1-pivotX) = minEdgeWidth → s = 1 + (right - minEdgeWidth) / (uiWidth * (1-pivotX))
            float sRight = ((1f - pivotX) > 0f)
                ? 1f + (right - minEdgeWidth) / (uiWidth * (1f - pivotX))
                : float.MaxValue;
            scaleByWidth = Mathf.Min(sLeft, sRight);
        }

        // 高度方向：同理
        if (checkMode == CheckMode.Height || checkMode == CheckMode.Both)
        {
            float pivotY = selfRT.pivot.y;
            float sBottom = (pivotY > 0f)
                ? 1f + (bottom - minEdgeHeight) / (uiHeight * pivotY)
                : float.MaxValue;
            float sTop = ((1f - pivotY) > 0f)
                ? 1f + (top - minEdgeHeight) / (uiHeight * (1f - pivotY))
                : float.MaxValue;
            scaleByHeight = Mathf.Min(sBottom, sTop);
        }

        float scaleFactor;
        switch (checkMode)
        {
            case CheckMode.Width:
                scaleFactor = scaleByWidth;
                break;
            case CheckMode.Height:
                scaleFactor = scaleByHeight;
                break;
            default:
                scaleFactor = Mathf.Min(scaleByWidth, scaleByHeight);
                break;
        }

        // 应用缩放限制
        scaleFactor = Mathf.Clamp(scaleFactor, minScaleLimit, maxScaleLimit);

        currentScaleFactor = scaleFactor;
        selfRT.localScale = originalScale * currentScaleFactor;
    }

    /// <summary>
    /// 获取当前缩放因子
    /// </summary>
    public float GetCurrentScaleFactor() => currentScaleFactor;

    /// <summary>
    /// 运行时动态设置边距
    /// </summary>
    public void SetEdgeMargins(float width, float height)
    {
        minEdgeWidth = width;
        minEdgeHeight = height;
        ApplyScale(true);
    }

    /// <summary>
    /// 运行时动态设置检测模式
    /// </summary>
    public void SetCheckMode(CheckMode mode)
    {
        checkMode = mode;
        ApplyScale(true);
    }

    /// <summary>
    /// 运行时动态设置缩放限制
    /// </summary>
    public void SetScaleLimits(float min, float max)
    {
        minScaleLimit = Mathf.Clamp(min, 0.1f, 3f);
        maxScaleLimit = Mathf.Clamp(max, 0.1f, 3f);
        if (minScaleLimit > maxScaleLimit)
        {
            float temp = minScaleLimit;
            minScaleLimit = maxScaleLimit;
            maxScaleLimit = temp;
        }
        ApplyScale(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (minScaleLimit > maxScaleLimit)
        {
            float temp = minScaleLimit;
            minScaleLimit = maxScaleLimit;
            maxScaleLimit = temp;
        }

        // 编辑器下参数修改时立即刷新缩放
        if (initialized && selfRT != null)
        {
            ApplyScale(true);
        }
    }
#endif
}