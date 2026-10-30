using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class TubeView : MonoBehaviour
{
    private const int MinimumFrontOverlaySortingOffset = 101;
    private const int TubeBackSortingGap = 2;

    [SerializeField] private bool isLongTube;

    [Header("Refs")]
    [Tooltip("用于接收点击射线的碰撞体；为空时 Reset/OnValidate 会尝试自动获取。")]
    [SerializeField] private Collider2D hitCollider;
    [Tooltip("试管口位置，小球选中和倒入时会以这里作为管口参考点。")]
    [SerializeField] private Transform tubeMouthAnchor;
    [Tooltip("从本管倒出时可选的路径控制点，用于调整飞行轨迹。")]
    [SerializeField] private Transform sourceWaypointAnchor;
    [Tooltip("倒入本管时可选的路径控制点，用于调整飞行轨迹。")]
    [SerializeField] private Transform targetWaypointAnchor;
    [Tooltip("槽位根节点；其子物体会按本地 Y 从低到高、同层按 X 从左到右收集为小球槽位。")]
    [SerializeField] private Transform slotRoot;
    [Tooltip("小球槽位列表，通常由 slotRoot 自动收集。")]
    [SerializeField] private List<Transform> slotAnchors = new List<Transform>();
    [Tooltip("运行时小球挂载的父节点；为空时使用当前试管节点。")]
    [SerializeField] private Transform ballContainer;
    [Tooltip("试管被选中时显示的标记对象。")]
    [SerializeField] private GameObject selectedMarker;
    [Tooltip("试管后景 SpriteRenderer，用于完成后染色；需要显示在管内小球下方。")]
    [SerializeField] private SpriteRenderer tubeRenderer;
    [Tooltip("需要显示在管内小球上方的前景 SpriteRenderer；为空时会自动查找 sprite1 / sprite (1)。")]
    [SerializeField] private SpriteRenderer frontOverlayRenderer;
    [Tooltip("完成后自底向上展示的蜡烛对象；为空时会自动查找子物体 lazhu。")]
    [SerializeField] private Transform lazhu;
    [Tooltip("蜡烛下方的火焰特效；为空时会自动查找子物体 fireEff。")]
    [SerializeField] private Transform fireEff;

    [Header("Complete Visual")]
    [Tooltip("长管完成后试管本体切换到的颜色。")]
    [SerializeField] private Color completedTint = Color.white;
    [Tooltip("本试管内小球 SpriteRenderer 的排序层级。")]
    [SerializeField] private int ballSortingOrder = 0;
    [Tooltip("前景 SpriteRenderer 相对管内小球的排序提升值，需要高于选中小球的临时排序。")]
    [SerializeField] private int frontOverlaySortingOffset = MinimumFrontOverlaySortingOffset;
    [Tooltip("蜡烛相对前景 SpriteRenderer 的排序提升值。")]
    [SerializeField] private int lazhuSortingOffset = 1;
    [Tooltip("蜡烛染色亮度倍率，避免使用小球颜色时过暗。")]
    [SerializeField] private float lazhuTintBrightness = 1.35f;
    [Tooltip("蜡烛染色每个颜色通道的最低亮度。")]
    [SerializeField] private float lazhuTintMinChannel = 0.18f;
    [Tooltip("蜡烛自底向上展示的动画时长。")]
    [SerializeField] private float lazhuRevealDuration = 0.35f;
    [Tooltip("蜡烛展示动画缓动。")]
    [SerializeField] private Ease lazhuRevealEase = Ease.OutCubic;
    [Tooltip("蜡烛开始展示后，火焰特效延迟出现的时间。")]
    [SerializeField] private float fireEffRevealDelay = 0f;
    [Tooltip("火焰特效相对管子/蜡烛/小球的排序提升值，避免被管子挡住。")]
    [SerializeField] private int fireEffSortingBoost = 200;
    private readonly List<BallView> runtimeBallViews = new List<BallView>();
    private Vector3 initialScale = Vector3.one;
    private Color initialTubeColor = Color.white;
    private Color initialLazhuColor = Color.white;
    private Sprite initialLazhuSprite;
    private bool hasInitialLazhuSprite;
    private float initialLazhuLocalBottomOffset;
    private bool hasInitialTubeColor;
    private bool hasInitialLazhuColor;
    private bool completedVisualApplied;
    private BallColorType lastCompletedColor = BallColorType.None;
    private Vector3 initialLazhuLocalPosition = Vector3.zero;
    private Vector3 initialLazhuLocalScale = Vector3.one;
    private float lazhuLocalBottomOffset;
    private bool hasInitialLazhuState;

    public Collider2D HitCollider => hitCollider;
    public bool IsLongTube => isLongTube;
    public Transform TubeMouthAnchor => tubeMouthAnchor != null ? tubeMouthAnchor : transform;
    public Transform SourceWaypointAnchor => sourceWaypointAnchor;
    public Transform TargetWaypointAnchor => targetWaypointAnchor;
    public Transform BallContainer => ballContainer != null ? ballContainer : transform;
    public int Capacity => slotAnchors.Count;
    public int BallSortingOrder => ballSortingOrder;
    public IReadOnlyList<BallView> RuntimeBallViews => runtimeBallViews;
    public BallColorType LastCompletedColor => lastCompletedColor;

    public int GetSelectedBallSortingOrder(int sortingBoost)
    {
        int boostedSortingOrder = ballSortingOrder + sortingBoost;
        FindFrontOverlayRenderer();
        if (frontOverlayRenderer == null)
        {
            return boostedSortingOrder;
        }

        return Mathf.Min(boostedSortingOrder, GetFrontOverlaySortingOrder() - 1);
    }

    private void Awake()
    {
        initialScale = transform.localScale;
        FindTubeRenderer();
        ApplyTubeRendererSorting();
        ApplyFrontOverlaySorting();

        if (tubeRenderer != null)
        {
            initialTubeColor = tubeRenderer.color;
            hasInitialTubeColor = true;
        }

        CacheLazhuState();
        HideLazhuImmediate();
        SetSelected(false);
    }

    private void Reset()
    {
        hitCollider = GetComponent<Collider2D>();
        if (ballContainer == null)
        {
            ballContainer = transform;
        }

        FindLazhu();
        FindFireEff();
        ApplyTubeSorting();
        CollectSlotAnchors();
    }

    private void OnValidate()
    {
        if (hitCollider == null)
        {
            hitCollider = GetComponent<Collider2D>();
        }

        if (ballContainer == null)
        {
            ballContainer = transform;
        }

        if (slotRoot != null)
        {
            CollectSlotAnchors();
        }

        FindLazhu();
        FindFireEff();
        ApplyTubeSorting();
    }

    public void CollectSlotAnchors()
    {
        slotAnchors.Clear();

        if (slotRoot == null)
        {
            return;
        }

        for (int i = 0; i < slotRoot.childCount; i++)
        {
            slotAnchors.Add(slotRoot.GetChild(i));
        }

        slotAnchors.Sort((left, right) =>
        {
            int yCompare = left.localPosition.y.CompareTo(right.localPosition.y);
            return yCompare != 0 ? yCompare : left.localPosition.x.CompareTo(right.localPosition.x);
        });
    }

    [ContextMenu("Collect Slot Anchors")]
    private void CollectSlotAnchorsContextMenu()
    {
        CollectSlotAnchors();
    }

    public Vector3 GetSlotWorldPosition(int index)
    {
        if (slotAnchors.Count == 0)
        {
            return transform.position;
        }

        index = Mathf.Clamp(index, 0, slotAnchors.Count - 1);
        return slotAnchors[index].position;
    }

    public float GetSlotSpacing()
    {
        if (slotAnchors.Count < 2)
        {
            return 0.5f;
        }

        float totalSpacing = 0f;
        for (int i = 1; i < slotAnchors.Count; i++)
        {
            totalSpacing += Vector3.Distance(slotAnchors[i - 1].position, slotAnchors[i].position);
        }

        return totalSpacing / (slotAnchors.Count - 1);
    }

    public List<BallView> GetTopBallViews(int count)
    {
        List<BallView> selectedBalls = new List<BallView>(count);
        int clampedCount = Mathf.Clamp(count, 0, runtimeBallViews.Count);

        for (int i = 0; i < clampedCount; i++)
        {
            selectedBalls.Add(runtimeBallViews[runtimeBallViews.Count - 1 - i]);
        }

        return selectedBalls;
    }

    public void SetRuntimeBalls(IList<BallView> balls)
    {
        runtimeBallViews.Clear();

        if (balls == null)
        {
            return;
        }

        for (int i = 0; i < balls.Count; i++)
        {
            runtimeBallViews.Add(balls[i]);
            if (balls[i] != null)
            {
                balls[i].transform.SetParent(BallContainer, true);
                balls[i].ResetVisuals();
                balls[i].SetSortingOrder(ballSortingOrder);
            }
        }
    }

    public void AddRuntimeBalls(IList<BallView> balls)
    {
        AddRuntimeBalls(balls, true);
    }

    public void AddRuntimeBalls(IList<BallView> balls, bool resetVisuals)
    {
        if (balls == null)
        {
            return;
        }

        for (int i = 0; i < balls.Count; i++)
        {
            BallView ball = balls[i];
            if (ball == null)
            {
                continue;
            }

            ball.transform.SetParent(BallContainer, true);
            if (resetVisuals)
            {
                ball.ResetVisuals();
                ball.SetSortingOrder(ballSortingOrder);
            }
            runtimeBallViews.Add(ball);
        }
    }

    public void RemoveTopRuntimeBalls(int count)
    {
        int clampedCount = Mathf.Clamp(count, 0, runtimeBallViews.Count);
        runtimeBallViews.RemoveRange(runtimeBallViews.Count - clampedCount, clampedCount);
    }

    public void RemoveRuntimeBalls(IList<BallView> balls)
    {
        if (balls == null)
        {
            return;
        }

        for (int i = 0; i < balls.Count; i++)
        {
            BallView ball = balls[i];
            if (ball == null)
            {
                continue;
            }

            runtimeBallViews.Remove(ball);
        }
    }

    public void SnapRuntimeBallsToSlots()
    {
        int ballCount = Mathf.Min(runtimeBallViews.Count, slotAnchors.Count);
        for (int i = 0; i < ballCount; i++)
        {
            runtimeBallViews[i].SnapTo(GetSlotWorldPosition(i));
        }
    }

    public void ClearRuntimeBalls()
    {
        for (int i = 0; i < runtimeBallViews.Count; i++)
        {
            if (runtimeBallViews[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeBallViews[i].gameObject);
                }
                else
                {
                    DestroyImmediate(runtimeBallViews[i].gameObject);
                }
            }
        }

        runtimeBallViews.Clear();
    }

    public void SetSelected(bool selected)
    {
        if (selectedMarker != null)
        {
            selectedMarker.SetActive(selected && !completedVisualApplied);
        }
    }

    public void SetCompletedVisualImmediate()
    {
        SetCompletedVisualImmediate(BallColorType.None, Color.white);
    }

    public void SetCompletedVisualImmediate(BallColorType completedColor, Color lazhuTint, Sprite lazhuSprite = null)
    {
        completedVisualApplied = true;
        lastCompletedColor = completedColor;
        SetSelected(false);
        ApplyLazhuTint(lazhuTint, lazhuSprite);
        ShowLazhuImmediate();

        if (tubeRenderer != null)
        {
            tubeRenderer.color = completedTint;
        }

        for (int i = 0; i < runtimeBallViews.Count; i++)
        {
            if (runtimeBallViews[i] != null)
            {
                if (!isLongTube) runtimeBallViews[i].gameObject.SetActive(false);
            }
        }
    }

    public Sequence PlayCompletedAnimation(BallColorType completedColor, Color lazhuTint, Sprite lazhuSprite = null)
    {
        if (AudioMgr.Instance != null)
        {
            AudioMgr.Instance.Play("满贯音效");
        }

        if (completedVisualApplied)
        {
            return null;
        }

        lastCompletedColor = completedColor;
        completedVisualApplied = true;
        SetSelected(false);
        ApplyLazhuTint(lazhuTint, lazhuSprite);

        if (tubeRenderer != null)
        {
            if (!hasInitialTubeColor)
            {
                initialTubeColor = tubeRenderer.color;
                hasInitialTubeColor = true;
            }

            tubeRenderer.color = completedTint;
        }

        Sequence completeSequence = DOTween.Sequence();

        for (int i = 0; i < runtimeBallViews.Count; i++)
        {
            if (runtimeBallViews[i] != null)
            {
                runtimeBallViews[i].KillTweens();
                if (!isLongTube) runtimeBallViews[i].gameObject.SetActive(false);
            }
        }

        Tween lazhuTween = PlayLazhuReveal();
        if (lazhuTween != null)
        {
            completeSequence.Insert(0f, lazhuTween);
        }

        return completeSequence;
    }

    public void ResetVisualState()
    {
        transform.DOKill();
        transform.localScale = initialScale;
        completedVisualApplied = false;
        lastCompletedColor = BallColorType.None;
        HideLazhuImmediate();
        SetSelected(false);

        if (tubeRenderer != null && hasInitialTubeColor)
        {
            tubeRenderer.color = initialTubeColor;
        }

        ResetLazhuTint();

        for (int i = 0; i < runtimeBallViews.Count; i++)
        {
            if (runtimeBallViews[i] != null)
            {
                runtimeBallViews[i].ResetVisuals();
            }
        }
    }

    private void FindLazhu()
    {
        if (lazhu != null)
        {
            return;
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform child = childTransforms[i];
            if (child != null && child != transform && child.name.StartsWith("lazhu"))
            {
                lazhu = child;
                return;
            }
        }
    }

    private void CacheLazhuState()
    {
        FindLazhu();
        if (lazhu == null)
        {
            return;
        }

        FindFireEff();

        initialLazhuLocalPosition = lazhu.localPosition;
        initialLazhuLocalScale = lazhu.localScale;
        lazhuLocalBottomOffset = 0f;

        SpriteRenderer lazhuRenderer = lazhu.GetComponent<SpriteRenderer>();
        if (lazhuRenderer != null)
        {
            initialLazhuColor = lazhuRenderer.color;
            hasInitialLazhuColor = true;
            initialLazhuSprite = lazhuRenderer.sprite;
            hasInitialLazhuSprite = true;
            ApplyLazhuSorting(lazhuRenderer);

            if (lazhuRenderer.sprite != null)
            {
                lazhuLocalBottomOffset = lazhuRenderer.sprite.bounds.min.y;
                initialLazhuLocalBottomOffset = lazhuLocalBottomOffset;
            }
        }

        hasInitialLazhuState = true;
    }

    private void HideLazhuImmediate()
    {
        if (!hasInitialLazhuState)
        {
            CacheLazhuState();
        }
        if (lazhu == null)
        {
            return;
        }

        lazhu.DOKill();
        SetFireEffActive(false);
        ApplyLazhuRevealProgress(0f);
        lazhu.gameObject.SetActive(false);
    }

    private void ApplyLazhuTint(Color tint, Sprite spriteOverride = null)
    {
        if (!hasInitialLazhuState)
        {
            CacheLazhuState();
        }
        if (lazhu == null)
        {
            return;
        }

        SpriteRenderer lazhuRenderer = lazhu.GetComponent<SpriteRenderer>();
        if (lazhuRenderer == null)
        {
            return;
        }

        if (!hasInitialLazhuColor)
        {
            initialLazhuColor = lazhuRenderer.color;
            hasInitialLazhuColor = true;
        }

        if (spriteOverride != null)
        {
            lazhuRenderer.sprite = spriteOverride;
            lazhuLocalBottomOffset = spriteOverride.bounds.min.y;
        }
        if (isLongTube) return;

        if (lastCompletedColor == BallColorType.Red)
        {
            // Keep the requested red exact, without the general brightness adjustment.
            lazhuRenderer.color = new Color(246f / 255f, 19f / 255f, 21f / 255f, initialLazhuColor.a);
            return;
        }

        lazhuRenderer.color = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)) <= 0.01f
            ? new Color(1f, 1f, 1f, initialLazhuColor.a)
            : ResolveLazhuTint(tint);
    }

    private Color ResolveLazhuTint(Color tint)
    {
        float brightness = Mathf.Max(0f, lazhuTintBrightness);
        float minChannel = Mathf.Clamp01(lazhuTintMinChannel);

        tint.r = Mathf.Clamp01(Mathf.Max(tint.r * brightness, minChannel));
        tint.g = Mathf.Clamp01(Mathf.Max(tint.g * brightness, minChannel));
        tint.b = Mathf.Clamp01(Mathf.Max(tint.b * brightness, minChannel));
        tint.a = initialLazhuColor.a;
        return tint;
    }

    private void ResetLazhuTint()
    {
        if (lazhu == null || !hasInitialLazhuColor)
        {
            return;
        }

        SpriteRenderer lazhuRenderer = lazhu.GetComponent<SpriteRenderer>();
        if (lazhuRenderer != null)
        {
            lazhuRenderer.color = initialLazhuColor;
            if (hasInitialLazhuSprite) lazhuRenderer.sprite = initialLazhuSprite;
            lazhuLocalBottomOffset = initialLazhuLocalBottomOffset;
        }
    }

    private void ShowLazhuImmediate()
    {
        if (!hasInitialLazhuState)
        {
            CacheLazhuState();
        }
        if (lazhu == null)
        {
            return;
        }

        lazhu.DOKill();
        ApplyLazhuSorting();
        lazhu.gameObject.SetActive(true);
        ApplyLazhuRevealProgress(1f);
        SetFireEffActive(true);
    }

    private Tween PlayLazhuReveal()
    {
        if (!hasInitialLazhuState)
        {
            CacheLazhuState();
        }
        if (lazhu == null)
        {
            return null;
        }

        lazhu.DOKill();
        SetFireEffActive(false);
        ApplyLazhuSorting();
        lazhu.gameObject.SetActive(true);
        ApplyLazhuRevealProgress(0f);

        Sequence sequence = DOTween.Sequence().SetTarget(lazhu);
        sequence.Insert(0f, DOVirtual.Float(0f, 1f, Mathf.Max(0.01f, lazhuRevealDuration), ApplyLazhuRevealProgress)
            .SetEase(lazhuRevealEase)
            .SetTarget(lazhu));

        if (fireEff != null)
        {
            sequence.InsertCallback(Mathf.Max(0f, fireEffRevealDelay), () => SetFireEffActive(true));
        }

        return sequence;
    }

    private void ApplyLazhuRevealProgress(float progress)
    {
        if (lazhu == null)
        {
            return;
        }

        progress = Mathf.Clamp01(progress);
        Vector3 scale = initialLazhuLocalScale;
        scale.y = initialLazhuLocalScale.y * progress;

        Vector3 position = initialLazhuLocalPosition;
        position.y = initialLazhuLocalPosition.y + lazhuLocalBottomOffset * initialLazhuLocalScale.y * (1f - progress);

        lazhu.localScale = scale;
        lazhu.localPosition = position;
    }

    private void FindFireEff()
    {
        if (fireEff != null)
        {
            return;
        }

        Transform searchRoot = lazhu != null ? lazhu : transform;
        Transform[] childTransforms = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform child = childTransforms[i];
            if (child != null && child != searchRoot && child.name.StartsWith("fireEff"))
            {
                fireEff = child;
                return;
            }
        }
    }

    private void FindFrontOverlayRenderer()
    {
        if (frontOverlayRenderer != null)
        {
            return;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer != tubeRenderer && IsFrontOverlayName(renderer.name))
            {
                frontOverlayRenderer = renderer;
                return;
            }
        }
    }

    private void FindTubeRenderer()
    {
        if (tubeRenderer != null)
        {
            return;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && IsTubeBackName(renderer.name))
            {
                tubeRenderer = renderer;
                return;
            }
        }
    }

    private bool IsFrontOverlayName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        string normalizedName = NormalizeRendererName(objectName);
        return normalizedName == "sprite1" || normalizedName == "sprite(1)";
    }

    private bool IsTubeBackName(string objectName)
    {
        return NormalizeRendererName(objectName) == "sprite";
    }

    private string NormalizeRendererName(string objectName)
    {
        return string.IsNullOrEmpty(objectName) ? string.Empty : objectName.ToLowerInvariant().Replace(" ", string.Empty);
    }

    private void ApplyTubeSorting()
    {
        ApplyTubeRendererSorting();
        ApplyFrontOverlaySorting();
    }

    private void ApplyTubeRendererSorting()
    {
        FindTubeRenderer();
        if (tubeRenderer == null)
        {
            return;
        }

        tubeRenderer.sortingOrder = GetTubeBackSortingOrder();
    }

    private void ApplyFrontOverlaySorting()
    {
        FindTubeRenderer();
        FindFrontOverlayRenderer();
        if (frontOverlayRenderer == null)
        {
            return;
        }

        if (tubeRenderer != null)
        {
            frontOverlayRenderer.sortingLayerID = tubeRenderer.sortingLayerID;
        }

        frontOverlayRenderer.sortingOrder = GetFrontOverlaySortingOrder();
    }

    private int GetFrontOverlaySortingOrder()
    {
        return ballSortingOrder + Mathf.Max(frontOverlaySortingOffset, MinimumFrontOverlaySortingOffset);
    }

    private int GetTubeBackSortingOrder()
    {
        return ballSortingOrder - TubeBackSortingGap;
    }

    private void ApplyLazhuSorting(SpriteRenderer lazhuRenderer = null)
    {
        if (lazhuRenderer == null && lazhu != null)
        {
            lazhuRenderer = lazhu.GetComponent<SpriteRenderer>();
        }

        if (lazhuRenderer == null)
        {
            return;
        }

        int baseSortingOrder = GetFrontOverlaySortingOrder();
        int sortingLayerId = 0;
        bool hasSortingLayer = false;

        FindFrontOverlayRenderer();
        if (frontOverlayRenderer != null)
        {
            sortingLayerId = frontOverlayRenderer.sortingLayerID;
            baseSortingOrder = Mathf.Max(baseSortingOrder, frontOverlayRenderer.sortingOrder);
            hasSortingLayer = true;
        }

        if (tubeRenderer != null)
        {
            sortingLayerId = tubeRenderer.sortingLayerID;
            baseSortingOrder = Mathf.Max(baseSortingOrder, tubeRenderer.sortingOrder);
            hasSortingLayer = true;
        }

        if (hasSortingLayer)
        {
            lazhuRenderer.sortingLayerID = sortingLayerId;
        }

        lazhuRenderer.sortingOrder = baseSortingOrder + Mathf.Max(1, lazhuSortingOffset);
    }

    private void SetFireEffActive(bool active)
    {
        FindFireEff();
        if (fireEff == null)
        {
            return;
        }

        if (active)
        {
            ApplyFireEffSorting();
        }

        fireEff.gameObject.SetActive(active);
    }

    private void ApplyFireEffSorting()
    {
        if (fireEff == null)
        {
            return;
        }

        int baseSortingOrder = ballSortingOrder;
        int sortingLayerId = 0;
        bool hasSortingLayer = false;

        if (tubeRenderer != null)
        {
            sortingLayerId = tubeRenderer.sortingLayerID;
            baseSortingOrder = Mathf.Max(baseSortingOrder, tubeRenderer.sortingOrder);
            hasSortingLayer = true;
        }

        SpriteRenderer lazhuRenderer = lazhu != null ? lazhu.GetComponent<SpriteRenderer>() : null;
        if (lazhuRenderer != null)
        {
            baseSortingOrder = Mathf.Max(baseSortingOrder, lazhuRenderer.sortingOrder);
            if (!hasSortingLayer)
            {
                sortingLayerId = lazhuRenderer.sortingLayerID;
                hasSortingLayer = true;
            }
        }

        Renderer[] fireRenderers = fireEff.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < fireRenderers.Length; i++)
        {
            Renderer fireRenderer = fireRenderers[i];
            if (fireRenderer == null)
            {
                continue;
            }

            if (hasSortingLayer)
            {
                fireRenderer.sortingLayerID = sortingLayerId;
            }

            fireRenderer.sortingOrder = baseSortingOrder + fireEffSortingBoost;
        }
    }
}
