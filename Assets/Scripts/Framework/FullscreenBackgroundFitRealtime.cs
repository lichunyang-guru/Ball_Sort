using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FullscreenBackgroundFitRealtime : MonoBehaviour
{
    [Header("通用设置")]
    [SerializeField] private bool checkEveryFrame = true;

    [Header("SpriteRenderer 设置")]
    [Tooltip("留空时自动使用 Main Camera")]
    [SerializeField] private Camera targetCamera;

    private readonly WaitForSeconds checkInterval = new WaitForSeconds(0.1f);

    private Image image;
    private RectTransform selfRectTransform;
    private RectTransform rootCanvasRectTransform;

    private SpriteRenderer spriteRenderer;
    private Camera activeCamera;

    private Vector2 lastTargetSize;
    private Sprite lastSprite;

    private void Awake()
    {
        image = GetComponent<Image>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (image != null)
        {
            InitializeImage();
            return;
        }

        if (spriteRenderer != null)
        {
            ResolveCamera();
            return;
        }

        Debug.LogWarning("FullscreenBackgroundFitRealtime 需要 Image 或 SpriteRenderer 组件。", this);
    }

    private void OnEnable()
    {
        if (image == null && spriteRenderer != null)
            ResolveCamera();

        StartCoroutine(WatchAndFit());
        ApplyFit(true);
    }

    private void InitializeImage()
    {
        selfRectTransform = GetComponent<RectTransform>();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            // 使用根 Canvas 的 UI 空间作为覆盖区域。
            Canvas rootCanvas = canvas.rootCanvas;
            rootCanvasRectTransform = rootCanvas != null
                ? rootCanvas.GetComponent<RectTransform>()
                : canvas.GetComponent<RectTransform>();
        }

        image.preserveAspect = true;

        // 固定中心锚点，使超出屏幕的部分向四周均匀裁切。
        selfRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        selfRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        selfRectTransform.pivot = new Vector2(0.5f, 0.5f);
        selfRectTransform.anchoredPosition = Vector2.zero;
    }

    private void ResolveCamera()
    {
        activeCamera = targetCamera != null ? targetCamera : Camera.main;
    }

    private IEnumerator WatchAndFit()
    {
        while (enabled && gameObject.activeInHierarchy)
        {
            ApplyFit();
            yield return checkEveryFrame ? null : checkInterval;
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        // RectTransform 回调可能早于 Awake，此处需要先确认初始化已完成。
        if (image != null && selfRectTransform != null)
            ApplyFit();
    }

    private void ApplyFit(bool force = false)
    {
        // 同一对象同时存在两个组件时优先处理 UI Image，避免重复控制 Transform。
        if (image != null)
        {
            ApplyImageFit(force);
            return;
        }

        if (spriteRenderer != null)
            ApplySpriteRendererFit(force);
    }

    private void ApplyImageFit(bool force)
    {
        Sprite sprite = image.sprite;
        if (sprite == null)
            return;

        Vector2 targetSize = GetImageTargetSize();
        if (targetSize.x <= 0f || targetSize.y <= 0f)
            return;

        if (!force && lastTargetSize == targetSize && lastSprite == sprite)
            return;

        Vector2 fittedSize = CalculateCoverSize(targetSize, sprite.rect.size);
        if (fittedSize.x <= 0f || fittedSize.y <= 0f)
            return;

        lastTargetSize = targetSize;
        lastSprite = sprite;

        selfRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fittedSize.x);
        selfRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fittedSize.y);
        selfRectTransform.anchoredPosition = Vector2.zero;
    }

    private Vector2 GetImageTargetSize()
    {
        if (rootCanvasRectTransform != null)
        {
            Vector2 canvasSize = rootCanvasRectTransform.rect.size;
            if (canvasSize.x > 0f && canvasSize.y > 0f)
                return canvasSize;
        }

        return new Vector2(Screen.width, Screen.height);
    }

    private void ApplySpriteRendererFit(bool force)
    {
        Sprite sprite = spriteRenderer.sprite;
        if (sprite == null)
            return;

        if (targetCamera != null && activeCamera != targetCamera)
            activeCamera = targetCamera;
        else if (activeCamera == null)
            ResolveCamera();

        if (activeCamera == null)
            return;

        Transform selfTransform = spriteRenderer.transform;
        float distance = Vector3.Dot(
            selfTransform.position - activeCamera.transform.position,
            activeCamera.transform.forward);

        if (distance <= 0f)
            return;

        Vector3 bottomLeft = activeCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 topRight = activeCamera.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
        Vector3 center = activeCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));

        Vector2 targetSize = new Vector2(
            Vector3.Project(topRight - bottomLeft, activeCamera.transform.right).magnitude,
            Vector3.Project(topRight - bottomLeft, activeCamera.transform.up).magnitude);

        Vector3 spriteBoundsSize = sprite.bounds.size;
        Vector2 spriteSize = spriteRenderer.drawMode == SpriteDrawMode.Simple
            ? new Vector2(spriteBoundsSize.x, spriteBoundsSize.y)
            : spriteRenderer.size;

        if (targetSize.x <= 0f || targetSize.y <= 0f || spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        Transform parent = selfTransform.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        float parentScaleX = Mathf.Abs(parentScale.x);
        float parentScaleY = Mathf.Abs(parentScale.y);
        if (parentScaleX <= Mathf.Epsilon || parentScaleY <= Mathf.Epsilon)
            return;

        float coverScale = Mathf.Max(targetSize.x / spriteSize.x, targetSize.y / spriteSize.y);
        Vector3 currentScale = selfTransform.localScale;
        Vector3 fittedScale = new Vector3(
            coverScale / parentScaleX,
            coverScale / parentScaleY,
            currentScale.z);

        // 保留背景所在深度，只移动到当前相机视口中心。
        bool positionChanged = (selfTransform.position - center).sqrMagnitude > 0.000001f;
        bool scaleChanged = (currentScale - fittedScale).sqrMagnitude > 0.000001f;
        bool sourceChanged = force || lastTargetSize != targetSize || lastSprite != sprite;

        if (positionChanged)
            selfTransform.position = center;

        if (sourceChanged || scaleChanged)
            selfTransform.localScale = fittedScale;

        lastTargetSize = targetSize;
        lastSprite = sprite;
    }

    private static Vector2 CalculateCoverSize(Vector2 targetSize, Vector2 sourceSize)
    {
        if (targetSize.x <= 0f || targetSize.y <= 0f || sourceSize.x <= 0f || sourceSize.y <= 0f)
            return Vector2.zero;

        float coverScale = Mathf.Max(targetSize.x / sourceSize.x, targetSize.y / sourceSize.y);
        return sourceSize * coverScale;
    }
}
