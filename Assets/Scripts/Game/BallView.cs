using DG.Tweening;
using UnityEngine;

public class BallView : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("小球可视部分的根节点，用于选中时浮动和缩放。")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("小球主体 SpriteRenderer。")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [Tooltip("小球发光 SpriteRenderer，用于选中、移动和完成效果。")]
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private WaterTransferVisual waterTransferVisual;

    [Header("Selection Visual")]
    [Tooltip("选中或移动提示时发光的最高透明度。")]
    [SerializeField] private float glowAlpha = 0.85f;
    [Tooltip("发光淡入/淡出的动画时长。")]
    [SerializeField] private float glowFadeDuration = 0.18f;
    [Tooltip("发光呼吸缩放一次循环的时长。")]
    [SerializeField] private float glowPulseDuration = 0.8f;
    [Tooltip("发光呼吸动画的最大缩放倍率。")]
    [SerializeField] private float glowPulseScale = 1.05f;

    [Header("Landing Bounce")]
    [Tooltip("小球落到槽位后的回弹高度。")]
    [SerializeField] private float landingBounceHeight = 0.12f;
    [Tooltip("落地回弹向上阶段的时长。")]
    [SerializeField] private float landingBounceUpDuration = 0.08f;
    [Tooltip("落地回弹落回原位阶段的时长。")]
    [SerializeField] private float landingBounceDownDuration = 0.1f;

    private Vector3 defaultScale = Vector3.one;
    private Vector3 defaultLocalPosition = Vector3.zero;
    private Vector3 defaultGlowScale = Vector3.one;
    private Color defaultBodyColor = Color.white;
    private Vector2 defaultBodySize;
    private WaterBodyTopAnchor bodyTopAnchor;
    private Color defaultGlowColor = Color.white;

    public BallColorType ColorType { get; private set; }
    public WaterBodyTopAnchor BodyTopAnchor => bodyTopAnchor;

    public void SetSortingOrder(int bodyOrder)
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.sortingOrder = bodyOrder;
        }

        if (glowRenderer != null)
        {
            glowRenderer.sortingOrder = bodyOrder - 1;
        }

        if (waterTransferVisual != null)
        {
            waterTransferVisual.SyncSorting();
        }
    }

    public Tween AnimateSelectionPop(float scaleMultiplier, float liftOffset, float duration, Ease ease = Ease.OutBack)
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        visualRoot.DOKill();
        Sequence sequence = DOTween.Sequence();
        sequence.Join(visualRoot
            .DOScale(defaultScale * Mathf.Max(0.01f, scaleMultiplier), duration)
            .SetEase(ease));
        sequence.Join(visualRoot
            .DOLocalMove(defaultLocalPosition + Vector3.up * liftOffset, duration)
            .SetEase(ease));
        return sequence;
    }

    public Tween AnimateSelectionPopReset(float duration, Ease ease = Ease.InQuad)
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        visualRoot.DOKill();
        Sequence sequence = DOTween.Sequence();
        sequence.Join(visualRoot.DOScale(defaultScale, duration).SetEase(ease));
        sequence.Join(visualRoot.DOLocalMove(defaultLocalPosition, duration).SetEase(ease));
        return sequence;
    }

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        defaultScale = visualRoot.localScale;
        defaultLocalPosition = visualRoot.localPosition;

        if (bodyRenderer != null)
        {
            defaultBodyColor = bodyRenderer.color;
            defaultBodySize = bodyRenderer.size;
            bodyTopAnchor = bodyRenderer.GetComponent<WaterBodyTopAnchor>();
        }

        if (glowRenderer != null)
        {
            defaultGlowScale = glowRenderer.transform.localScale;
            defaultGlowColor = glowRenderer.color;
            glowRenderer.gameObject.SetActive(false);
        }
    }

    public void Initialize(BallColorType colorType, BallVisualPalette palette)
    {
        ColorType = colorType;

        if (palette != null && palette.TryGetVisual(colorType, out BallVisualPalette.BallVisualEntry entry))
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.sprite = entry.sprite != null ? entry.sprite : bodyRenderer.sprite;
                bodyRenderer.color = entry.tint;
                defaultBodyColor = bodyRenderer.color;
            }

            if (glowRenderer != null)
            {
                Color glowColor = entry.tint;
                glowColor.a = 0f;
                glowRenderer.color = glowColor;
                defaultGlowColor = glowRenderer.color;
            }
        }
    }

    public void SnapTo(Vector3 worldPosition)
    {
        KillTweens();
        transform.position = worldPosition;
    }

    public void KillTweens()
    {
        transform.DOKill();

        if (bodyRenderer != null)
        {
            bodyRenderer.size = defaultBodySize;
        }

        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = defaultScale;
            visualRoot.localPosition = defaultLocalPosition;
            visualRoot.gameObject.SetActive(true);
        }

        if (glowRenderer != null)
        {
            glowRenderer.DOKill();
            glowRenderer.transform.DOKill();
            glowRenderer.transform.localScale = defaultGlowScale;
            Color glowColor = glowRenderer.color;
            glowColor.a = 0f;
            glowRenderer.color = glowColor;
            glowRenderer.gameObject.SetActive(false);
        }

        if (waterTransferVisual != null)
        {
            waterTransferVisual.RestoreBody();
        }
    }

    public void BeginLiftVisual()
    {
        KillTweens();
        if (waterTransferVisual != null)
        {
            waterTransferVisual.PlayUp();
        }
        else
        {
            SetMoveGlow(true);
        }
    }

    public void HideVisuals()
    {
        KillTweens();
        if (waterTransferVisual != null)
        {
            waterTransferVisual.HideVisuals();
        }
        else
        {
            visualRoot.gameObject.SetActive(false);
        }
    }

    private float BottomOverlapWorld => bodyTopAnchor != null
        ? bodyTopAnchor.BottomOverlapWorld : 0f;

    public Vector3 BodyBottomWorldOffset
    {
        get
        {
            Bounds bounds = WaterBodyTopAnchor.GetLocalSpriteBounds(bodyRenderer);
            return bodyRenderer.transform.TransformPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)) + bodyRenderer.transform.up * BottomOverlapWorld - transform.position;
        }
    }

    public void SetHeightUnitWorld(float worldHeightPerUnit)
    {
        Vector3 scale = bodyRenderer.transform.localScale;
        scale.y = worldHeightPerUnit / Mathf.Abs(bodyRenderer.transform.parent.lossyScale.y);
        bodyRenderer.transform.localScale = scale;
    }

    public void ShowWaterColumn(float height)
    {
        ResetVisuals();
        SetWaterColumnHeight(height);
    }

    public void SetWaterColumnHeight(float height)
    {
        Vector2 size = defaultBodySize;
        // Keep effective height unchanged while covering the lowered section of the column below.
        size.y = height + BottomOverlapWorld / Mathf.Abs(bodyRenderer.transform.lossyScale.y);
        bodyRenderer.size = size;
        if (bodyTopAnchor != null) bodyTopAnchor.SetRevealProgress(1f);
    }

    public Sequence CreateDropSequence(Vector3 slotPosition, float duration)
    {
        transform.DOKill();
        Sequence sequence = DOTween.Sequence();
        if (waterTransferVisual != null)
        {
            sequence.AppendCallback(waterTransferVisual.PlayDown);
        }
        sequence.Append(transform.DOMove(slotPosition, duration).SetEase(Ease.InQuad));
        if (waterTransferVisual != null)
        {
            sequence.Join(waterTransferVisual.CreateDownProgressTween(duration));
            sequence.Append(waterTransferVisual.CreateRevealSequence());
        }
        else
        {
            sequence.AppendCallback(() => visualRoot.gameObject.SetActive(true));
        }
        return sequence;
    }

    public void SetMoveGlow(bool enabled)
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        visualRoot.DOKill();
        visualRoot.localScale = defaultScale;
        visualRoot.localPosition = defaultLocalPosition;

        if (glowRenderer == null)
        {
            return;
        }

        glowRenderer.DOKill();
        glowRenderer.transform.DOKill();
        glowRenderer.transform.localScale = defaultGlowScale;

        if (enabled)
        {
            glowRenderer.gameObject.SetActive(true);

            Color baseGlowColor = defaultGlowColor;
            baseGlowColor.a = 0f;
            glowRenderer.color = baseGlowColor;

            glowRenderer
                .DOFade(glowAlpha, glowFadeDuration)
                .SetEase(Ease.OutQuad);

            glowRenderer.transform
                .DOScale(defaultGlowScale * glowPulseScale, glowPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            Color glowColor = glowRenderer.color;
            glowColor.a = 0f;
            glowRenderer.color = glowColor;
            glowRenderer.transform.localScale = defaultGlowScale;
            glowRenderer.gameObject.SetActive(false);
        }
    }

    public Tween FadeOutSelectedVisual()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        visualRoot.DOKill();
        visualRoot.localScale = defaultScale;
        visualRoot.localPosition = defaultLocalPosition;

        if (glowRenderer == null)
        {
            return null;
        }

        glowRenderer.DOKill();
        glowRenderer.transform.DOKill();
        glowRenderer.transform.localScale = defaultGlowScale;

        if (!glowRenderer.gameObject.activeSelf)
        {
            Color hiddenGlowColor = glowRenderer.color;
            hiddenGlowColor.a = 0f;
            glowRenderer.color = hiddenGlowColor;
            return null;
        }

        Tween fadeTween = glowRenderer
            .DOFade(0f, glowFadeDuration)
            .SetEase(Ease.OutQuad);

        fadeTween.OnComplete(() =>
        {
            if (glowRenderer != null)
            {
                Color glowColor = glowRenderer.color;
                glowColor.a = 0f;
                glowRenderer.color = glowColor;
                glowRenderer.transform.localScale = defaultGlowScale;
                glowRenderer.gameObject.SetActive(false);
            }
        });

        return fadeTween;
    }

    public Tween AnimateTo(Vector3 worldPosition, float duration, Ease ease = Ease.OutQuad)
    {
        transform.DOKill();
        return transform.DOMove(worldPosition, duration).SetEase(ease);
    }

    public Tween AnimateToWithLandingBounce(
        Vector3 worldPosition,
        float duration,
        Ease ease = Ease.OutQuad)
    {
        transform.DOKill();
        Sequence sequence = DOTween.Sequence();
        sequence.Append(transform.DOMove(worldPosition, duration).SetEase(ease));
        AppendLandingBounce(sequence, worldPosition);
        return sequence;
    }

    public Tween AnimateToWithSfx(
        Vector3 worldPosition,
        float duration,
        string sfxName,
        Ease ease = Ease.OutQuad)
    {
        transform.DOKill();
        Sequence sequence = DOTween.Sequence();
        Tween moveTween = transform.DOMove(worldPosition, duration).SetEase(ease);
        moveTween.OnComplete(() =>
        {
            if (!string.IsNullOrEmpty(sfxName) && AudioMgr.Instance != null)
            {
                AudioMgr.Instance.Play(sfxName);
            }
        });

        sequence.Append(moveTween);
        AppendLandingBounce(sequence, worldPosition);
        return sequence;
    }

    public Tween AnimateToWithSfxNoBounce(
        Vector3 worldPosition,
        float duration,
        string sfxName,
        Ease ease = Ease.OutQuad)
    {
        transform.DOKill();
        Tween tween = transform.DOMove(worldPosition, duration).SetEase(ease);
        tween.OnComplete(() =>
        {
            if (!string.IsNullOrEmpty(sfxName) && AudioMgr.Instance != null)
            {
                AudioMgr.Instance.Play(sfxName);
            }
        });
        return tween;
    }

    public Tween AnimateToNoBounce(
        Vector3 worldPosition,
        float duration,
        Ease ease = Ease.OutQuad)
    {
        transform.DOKill();
        return transform.DOMove(worldPosition, duration).SetEase(ease);
    }

    public Sequence PlayDisappear(float duration)
    {
        KillTweens();

        Sequence sequence = DOTween.Sequence();

        if (bodyRenderer != null)
        {
            bodyRenderer.DOKill();
            sequence.Join(bodyRenderer.DOFade(0f, duration).SetEase(Ease.OutQuad));
        }

        if (glowRenderer != null)
        {
            glowRenderer.DOKill();
            glowRenderer.transform.DOKill();
            glowRenderer.gameObject.SetActive(true);
            sequence.Join(glowRenderer.DOFade(0f, duration).SetEase(Ease.OutQuad));
        }

        sequence.OnComplete(() => gameObject.SetActive(false));
        return sequence;
    }

    public Sequence PlayCompletionDisappear(float duration)
    {
        KillTweens();

        Sequence sequence = DOTween.Sequence();
        float glowRampDuration = Mathf.Min(duration * 0.4f, 0.12f);

        if (glowRenderer != null)
        {
            glowRenderer.DOKill();
            glowRenderer.transform.DOKill();
            glowRenderer.transform.localScale = defaultGlowScale;
            glowRenderer.gameObject.SetActive(true);

            Color glowColor = defaultGlowColor;
            glowColor.a = 0f;
            glowRenderer.color = glowColor;

            sequence.Append(glowRenderer.DOFade(glowAlpha, glowRampDuration).SetEase(Ease.OutQuad));
            sequence.Join(
                glowRenderer.transform
                    .DOScale(defaultGlowScale * glowPulseScale, glowRampDuration)
                    .SetEase(Ease.OutQuad));
            sequence.Append(glowRenderer.DOFade(0f, duration).SetEase(Ease.OutQuad));
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.DOKill();
            sequence.Insert(glowRampDuration, bodyRenderer.DOFade(0f, duration).SetEase(Ease.OutQuad));
        }

        sequence.OnComplete(() => gameObject.SetActive(false));
        return sequence;
    }

    public void ResetVisuals()
    {
        KillTweens();

        gameObject.SetActive(true);

        if (bodyRenderer != null)
        {
            bodyRenderer.color = defaultBodyColor;
        }

        if (glowRenderer != null)
        {
            glowRenderer.transform.localScale = defaultGlowScale;
            glowRenderer.color = defaultGlowColor;
            glowRenderer.gameObject.SetActive(false);
        }
    }

    public Sequence CreateTransferSequence(
        Vector3 raiseToPosition,
        Vector3 targetHoverPosition,
        Vector3 targetSlotPosition,
        float raiseDuration,
        float travelDuration,
        float dropDuration,
        float arcHeight)
    {
        transform.DOKill();
        Sequence sequence = DOTween.Sequence();
        float clampedArcHeight = Mathf.Max(0f, arcHeight);
        float exitPathDuration = Mathf.Max(0f, raiseDuration) + Mathf.Max(0f, travelDuration);
        if (exitPathDuration > 0f)
        {
            Vector3[] travelPath = CreateExitArcPath(transform.position, raiseToPosition, targetHoverPosition, clampedArcHeight);
            sequence.Append(transform.DOPath(travelPath, exitPathDuration, PathType.Linear).SetEase(Ease.Linear));
        }
        else
        {
            transform.position = targetHoverPosition;
        }

        if (waterTransferVisual != null)
        {
            sequence.AppendCallback(waterTransferVisual.PlayDown);
        }
        sequence.AppendCallback(() =>
        {
            if (AudioMgr.Instance != null)
            {
                AudioMgr.Instance.Play("下落");
            }
        });
        Tween dropTween = transform.DOMove(targetSlotPosition, dropDuration).SetEase(Ease.InQuad);
        sequence.Append(dropTween);
        if (waterTransferVisual != null)
        {
            sequence.Join(waterTransferVisual.CreateDownProgressTween(dropDuration));
            sequence.Append(waterTransferVisual.CreateRevealSequence());
        }
        else
        {
            AppendLandingBounce(sequence, targetSlotPosition);
        }
        return sequence;
    }

    private static Vector3[] CreateExitArcPath(
        Vector3 startPosition,
        Vector3 raiseToPosition,
        Vector3 targetHoverPosition,
        float arcHeight)
    {
        float verticalRiseDistance = Mathf.Max(0f, raiseToPosition.y - startPosition.y);
        float blendHeight = Mathf.Min(0.16f, verticalRiseDistance * 0.45f);
        Vector3 curveStart = raiseToPosition - Vector3.up * blendHeight;
        curveStart.x = startPosition.x;
        curveStart.z = startPosition.z;

        const int arcSegmentCount = 18;
        bool needsStraightExit = Vector3.Distance(startPosition, curveStart) > 0.01f;
        Vector3[] path = new Vector3[arcSegmentCount + (needsStraightExit ? 1 : 0)];
        int pathIndex = 0;
        if (needsStraightExit)
        {
            path[pathIndex] = curveStart;
            pathIndex++;
        }

        float startControlHeight = Mathf.Max(0.06f, blendHeight + arcHeight * 0.25f);
        Vector3 startControlPoint = curveStart + Vector3.up * startControlHeight;
        Vector3 endControlPoint = targetHoverPosition + Vector3.up * arcHeight;

        for (int i = 0; i < arcSegmentCount; i++)
        {
            float t = (float)(i + 1) / arcSegmentCount;
            path[pathIndex + i] = EvaluateCubicBezier(
                curveStart,
                startControlPoint,
                endControlPoint,
                targetHoverPosition,
                t);
        }

        return path;
    }

    private static Vector3 EvaluateCubicBezier(
        Vector3 start,
        Vector3 startControl,
        Vector3 endControl,
        Vector3 end,
        float t)
    {
        float inverseT = 1f - t;
        return inverseT * inverseT * inverseT * start
               + 3f * inverseT * inverseT * t * startControl
               + 3f * inverseT * t * t * endControl
               + t * t * t * end;
    }

    private void AppendLandingBounce(Sequence sequence, Vector3 landingPosition)
    {
        if (sequence == null || landingBounceHeight <= 0f)
        {
            return;
        }

        float bounceUpDuration = Mathf.Max(0f, landingBounceUpDuration);
        float bounceDownDuration = Mathf.Max(0f, landingBounceDownDuration);
        if (bounceUpDuration <= 0f || bounceDownDuration <= 0f)
        {
            return;
        }

        Vector3 bouncePeakPosition = landingPosition + Vector3.up * landingBounceHeight;
        sequence.Append(transform.DOMove(bouncePeakPosition, bounceUpDuration).SetEase(Ease.OutQuad));
        sequence.Append(transform.DOMove(landingPosition, bounceDownDuration).SetEase(Ease.InQuad));
    }
}
