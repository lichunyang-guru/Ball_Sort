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

    [Header("Selection Visual")]
    [Tooltip("选中或移动提示时发光的最高透明度。")]
    [SerializeField] private float glowAlpha = 0.85f;
    [Tooltip("发光淡入/淡出的动画时长。")]
    [SerializeField] private float glowFadeDuration = 0.18f;

    [Tooltip("发光呼吸动画的最大缩放倍率。")]
    [SerializeField] private float glowPulseScale = 1.05f;

    [Header("Landing Bounce")]
    [Tooltip("小球落到槽位后的回弹高度。")]
    [SerializeField] private float landingBounceHeight = 0.12f;
    [SerializeField] private float landingBounceHeightMultiplier = 0.35f;
    [SerializeField] private float selectionBounceHeightMultiplier = 0.25f;
    [Tooltip("落地回弹向上阶段的时长。")]
    [SerializeField] private float landingBounceUpDuration = 0.08f;
    [Tooltip("落地回弹落回原位阶段的时长。")]
    [SerializeField] private float landingBounceDownDuration = 0.1f;

    private Vector3 defaultScale = Vector3.one;
    private Vector3 defaultLocalPosition = Vector3.zero;
    private Vector3 defaultBodyLocalScale = Vector3.one;
    private Vector3 defaultGlowScale = Vector3.one;
    private Color defaultBodyColor = Color.white;
    private Color defaultGlowColor = Color.white;
    private bool movingSortingApplied;
    private int originalBodySortingLayer;
    private int originalGlowSortingLayer;

    public BallColorType ColorType { get; private set; }

    public void SetSortingOrder(int bodyOrder)
    {
        if (movingSortingApplied)
        {
            if (bodyRenderer != null) bodyRenderer.sortingLayerID = originalBodySortingLayer;
            if (glowRenderer != null) glowRenderer.sortingLayerID = originalGlowSortingLayer;
            movingSortingApplied = false;
        }
        if (bodyRenderer != null)
        {
            bodyRenderer.sortingOrder = bodyOrder;
        }

        if (glowRenderer != null)
        {
            glowRenderer.sortingOrder = bodyOrder - 1;
        }
    }

    public void SetMovingSorting()
    {
        if (!movingSortingApplied)
        {
            if (bodyRenderer != null) originalBodySortingLayer = bodyRenderer.sortingLayerID;
            if (glowRenderer != null) originalGlowSortingLayer = glowRenderer.sortingLayerID;
            movingSortingApplied = true;
        }

        // The tube layer is above the ball layer; moving balls must also clear candles and fire.
        int foregroundLayer = SortingLayer.NameToID("tube");
        if (bodyRenderer != null)
        {
            bodyRenderer.sortingLayerID = foregroundLayer;
            bodyRenderer.sortingOrder = 32767;
        }
        if (glowRenderer != null)
        {
            glowRenderer.sortingLayerID = foregroundLayer;
            glowRenderer.sortingOrder = 32766;
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
            defaultBodyLocalScale = bodyRenderer.transform.localScale;
        }

        if (glowRenderer != null)
        {
            defaultGlowScale = glowRenderer.transform.localScale;
            defaultGlowColor = glowRenderer.color;
            glowRenderer.gameObject.SetActive(false);
        }
    }

    public void Initialize(BallColorType colorType, BallVisualPalette palette, Sprite spriteOverride = null)
    {
        ColorType = colorType;
        BallVisualPalette.BallVisualEntry entry = null;
        if (palette != null) palette.TryGetVisual(colorType, out entry);
        Sprite reference = entry != null ? entry.sprite : null;
        if (bodyRenderer != null && (spriteOverride != null || entry != null))
        {
            if (spriteOverride != null) bodyRenderer.sprite = spriteOverride;
            else if (reference != null) bodyRenderer.sprite = reference;
            NormalizeBodySpriteSize(reference);
            bodyRenderer.color = Color.white;
            defaultBodyColor = bodyRenderer.color;
        }
        if (glowRenderer != null && entry != null)
        {
            Color glowColor = entry.tint;
            glowColor.a = 0f;
            glowRenderer.color = glowColor;
            defaultGlowColor = glowColor;
        }
    }

    private void NormalizeBodySpriteSize(Sprite reference)
    {
        if (bodyRenderer == null || bodyRenderer.sprite == null) return;
        if (reference == null) reference = bodyRenderer.sprite;
        Vector2 expected = reference.bounds.size;
        Vector2 actual = bodyRenderer.sprite.bounds.size;
        if (expected.x <= 0f || expected.y <= 0f || actual.x <= 0f || actual.y <= 0f) return;
        float factor = Mathf.Min(expected.x / actual.x, expected.y / actual.y);
        bodyRenderer.transform.localScale = defaultBodyLocalScale * Mathf.Max(0.01f, factor);
    }

    public void SnapTo(Vector3 worldPosition)
    {
        KillTweens();
        transform.position = worldPosition;
    }

    public void KillTweens()
    {
        transform.DOKill();

        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = defaultScale;
            visualRoot.localPosition = defaultLocalPosition;
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

            Color baseGlowColor = Color.white;
            baseGlowColor.a = 0f;
            glowRenderer.color = baseGlowColor;

            glowRenderer
                .DOFade(glowAlpha, glowFadeDuration)
                .SetEase(Ease.OutQuad);

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

    public Tween FadeOutSelectedVisual(bool resetVisual = true)
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (resetVisual)
        {
            visualRoot.DOKill();
            visualRoot.localScale = defaultScale;
            visualRoot.localPosition = defaultLocalPosition;
        }

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
        AppendLandingBounce(sequence, worldPosition, landingBounceHeightMultiplier);
        return sequence;
    }

    public Tween AnimateToWithSelectionBounce(Vector3 worldPosition, float duration, Ease ease = Ease.OutQuad)
    {
        transform.DOKill();
        Sequence sequence = DOTween.Sequence();
        sequence.Append(transform.DOMove(worldPosition, duration).SetEase(ease));
        AppendLandingBounce(sequence, worldPosition, selectionBounceHeightMultiplier);
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
        AppendLandingBounce(sequence, worldPosition, landingBounceHeightMultiplier);
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


        Tween dropTween = transform.DOMove(targetSlotPosition, dropDuration).SetEase(Ease.InQuad);
        dropTween.OnComplete(() =>
        {
            if (AudioMgr.Instance != null)
            {
                AudioMgr.Instance.Play("掉");
            }
        });
        sequence.Append(dropTween);
        AppendLandingBounce(sequence, targetSlotPosition, landingBounceHeightMultiplier);
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

    private void AppendLandingBounce(Sequence sequence, Vector3 landingPosition, float heightMultiplier = 1f)
    {
        float height = landingBounceHeight * Mathf.Max(0f, heightMultiplier);
        if (sequence == null || height <= 0f)
        {
            return;
        }

        float bounceUpDuration = Mathf.Max(0f, landingBounceUpDuration);
        float bounceDownDuration = Mathf.Max(0f, landingBounceDownDuration);
        if (bounceUpDuration <= 0f || bounceDownDuration <= 0f)
        {
            return;
        }

        Vector3 bouncePeakPosition = landingPosition + Vector3.up * height;
        sequence.Append(transform.DOMove(bouncePeakPosition, bounceUpDuration).SetEase(Ease.OutQuad));
        sequence.Append(transform.DOMove(landingPosition, bounceDownDuration).SetEase(Ease.InQuad));
    }
}
