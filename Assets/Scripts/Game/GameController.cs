using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    private const float BackgroundDistanceFromCamera = 5f;
    private GameObject backgroundObject;
    private SpriteRenderer backgroundRenderer;
    private GameObject backgroundCanvasObject;
    private Image backgroundImage;
    private bool backgroundObjectCreated;
    private Sprite sceneBackgroundSprite;
    private Color sceneBackgroundColor = Color.white;
    private Texture backgroundTextureCached;
    private Sprite backgroundSpriteCached;
    private const int InitialBallSortingOrder = 0;
    private const int BallsPerConfiguredColor = 2;
    [SerializeField] private Sprite configuredLazhuSprite;
    private GameRootPortraitAspectScaler gameRootAspectScaler;
    private readonly Dictionary<Texture, Sprite> textureSpriteCache = new Dictionary<Texture, Sprite>();
    private readonly HashSet<BallView> movingBalls = new HashSet<BallView>();

    [Serializable]
    public class TubeLevelEntry
    {
        [Tooltip("本关中对应的试管视图对象。")]
        public TubeView tubeView;
        [Tooltip("试管初始小球颜色列表，顺序为从底部到顶部。")]
        public List<BallColorType> balls = new List<BallColorType>();
    }

    private sealed class SelectionState
    {
        public TubeView sourceView;
        public TubeModel sourceModel;
    }

    private sealed class PreparedMoveState
    {
        public TubeView sourceView;
        public TubeView targetView;
        public MoveEvaluation move;
        public List<BallView> movedBalls;
        public List<BallView> overflowBalls;
        public int targetStartCount;
        public bool targetCompletedByMove;
        public bool puzzleSolvedByMove;
    }

    private sealed class IntroDropState
    {
        public TubeView tubeView;
        public TubeModel model;
        public List<BallView> balls;
    }

    private sealed class IntroDropBallState
    {
        public TubeView tubeView;
        public BallView ball;
        public int ballIndex;
        public Vector3 slotPosition;
    }

    private enum GamePhase
    {
        Idle = 0,
        Win = 1
    }

    [Header("Board")]
    [Tooltip("用于把点击屏幕位置转换到世界坐标的相机；为空时使用 Camera.main。")]
    [SerializeField] private Camera worldCamera;
    [Tooltip("运行时生成小球时使用的预制体。")]
    [SerializeField] private BallView ballPrefab;
    [Tooltip("小球颜色与 Sprite/发光颜色的配置表。")]
    [SerializeField] private BallVisualPalette ballVisualPalette;
    [Tooltip("当前关卡所有试管及其初始小球配置。")]
    [SerializeField] private List<TubeLevelEntry> levelEntries = new List<TubeLevelEntry>();
    [Tooltip("需要完成的长管数量；同时其余试管必须为空才算解谜完成。")]
    [SerializeField] private int requiredCompletedTubeCount = 4;

    [Header("Intro Drop Animation")]
    [Tooltip("开局时前几个管子的小球先隐藏，随后从管口上方落下。")]
    [SerializeField] private int introDropTubeCount = 5;
    [Tooltip("开局后等待多久开始落球。")]
    [SerializeField] private float introDropDelay = 2f;
    [Tooltip("小球初始位置高于管口的距离。")]
    [SerializeField] private float introDropSpawnHeight = 0.5f;
    [Tooltip("单颗小球从管口上方落到槽位的时长。")]
    [SerializeField] private float introDropDuration = 0.18f;
    [Tooltip("按从左到右顺序，相邻列开始落下的间隔。")]
    [SerializeField] private float introDropStartInterval = 0.04f;
    [Tooltip("同一列内，上下小球开始落下的间隔。")]
    [SerializeField] private float introDropSameColumnInterval = 0.04f;
    [Tooltip("开局小球落下时播放的音效名称。")]
    [SerializeField] private string introDropSfxName = "落下";
    [Tooltip("开局落球时，每隔几颗球补一次音效。")]
    [SerializeField] private int introDropSfxBallInterval = 2;
    [Tooltip("开局落球音效两次播放之间的最短间隔。")]
    [SerializeField] private float introDropSfxMinInterval = 0.06f;

    [Header("Selection Animation")]
    [Tooltip("当指定高度没有高于当前槽位时，选中小球使用的兜底上抬高度。")]
    [SerializeField] private float selectionLiftHeight = 0.35f;
    [Tooltip("选中后最底部小球中心停在管口上方的高度。")]
    [SerializeField] private float selectionBottomBallMouthOffset = 0.15f;
    [Tooltip("选中/取消选中时，小球抬起或回槽位的动画时长。")]
    [SerializeField] private float selectionTweenDuration = 0.15f;
    [Tooltip("选中小球临时提升的渲染排序值，越大越靠前。")]
    [SerializeField] private int selectionSortingBoost = 100;

    [Header("Move Animation")]
    [Tooltip("倒球时，小球保持当前横向位置并垂直抬起的动画时长。")]
    [SerializeField] private float raiseDuration = 0.18f;
    [Tooltip("小球横向移动前/落下前，距离管口上方的高度。")]
    [SerializeField] private float transferHoverHeight = 0.35f;
    [SerializeField] private float longTubeTransferHoverHeight = 0.35f;
    [Tooltip("小球从源管口上方飞到目标管口上方时的弧线高度。")]
    [SerializeField] private float transferArcHeight = 0.8f;
    [Tooltip("小球从源试管移动到目标试管管口的动画时长。")]
    [SerializeField] private float travelDuration = 0.2f;
    [SerializeField] private float longTubeTravelDuration = 0.2f;
    [Tooltip("小球从目标管口落入目标槽位的动画时长。")]
    [SerializeField] private float dropDuration = 0.15f;
    [SerializeField] private float longTubeDropDuration = 0.15f;
    [Tooltip("同一批倒球中，每个小球开始移动的间隔。")]
    [SerializeField] private float moveStartInterval = 0.05f;

    [Header("Events")]
    [Tooltip("全部目标长管完成且其余试管为空时触发。")]
    [SerializeField] private UnityEvent onPuzzleSolved;

    private readonly Dictionary<TubeView, TubeModel> tubeModels = new Dictionary<TubeView, TubeModel>();
    private readonly Dictionary<TubeView, Coroutine> tubeTransitionCoroutines = new Dictionary<TubeView, Coroutine>();
    private readonly Dictionary<TubeView, int> pendingIncomingMoves = new Dictionary<TubeView, int>();
    private readonly Dictionary<TubeView, int> pendingOutgoingMoves = new Dictionary<TubeView, int>();
    private GamePhase gamePhase = GamePhase.Idle;
    private SelectionState currentSelection;
    private bool puzzleSolvedInvoked;
    private bool introDropInProgress;
    private EndPanel endPanel;

    private void Start()
    {
        foreach (Canvas canvas in FindObjectsOfType<Canvas>())
        {
            endPanel = canvas.GetComponentInChildren<EndPanel>(true);
            if (endPanel != null) break;
        }
        if (endPanel != null) endPanel.gameObject.SetActive(false);
        EnsureBackgroundRenderer();
        BuildBoard();
    }

    private void LateUpdate()
    {
        UpdateBackgroundRenderer();
    }

    private void Update()
    {
        if (!TryGetPressedScreenPosition(out Vector2 screenPosition)) return;
        //if (Utils.HasStoreJumped) Utils.OpenStore();
        if (gamePhase == GamePhase.Win || introDropInProgress) return;

        if (!TryRaycastTube(screenPosition, out TubeView tubeView))
        {
            return;
        }

        RecordTubeClick();
        HandleTubeClicked(tubeView);
    }

    [ContextMenu("Build Board")]
    public void BuildBoard()
    {
        if (ballPrefab == null)
        {
            Debug.LogError("GameController 缺少 Ball Prefab 引用。", this);
            return;
        }

        DOTween.Kill(this);

        StopAllCoroutines();
        tubeModels.Clear();
        tubeTransitionCoroutines.Clear();
        pendingIncomingMoves.Clear();
        pendingOutgoingMoves.Clear();
        currentSelection = null;
        gamePhase = GamePhase.Idle;
        puzzleSolvedInvoked = false;
        introDropInProgress = false;
        movingBalls.Clear();

        List<IntroDropState> introDropStates = new List<IntroDropState>();
        for (int i = 0; i < levelEntries.Count; i++)
        {
            TubeLevelEntry entry = levelEntries[i];
            if (entry == null || entry.tubeView == null)
            {
                continue;
            }

            entry.tubeView.CollectSlotAnchors();
            entry.tubeView.ResetVisualState();
            entry.tubeView.ClearRuntimeBalls();

            if (entry.tubeView.Capacity <= 0)
            {
                Debug.LogError($"{entry.tubeView.name} 没有配置任何槽位物体。", entry.tubeView);
                continue;
            }

            TubeModel model = new TubeModel(entry.tubeView.name, entry.tubeView.Capacity);

            try
            {
                model.SetBalls(ExpandConfiguredBallPairs(entry));
            }
            catch (Exception exception)
            {
                Debug.LogError(exception.Message, entry.tubeView);
                continue;
            }

            tubeModels[entry.tubeView] = model;

            bool shouldPlayIntroDrop = i < introDropTubeCount && model.Count > 0;
            List<BallView> spawnedBalls = new List<BallView>(model.Count);
            for (int ballIndex = 0; ballIndex < model.Count; ballIndex++)
            {
                BallView ballView = Instantiate(ballPrefab, entry.tubeView.BallContainer);
                ballView.name = $"{model.Balls[ballIndex]}_{ballIndex}";
                ballView.Initialize(model.Balls[ballIndex], ballVisualPalette, ResolveConfiguredBallSprite(model.Balls[ballIndex]));
                Vector3 slotPosition = entry.tubeView.GetSlotWorldPosition(ballIndex);
                ballView.SnapTo(shouldPlayIntroDrop
                    ? GetIntroDropStartPosition(entry.tubeView, slotPosition)
                    : slotPosition);
                spawnedBalls.Add(ballView);
            }

            entry.tubeView.SetRuntimeBalls(spawnedBalls);
            foreach (BallView spawnedBall in spawnedBalls)
                if (spawnedBall != null) spawnedBall.SetSortingOrder(InitialBallSortingOrder);

            if (shouldPlayIntroDrop)
            {
                for (int ballIndex = 0; ballIndex < spawnedBalls.Count; ballIndex++)
                {
                    if (spawnedBalls[ballIndex] != null)
                    {
                        spawnedBalls[ballIndex].gameObject.SetActive(false);
                    }
                }

                introDropStates.Add(new IntroDropState
                {
                    tubeView = entry.tubeView,
                    model = model,
                    balls = spawnedBalls
                });
            }

            if (model.IsCompleted && !shouldPlayIntroDrop)
            {
                BallColorType completedColor = model.TopColor;
                entry.tubeView.SetCompletedVisualImmediate(completedColor, ResolveCompletedLazhuTint(completedColor), ResolveCompletedLazhuSprite(entry.tubeView, completedColor));
            }
        }

        if (introDropStates.Count > 0)
        {
            introDropInProgress = true;
            StartCoroutine(PlayIntroDropRoutine(introDropStates));
        }
    }

    private static List<BallColorType> ExpandConfiguredBallPairs(TubeLevelEntry entry)
    {
        List<BallColorType> expanded = new List<BallColorType>();
        if (entry == null || entry.balls == null) return expanded;
        foreach (BallColorType color in entry.balls)
            for (int i = 0; i < BallsPerConfiguredColor; i++) expanded.Add(color);
        return expanded;
    }

    private Sprite ResolveConfiguredBallSprite(BallColorType color)
    {
        Texture texture = AssetConfig.Instance != null ? AssetConfig.Instance.GetBallTexture(color) : null;
        return CreateSpriteFromTexture(texture);
    }

    private Sprite CreateSpriteFromTexture(Texture texture)
    {
        if (texture == null) return null;
        if (textureSpriteCache.TryGetValue(texture, out Sprite cached)) return cached;
        Texture2D texture2D = texture as Texture2D;
        if (texture2D == null || texture2D.width <= 0 || texture2D.height <= 0) return null;
        Sprite sprite = Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height),
            new Vector2(0.5f, 0.5f), 100f);
        textureSpriteCache[texture] = sprite;
        return sprite;
    }

    private void EnsureBackgroundRenderer()
    {
        if (backgroundObject == null)
        {
            GameObject existing = GameObject.Find("bk");
            if (existing != null)
            {
                backgroundRenderer = existing.GetComponent<SpriteRenderer>();
                if (backgroundRenderer != null)
                {
                    backgroundObject = existing;
                    sceneBackgroundSprite = backgroundRenderer.sprite;
                    sceneBackgroundColor = backgroundRenderer.color;
                }
                else
                {
                    backgroundImage = existing.GetComponent<Image>();
                    if (backgroundImage != null)
                    {
                        backgroundCanvasObject = existing;
                        sceneBackgroundSprite = backgroundImage.sprite;
                        sceneBackgroundColor = backgroundImage.color;
                        backgroundImage.enabled = false;
                        backgroundImage.raycastTarget = false;
                        backgroundCanvasObject.transform.SetAsFirstSibling();
                    }
                }
            }
            if (backgroundRenderer == null)
            {
                backgroundObject = new GameObject("GameBackground");
                backgroundRenderer = backgroundObject.AddComponent<SpriteRenderer>();
                // PlayWorks needs an exported material for a runtime-created renderer.
                SpriteRenderer templateRenderer = ballPrefab != null
                    ? ballPrefab.GetComponentInChildren<SpriteRenderer>(true)
                    : null;
                if (templateRenderer != null)
                    backgroundRenderer.sharedMaterial = templateRenderer.sharedMaterial;
                backgroundObjectCreated = true;
            }
            backgroundRenderer.sortingLayerID = SortingLayer.NameToID("Default");
            backgroundRenderer.sortingOrder = -30;
#if !UNITY_LUNA
            // Rendering layer masks are not exposed by the PlayWorks runtime.
            backgroundRenderer.renderingLayerMask = uint.MaxValue;
#endif
        }
        UpdateBackgroundRenderer();
    }

    private void UpdateBackgroundRenderer()
    {
        if (backgroundObject == null || backgroundRenderer == null) return;
        if (backgroundCanvasObject != null) backgroundCanvasObject.transform.SetAsFirstSibling();
        Camera camera = worldCamera != null ? worldCamera : Camera.main;
        if (camera == null) return;
        Texture texture = AssetConfig.Instance != null ? AssetConfig.Instance.backgroundTexture : null;
        if (texture != backgroundTextureCached)
        {
            backgroundTextureCached = texture;
            // Reuse the imported sprite when the configured texture is unchanged.
            backgroundSpriteCached = sceneBackgroundSprite != null && sceneBackgroundSprite.texture == texture
                ? sceneBackgroundSprite
                : CreateSpriteFromTexture(texture);
        }
        Sprite sprite = backgroundSpriteCached != null ? backgroundSpriteCached : sceneBackgroundSprite;
        backgroundRenderer.sprite = sprite;
        backgroundRenderer.color = backgroundSpriteCached != null ? Color.white : sceneBackgroundColor;
        if (backgroundImage != null)
        {
            backgroundImage.sprite = sprite;
            backgroundImage.color = backgroundRenderer.color;
            backgroundImage.enabled = false;
            backgroundImage.raycastTarget = false;
        }
        backgroundRenderer.enabled = sprite != null;
        if (sprite == null || !backgroundObjectCreated) return;
        Transform backgroundTransform = backgroundObject.transform;
        backgroundTransform.SetPositionAndRotation(
            camera.transform.position + camera.transform.forward * BackgroundDistanceFromCamera,
            camera.transform.rotation);
        Vector2 size = sprite.bounds.size;
        if (size.x <= 0f || size.y <= 0f) return;
        float height = camera.orthographic ? camera.orthographicSize * 2f : 2f;
        float width = height * Mathf.Max(0.01f, camera.aspect);
        float scale = Mathf.Max(width / size.x, height / size.y);
        backgroundTransform.localScale = new Vector3(scale, scale, 1f);
    }

    private Sprite ResolveCompletedLazhuSprite(TubeView tubeView, BallColorType color)
    {
        if (tubeView == null || tubeView.IsLongTube
            || (color != BallColorType.Black && color != BallColorType.White && color != BallColorType.Green)) return null;
        BallVisualPalette.BallVisualEntry entry = null;
        if (ballVisualPalette != null) ballVisualPalette.TryGetVisual(color, out entry);
        // Black uses an already shaded candle sprite; a ball texture override must not replace it with the white template.
        if (color == BallColorType.Black && entry != null && entry.lazhuSprite != null)
            return entry.lazhuSprite;
        Texture texture = AssetConfig.Instance != null ? AssetConfig.Instance.GetBallTexture(color) : null;
        bool overridden = texture != null && (entry == null || entry.sprite == null || entry.sprite.texture != texture);
        return overridden && configuredLazhuSprite != null ? configuredLazhuSprite : entry != null ? entry.lazhuSprite : null;
    }

    private float ScaleGameRootDistance(float value)
    {
        if (gameRootAspectScaler == null) gameRootAspectScaler = GetComponent<GameRootPortraitAspectScaler>();
        float factor = gameRootAspectScaler != null ? gameRootAspectScaler.CurrentScaleFactor : 1f;
        return value * Mathf.Max(0.01f, factor);
    }

    public void RestartLevel()
    {
        BuildBoard();
    }

    private IEnumerator PlayIntroDropRoutine(List<IntroDropState> introDropStates)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, introDropDelay));

        Sequence dropSequence = DOTween.Sequence();
        List<IntroDropBallState> dropBalls = CollectIntroDropBalls(introDropStates);
        float columnStartInterval = Mathf.Max(0f, introDropStartInterval);
        float sameColumnInterval = Mathf.Max(0f, introDropSameColumnInterval);
        int sfxBallInterval = Mathf.Max(1, introDropSfxBallInterval);
        float sfxMinInterval = Mathf.Max(0f, introDropSfxMinInterval);
        float lastSfxTime = -999f;
        int visibleDropIndex = 0;
        int columnIndex = -1;
        int sameColumnIndex = 0;
        float lastColumnX = 0f;

        for (int i = 0; i < dropBalls.Count; i++)
        {
            IntroDropBallState dropBall = dropBalls[i];
            BallView ball = dropBall.ball;
            if (ball == null)
            {
                continue;
            }

            if (columnIndex < 0 || Mathf.Abs(dropBall.slotPosition.x - lastColumnX) > 0.001f)
            {
                columnIndex++;
                sameColumnIndex = 0;
                lastColumnX = dropBall.slotPosition.x;
            }

            Vector3 startPosition = GetIntroDropStartPosition(dropBall.tubeView, dropBall.slotPosition);
            float startAt = columnIndex * columnStartInterval + sameColumnIndex * sameColumnInterval;
            ball.SnapTo(startPosition);
            ball.gameObject.SetActive(false);

            dropSequence.InsertCallback(startAt, () =>
            {
                if (ball != null)
                {
                    ball.gameObject.SetActive(true);
                }
            });
            dropSequence.Insert(
                startAt,
                ball.AnimateToWithLandingBounce(
                    dropBall.slotPosition,
                    Mathf.Max(0.01f, introDropDuration),
                    Ease.InQuad));

            if (!string.IsNullOrEmpty(introDropSfxName)
                && visibleDropIndex % sfxBallInterval == 0
                && startAt - lastSfxTime >= sfxMinInterval)
            {
                lastSfxTime = startAt;
                dropSequence.InsertCallback(startAt, PlayIntroDropSfx);
            }

            sameColumnIndex++;
            visibleDropIndex++;
        }

        if (dropSequence.IsActive())
        {
            yield return dropSequence.WaitForCompletion();
        }

        for (int i = 0; i < introDropStates.Count; i++)
        {
            IntroDropState state = introDropStates[i];
            if (state != null
                && state.model != null
                && state.model.IsCompleted
                && state.tubeView != null)
            {
                BallColorType completedColor = state.model.TopColor;
                state.tubeView.SetCompletedVisualImmediate(completedColor, ResolveCompletedLazhuTint(completedColor), ResolveCompletedLazhuSprite(state.tubeView, completedColor));
            }
        }

        introDropInProgress = false;
    }

    private void PlayIntroDropSfx()
    {
        PlaySfxSafe(introDropSfxName);
    }

    private void PlaySfxSafe(string sfxName)
    {
        if (string.IsNullOrEmpty(sfxName) || AudioMgr.Instance == null)
        {
            return;
        }

        try
        {
            AudioMgr.Instance.Play(sfxName);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"播放音效失败: {sfxName}, {exception.Message}", this);
        }
    }

    private List<IntroDropBallState> CollectIntroDropBalls(List<IntroDropState> introDropStates)
    {
        List<IntroDropBallState> dropBalls = new List<IntroDropBallState>();
        for (int tubeIndex = 0; tubeIndex < introDropStates.Count; tubeIndex++)
        {
            IntroDropState state = introDropStates[tubeIndex];
            if (state == null || state.tubeView == null || state.balls == null)
            {
                continue;
            }

            for (int ballIndex = 0; ballIndex < state.balls.Count; ballIndex++)
            {
                BallView ball = state.balls[ballIndex];
                if (ball == null)
                {
                    continue;
                }

                dropBalls.Add(new IntroDropBallState
                {
                    tubeView = state.tubeView,
                    ball = ball,
                    ballIndex = ballIndex,
                    slotPosition = state.tubeView.GetSlotWorldPosition(ballIndex)
                });
            }
        }

        dropBalls.Sort((left, right) =>
        {
            int xCompare = left.slotPosition.x.CompareTo(right.slotPosition.x);
            if (xCompare != 0)
            {
                return xCompare;
            }

            int yCompare = left.slotPosition.y.CompareTo(right.slotPosition.y);
            if (yCompare != 0)
            {
                return yCompare;
            }

            return left.ballIndex.CompareTo(right.ballIndex);
        });

        return dropBalls;
    }

    private Vector3 GetIntroDropStartPosition(TubeView tubeView, Vector3 slotPosition)
    {
        Transform mouthAnchor = tubeView != null ? tubeView.TubeMouthAnchor : null;
        if (mouthAnchor == null)
        {
            return slotPosition + Vector3.up * ScaleGameRootDistance(introDropSpawnHeight);
        }

        Vector3 startPosition = slotPosition;
        startPosition.y = mouthAnchor.position.y + ScaleGameRootDistance(introDropSpawnHeight);
        return startPosition;
    }

    private bool TryGetPressedScreenPosition(out Vector2 screenPosition)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                screenPosition = touch.position;
                return true;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        screenPosition = default;
        return false;
    }

    private bool TryRaycastTube(Vector2 screenPosition, out TubeView tubeView)
    {
        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        tubeView = null;
        if (cameraToUse == null) return false;
        Vector2 worldPosition = cameraToUse.ScreenToWorldPoint(screenPosition);
        RaycastHit2D[] hits = Physics2D.RaycastAll(worldPosition, Vector2.zero);
        TubeView fallback = null;
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null) continue;
            TubeView candidate = hit.collider.GetComponent<TubeView>();
            if (candidate == null) candidate = hit.collider.GetComponentInParent<TubeView>();
            if (candidate == null) continue;
            if (fallback == null) fallback = candidate;
            if (!IsTubeTransitionActive(candidate) && !IsTubeSelectionLocked(candidate))
            {
                tubeView = candidate;
                return true;
            }
        }
        tubeView = fallback;
        return tubeView != null;
    }

    private void HandleTubeClicked(TubeView clickedView)
    {
        if (clickedView == null || IsTubeTransitionActive(clickedView))
        {
            return;
        }

        if (!tubeModels.TryGetValue(clickedView, out TubeModel clickedModel))
        {
            return;
        }

        if (currentSelection == null)
        {
            if (clickedView.IsLongTube) return;
            if (clickedModel.CanBeSelected && !IsTubeSelectionLocked(clickedView))
            {
                BeginSelectTube(clickedView, clickedModel);
            }

            return;
        }

        if (clickedView == currentSelection.sourceView)
        {
            SelectionState selectionToDrop = currentSelection;
            currentSelection = null;
            BeginDropSelection(selectionToDrop);
            return;
        }

        MoveEvaluation move = currentSelection.sourceModel.EvaluateMoveTo(clickedModel);
        if (move.IsValid)
        {
            SelectionState selectionToMove = currentSelection;
            currentSelection = null;
            StopTubeTransition(selectionToMove.sourceView);
            PreparedMoveState preparedMove = PrepareMove(selectionToMove, clickedView, clickedModel, move);
            StartMoveTransition(preparedMove);
            return;
        }

        if (clickedView.IsLongTube) return;

        SelectionState selectionToSwitch = currentSelection;
        currentSelection = null;
        BeginSwitchSelection(selectionToSwitch, clickedView, clickedModel);
    }

    private void BeginSelectTube(TubeView sourceView, TubeModel sourceModel)
    {
        if (sourceView == null
            || sourceView.IsLongTube
            || sourceModel == null
            || !sourceModel.CanBeSelected
            || IsTubeSelectionLocked(sourceView))
        {
            return;
        }

        InterruptFloatingSelectionsExcept(sourceView);
        StopTubeTransition(sourceView);

        SelectionState selection = CreateSelection(sourceView, sourceModel);
        currentSelection = selection;
        sourceView.SetSelected(true);
        PlaySfxSafe("点击");
        StartTubeTransition(sourceView, PlaySelectTubeRoutine(selection));
    }

    private IEnumerator PlaySelectTubeRoutine(SelectionState selection)
    {
        if (selection == null || selection.sourceView == null || selection.sourceModel == null)
        {
            yield break;
        }

        int selectedCount = selection.sourceModel.GetTopRunCount();
        List<BallView> selectedBalls = selection.sourceView.GetTopBallViews(selectedCount);
        int topSlotIndex = selection.sourceModel.Count - selectedCount;
        float resolvedLiftHeight = ResolveSelectionLiftHeight(selection.sourceView, topSlotIndex);

        Sequence sequence = DOTween.Sequence();
        for (int i = 0; i < selectedBalls.Count; i++)
        {
            BallView ball = selectedBalls[i];
            if (ball == null)
            {
                continue;
            }

            ball.SetMoveGlow(true);
            ball.SetSortingOrder(selection.sourceView.GetSelectedBallSortingOrder(selectionSortingBoost));

            int slotIndex = selection.sourceModel.Count - 1 - i;
            Vector3 slotPosition = selection.sourceView.GetSlotWorldPosition(slotIndex);
            Vector3 liftedPosition = slotPosition + Vector3.up * resolvedLiftHeight;
            sequence.Insert(0f, ball.AnimateToWithSelectionBounce(
                liftedPosition,
                Mathf.Max(0.01f, selectionTweenDuration),
                Ease.OutQuad));
        }

        if (sequence.IsActive())
        {
            yield return sequence.WaitForCompletion();
        }
    }

    private float ResolveSelectionLiftHeight(TubeView sourceView, int topSlotIndex)
    {
        if (sourceView == null)
        {
            return Mathf.Max(0f, ScaleGameRootDistance(selectionLiftHeight));
        }

        Vector3 topSlotPosition = sourceView.GetSlotWorldPosition(topSlotIndex);
        Transform mouthAnchor = sourceView.TubeMouthAnchor;
        float targetTopBallY = mouthAnchor != null
            ? mouthAnchor.position.y + ScaleGameRootDistance(selectionBottomBallMouthOffset)
            : topSlotPosition.y + ScaleGameRootDistance(selectionLiftHeight);

        float liftHeight = targetTopBallY - topSlotPosition.y;
        return Mathf.Max(Mathf.Max(0f, ScaleGameRootDistance(selectionLiftHeight)), liftHeight);
    }

    private void BeginDropSelection(SelectionState selection)
    {
        if (selection == null)
        {
            return;
        }

        StopTubeTransition(selection.sourceView);
        selection.sourceView.SetSelected(false);
        StartTubeTransition(selection.sourceView, AnimateSelectionReturnRoutine(selection));
    }

    private void BeginSwitchSelection(SelectionState previousSelection, TubeView nextView, TubeModel nextModel)
    {
        if (previousSelection == null)
        {
            return;
        }

        if (nextView == null
            || nextModel == null
            || !nextModel.CanBeSelected
            || IsTubeSelectionLocked(nextView))
        {
            BeginDropSelection(previousSelection);
            return;
        }

        BeginDropSelection(previousSelection);
        BeginSelectTube(nextView, nextModel);
    }

    private IEnumerator AnimateSelectionReturnRoutine(SelectionState selection)
    {
        if (selection == null)
        {
            yield break;
        }

        if (selection.sourceView == null || selection.sourceModel == null)
        {
            yield break;
        }

        int selectedCount = selection.sourceModel.GetTopRunCount();
        List<BallView> selectedBalls = selection.sourceView.GetTopBallViews(selectedCount);
        Sequence sequence = DOTween.Sequence();
        for (int i = 0; i < selectedBalls.Count; i++)
        {
            BallView ball = selectedBalls[i];
            if (ball == null)
            {
                continue;
            }

            ball.SetSortingOrder(selection.sourceView.BallSortingOrder);
            int slotIndex = selection.sourceModel.Count - 1 - i;
            Vector3 slotPosition = selection.sourceView.GetSlotWorldPosition(slotIndex);
            sequence.Insert(0f, ball.AnimateToNoBounce(
                slotPosition,
                Mathf.Max(0.01f, selectionTweenDuration),
                Ease.InQuad));

            Tween fadeTween = ball.FadeOutSelectedVisual();
            if (fadeTween != null)
            {
                sequence.Insert(0f, fadeTween);
            }
        }

        if (sequence.IsActive())
        {
            yield return sequence.WaitForCompletion();
        }
    }

    private IEnumerator ExecuteMoveRoutine(PreparedMoveState preparedMove)
    {
        if (preparedMove == null
            || preparedMove.sourceView == null
            || preparedMove.targetView == null
            || preparedMove.move.TransferCount <= 0)
        {
            yield break;
        }

        preparedMove.sourceView.SetSelected(false);

        Sequence batchSequence = DOTween.Sequence();
        if (preparedMove.movedBalls.Count > 0)
        {
            for (int i = 0; i < preparedMove.movedBalls.Count; i++)
            {
                BallView ball = preparedMove.movedBalls[i];
                Vector3 targetSlotPosition = preparedMove.targetView.GetSlotWorldPosition(preparedMove.targetStartCount + i);
                float sourceExitY = preparedMove.sourceView.TubeMouthAnchor.position.y + ScaleGameRootDistance(transferHoverHeight);
                float targetHoverY = preparedMove.targetView.TubeMouthAnchor.position.y + ScaleGameRootDistance(
                    preparedMove.targetView.IsLongTube ? longTubeTransferHoverHeight : transferHoverHeight);
                float ballTravelDuration = preparedMove.targetView.IsLongTube ? longTubeTravelDuration : travelDuration;
                float ballDropDuration = preparedMove.targetView.IsLongTube ? longTubeDropDuration : dropDuration;
                Vector3 sourceExitPosition = new Vector3(
                    ball.transform.position.x,
                    sourceExitY,
                    ball.transform.position.z);
                Vector3 targetHoverPosition = new Vector3(
                    targetSlotPosition.x,
                    targetHoverY,
                    targetSlotPosition.z);

                float ballRaiseDuration = raiseDuration;

                Sequence transferSequence = ball.CreateTransferSequence(
                    sourceExitPosition,
                    targetHoverPosition,
                    targetSlotPosition,
                    ballRaiseDuration,
                    ballTravelDuration,
                    ballDropDuration,
                    ScaleGameRootDistance(transferArcHeight));
                transferSequence.OnComplete(() => movingBalls.Remove(ball));

                float startAt = i * moveStartInterval;
                int targetSortingOrder = preparedMove.targetView.BallSortingOrder;
                batchSequence.Insert(startAt, transferSequence);
                batchSequence.InsertCallback(
                    startAt + ballRaiseDuration,
                    () =>
                    {
                        if (ball != null)
                        {
                            ball.FadeOutSelectedVisual(false);
                            ball.SetSortingOrder(targetSortingOrder);
                        }
                    });
            }
        }

        if (preparedMove.overflowBalls.Count > 0)
        {
            for (int i = 0; i < preparedMove.overflowBalls.Count; i++)
            {
                BallView ball = preparedMove.overflowBalls[i];
                if (ball == null)
                {
                    continue;
                }

                ball.SetSortingOrder(preparedMove.sourceView.BallSortingOrder);
                int slotIndex = GetRuntimeBallSlotIndex(preparedMove.sourceView, ball);
                Tween fadeTween = ball.FadeOutSelectedVisual();
                if (fadeTween != null)
                {
                    batchSequence.Insert(0f, fadeTween);
                }
                if (slotIndex >= 0)
                {
                    Vector3 slotPosition = preparedMove.sourceView.GetSlotWorldPosition(slotIndex);
                    batchSequence.Insert(0f, ball.AnimateToNoBounce(
                        slotPosition,
                        Mathf.Max(0.01f, selectionTweenDuration),
                        Ease.InQuad).OnComplete(() => movingBalls.Remove(ball)));
                }
            }
        }

        if (batchSequence.IsActive())
        {
            yield return batchSequence.WaitForCompletion();
        }

        if (preparedMove.targetCompletedByMove)
        {
            // 满管后的完成动画独立播放，不阻塞后续操作。
            Sequence completionSequence = preparedMove.targetView.PlayCompletedAnimation(
                preparedMove.move.Color,
                ResolveCompletedLazhuTint(preparedMove.move.Color),
                ResolveCompletedLazhuSprite(preparedMove.targetView, preparedMove.move.Color));
            if (preparedMove.targetView.IsLongTube)
            {
                if (completionSequence != null && completionSequence.IsActive())
                    completionSequence.OnComplete(ShowEndPanel);
                else
                    ShowEndPanel();
            }
        }

        if (preparedMove.puzzleSolvedByMove && !puzzleSolvedInvoked)
        {
            puzzleSolvedInvoked = true;
            onPuzzleSolved?.Invoke();
        }
    }

    private void ShowEndPanel()
    {
        if (endPanel == null)
        {
            Debug.LogError("未找到 EndPanel，请检查 Canvas 下的面板组件。", this);
            return;
        }
        endPanel.transform.SetAsLastSibling();
        endPanel.gameObject.SetActive(true);
    }

    private PreparedMoveState PrepareMove(
        SelectionState selection,
        TubeView targetView,
        TubeModel targetModel,
        MoveEvaluation move)
    {
        PreparedMoveState preparedMove = new PreparedMoveState
        {
            sourceView = selection.sourceView,
            targetView = targetView,
            move = move,
            movedBalls = new List<BallView>(move.TransferCount),
            overflowBalls = new List<BallView>(move.OverflowCount)
        };

        List<BallView> selectedBalls = GetMoveOrderedTopBalls(selection.sourceView, targetView, move.SelectedCount);
        for (int i = 0; i < selectedBalls.Count; i++)
        {
            if (i < move.TransferCount)
            {
                preparedMove.movedBalls.Add(selectedBalls[i]);
            }
            else
            {
                preparedMove.overflowBalls.Add(selectedBalls[i]);
            }
        }

        preparedMove.targetStartCount = targetView.RuntimeBallViews.Count;

        selection.sourceView.RemoveRuntimeBalls(preparedMove.movedBalls);
        targetView.AddRuntimeBalls(preparedMove.movedBalls, false);

        foreach (BallView ball in preparedMove.movedBalls) movingBalls.Add(ball);
        foreach (BallView ball in preparedMove.overflowBalls) movingBalls.Add(ball);
        selection.sourceModel.RemoveTop(move.TransferCount);
        bool targetWasCompleted = targetModel.IsCompleted;
        targetModel.AddTop(move.Color, move.TransferCount);
        preparedMove.targetCompletedByMove = !targetWasCompleted && targetModel.IsCompleted;
        if (preparedMove.targetCompletedByMove)
        {
            RecordCompletedTube(targetView);
        }

        preparedMove.puzzleSolvedByMove = CheckWinCondition();
        if (preparedMove.puzzleSolvedByMove)
        {
            GameConfig.Instance?.AddTargetLevelCount();
            gamePhase = GamePhase.Win;
        }

        AddPendingOutgoing(selection.sourceView);
        AddPendingIncoming(targetView);

        return preparedMove;
    }

    private static int GetRuntimeBallSlotIndex(TubeView tubeView, BallView ball)
    {
        if (tubeView == null || ball == null)
        {
            return -1;
        }

        IReadOnlyList<BallView> runtimeBalls = tubeView.RuntimeBallViews;
        for (int i = 0; i < runtimeBalls.Count; i++)
        {
            if (runtimeBalls[i] == ball)
            {
                return i;
            }
        }

        return -1;
    }

    private SelectionState CreateSelection(TubeView sourceView, TubeModel sourceModel)
    {
        return new SelectionState
        {
            sourceView = sourceView,
            sourceModel = sourceModel
        };
    }

    private Color ResolveCompletedLazhuTint(BallColorType completedColor)
    {
        if (ballVisualPalette != null
            && ballVisualPalette.TryGetVisual(completedColor, out BallVisualPalette.BallVisualEntry entry)
            && entry != null)
        {
            return entry.tint;
        }

        return Color.white;
    }

    private static List<BallView> GetMoveOrderedTopBalls(TubeView sourceView, TubeView targetView, int count)
    {
        List<BallView> balls = sourceView.GetTopBallViews(count);
        float dx = targetView.transform.position.x - sourceView.transform.position.x;
        float dy = targetView.transform.position.y - sourceView.transform.position.y;
        bool primarilyVertical = Mathf.Abs(dy) > Mathf.Abs(dx);
        bool movingLeft = dx < 0f;

        balls.Sort((left, right) =>
        {
            if (left == null && right == null) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            float yDelta = right.transform.position.y - left.transform.position.y;
            if (Mathf.Abs(yDelta) > 0.01f)
            {
                return yDelta > 0f ? 1 : -1;
            }

            int xCompare = left.transform.position.x.CompareTo(right.transform.position.x);
            return primarilyVertical || movingLeft ? xCompare : -xCompare;
        });

        return balls;
    }

    private void RecordTubeClick()
    {
        if (GameConfig.Instance == null)
        {
            return;
        }

        GameConfig.Instance.AddTargetStepCount();
        GameConfig.Instance.OpRecord();
    }

    private void RecordCompletedTube(TubeView tubeView)
    {
        if (tubeView != null && GameConfig.Instance != null)
            GameConfig.Instance.OnTubeCompleted();
    }

    private bool CheckWinCondition()
    {
        int completedCount = 0;
        int emptyCount = 0;

        foreach (TubeModel model in tubeModels.Values)
        {
            if (model.IsCompleted)
            {
                completedCount++;
            }

            if (model.IsEmpty)
            {
                emptyCount++;
            }
        }

        return completedCount == requiredCompletedTubeCount
               && emptyCount == tubeModels.Count - requiredCompletedTubeCount;
    }

    private void InterruptFloatingSelectionsExcept(TubeView exceptView)
    {
        if (currentSelection == null || currentSelection.sourceView == exceptView)
        {
            return;
        }

        SelectionState interruptedSelection = currentSelection;
        currentSelection = null;
        BeginDropSelection(interruptedSelection);
    }

    private bool IsTubeSelectionLocked(TubeView tubeView)
    {
        if (tubeView == null || (GetPendingMoveCount(pendingIncomingMoves, tubeView) <= 0
            && GetPendingMoveCount(pendingOutgoingMoves, tubeView) <= 0)) return false;
        if (!tubeModels.TryGetValue(tubeView, out TubeModel model) || model == null) return true;
        foreach (BallView ball in tubeView.GetTopBallViews(model.GetTopRunCount()))
            if (ball != null && movingBalls.Contains(ball)) return true;
        return false;
    }

    private bool IsTubeTransitionActive(TubeView tubeView)
    {
        return tubeView != null && tubeTransitionCoroutines.ContainsKey(tubeView);
    }

    private void StartMoveTransition(PreparedMoveState preparedMove)
    {
        if (preparedMove == null)
        {
            return;
        }

        StartCoroutine(RunMoveTransition(preparedMove));
    }

    private IEnumerator RunMoveTransition(PreparedMoveState preparedMove)
    {
        try
        {
            yield return ExecuteMoveRoutine(preparedMove);
        }
        finally
        {
            foreach (BallView ball in preparedMove.movedBalls) movingBalls.Remove(ball);
            foreach (BallView ball in preparedMove.overflowBalls) movingBalls.Remove(ball);
            RemovePendingOutgoing(preparedMove.sourceView);
            RemovePendingIncoming(preparedMove.targetView);
        }
    }

    private void StartTubeTransition(TubeView tubeView, IEnumerator routine)
    {
        if (tubeView == null || routine == null)
        {
            return;
        }

        StopTubeTransition(tubeView);
        tubeTransitionCoroutines[tubeView] = StartCoroutine(RunTubeTransition(tubeView, routine));
    }

    private IEnumerator RunTubeTransition(TubeView tubeView, IEnumerator routine)
    {
        try
        {
            yield return routine;
        }
        finally
        {
            if (tubeView != null
                && tubeTransitionCoroutines.TryGetValue(tubeView, out Coroutine activeCoroutine)
                && activeCoroutine != null)
            {
                tubeTransitionCoroutines.Remove(tubeView);
            }
        }
    }

    private void StopTubeTransition(TubeView tubeView)
    {
        if (tubeView == null)
        {
            return;
        }

        if (tubeTransitionCoroutines.TryGetValue(tubeView, out Coroutine coroutine) && coroutine != null)
        {
            StopCoroutine(coroutine);
        }

        tubeTransitionCoroutines.Remove(tubeView);
    }

    private void StopAllTubeTransitions()
    {
        foreach (Coroutine coroutine in tubeTransitionCoroutines.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }

        tubeTransitionCoroutines.Clear();
    }

    private void AddPendingIncoming(TubeView tubeView)
    {
        AddPendingMove(pendingIncomingMoves, tubeView);
    }

    private void RemovePendingIncoming(TubeView tubeView)
    {
        RemovePendingMove(pendingIncomingMoves, tubeView);
    }

    private void AddPendingOutgoing(TubeView tubeView)
    {
        AddPendingMove(pendingOutgoingMoves, tubeView);
    }

    private void RemovePendingOutgoing(TubeView tubeView)
    {
        RemovePendingMove(pendingOutgoingMoves, tubeView);
    }

    private static void AddPendingMove(Dictionary<TubeView, int> counter, TubeView tubeView)
    {
        if (tubeView == null)
        {
            return;
        }

        counter.TryGetValue(tubeView, out int currentCount);
        counter[tubeView] = currentCount + 1;
    }

    private static void RemovePendingMove(Dictionary<TubeView, int> counter, TubeView tubeView)
    {
        if (tubeView == null)
        {
            return;
        }

        if (!counter.TryGetValue(tubeView, out int currentCount))
        {
            return;
        }

        if (currentCount <= 1)
        {
            counter.Remove(tubeView);
            return;
        }

        counter[tubeView] = currentCount - 1;
    }

    private static int GetPendingMoveCount(Dictionary<TubeView, int> counter, TubeView tubeView)
    {
        if (tubeView == null)
        {
            return 0;
        }

        return counter.TryGetValue(tubeView, out int count) ? count : 0;
    }
}
