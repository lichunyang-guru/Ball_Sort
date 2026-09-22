using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class GameController : MonoBehaviour
{
    [Serializable]
    public class WaterColumnEntry
    {
        public BallColorType color = BallColorType.Yellow;
        [Tooltip("上下截面中线之间的有效高度；Body 额外延伸半个截面用于覆盖，每管总高度不能超过 16。")]
        [Min(1)] public int length = 2;
    }

    [Serializable]
    public class TubeLevelEntry
    {
        [Tooltip("本关中对应的试管视图对象。")]
        public TubeView tubeView;
        [Tooltip("从底部到顶部配置颜色和长度，总长度不能超过试管容量；空管不添加条目。")]
        public List<WaterColumnEntry> columns = new List<WaterColumnEntry>();

        public List<BallColorType> CreateInitialBalls(int capacity)
        {
            List<BallColorType> result = new List<BallColorType>(capacity);
            foreach (WaterColumnEntry column in columns)
            {
                if (column == null || column.color == BallColorType.None || column.length < 1)
                {
                    throw new InvalidOperationException("水柱必须配置有效颜色，高度必须是正整数。");
                }
                if (column.length > capacity - result.Count)
                {
                    throw new InvalidOperationException($"水柱总高度不能超过试管容量 {capacity}。");
                }
                for (int i = 0; i < column.length; i++)
                {
                    result.Add(column.color);
                }
            }
            return result;
        }
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
        public int length;
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
    [SerializeField] private string introDropSfxName = "下落";
    [Tooltip("开局落球时，每隔几颗球补一次音效。")]
    [SerializeField] private int introDropSfxBallInterval = 2;
    [Tooltip("开局落球音效两次播放之间的最短间隔。")]
    [SerializeField] private float introDropSfxMinInterval = 0.06f;

    [Header("Selection Animation")]
    [Tooltip("当指定高度没有高于当前槽位时，选中小球使用的兜底上抬高度。")]
    [SerializeField] private float selectionLiftHeight = 0.35f;
    [Tooltip("选中后最顶部小球中心停在管口上方的高度。")]
    [SerializeField] private float selectionTopBallMouthOffset = 0.15f;
    [Tooltip("选中/取消选中时，小球抬起或回槽位的动画时长。")]
    [SerializeField] private float selectionTweenDuration = 0.15f;
    [Tooltip("选中小球临时提升的渲染排序值，越大越靠前。")]
    [SerializeField] private int selectionSortingBoost = 100;

    [Header("Move Animation")]
    [Tooltip("倒球时，小球保持当前横向位置并垂直抬起的动画时长。")]
    [SerializeField] private float raiseDuration = 0.18f;
    [Tooltip("小球横向移动前/落下前，距离管口上方的高度。")]
    [SerializeField] private float transferHoverHeight = 0.35f;
    [Tooltip("小球从源管口上方飞到目标管口上方时的弧线高度。")]
    [SerializeField] private float transferArcHeight = 0.8f;
    [Tooltip("小球从源试管移动到目标试管管口的动画时长。")]
    [SerializeField] private float travelDuration = 0.2f;
    [Tooltip("小球从目标管口落入目标槽位的动画时长。")]
    [SerializeField] private float dropDuration = 0.15f;

    [Tooltip("前一段出发后，等待多少秒开始抬起下一段同色水柱；不等待前段落地，不改变动画速度。")]
    [Min(0f)]
    [SerializeField] private float followInterval = 0.12f;

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
    private bool boardEntryInProgress;
    private int completedTubeAnalyticsCount;

    public void SetBoardEntryInProgress(bool inProgress)
    {
        boardEntryInProgress = inProgress;
    }

    private void Start()
    {
        BuildBoard();
    }

    private void Update()
    {
        if (gamePhase == GamePhase.Win || introDropInProgress || boardEntryInProgress)
        {
            return;
        }

        if (!TryGetPressedScreenPosition(out Vector2 screenPosition))
        {
            return;
        }

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
        completedTubeAnalyticsCount = 0;

        List<IntroDropState> introDropStates = new List<IntroDropState>();
        for (int i = 0; i < levelEntries.Count; i++)
        {
            TubeLevelEntry entry = levelEntries[i];
            if (entry == null || entry.tubeView == null)
            {
                continue;
            }

            StopTubeTransition(entry.tubeView);
            entry.tubeView.CollectSlotAnchors();
            entry.tubeView.ResetVisualState();
            entry.tubeView.ClearRuntimeBalls();

            if (!entry.tubeView.ConfigureWaterHeightLayout())
            {
                Debug.LogError($"{entry.tubeView.name} 没有配置内腔遮罩，无法计算水柱高度。", entry.tubeView);
                continue;
            }

            TubeModel model = new TubeModel(entry.tubeView.name, entry.tubeView.Capacity);

            try
            {
                model.SetBalls(entry.CreateInitialBalls(entry.tubeView.Capacity));
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
                ballView.Initialize(model.Balls[ballIndex], ballVisualPalette);
                entry.tubeView.InitializeWaterBall(ballView);
                Vector3 slotPosition = entry.tubeView.GetSlotWorldPosition(ballIndex);
                ballView.SnapTo(shouldPlayIntroDrop
                    ? GetIntroDropStartPosition(entry.tubeView, slotPosition)
                    : slotPosition);
                spawnedBalls.Add(ballView);
            }

            entry.tubeView.SetRuntimeBalls(spawnedBalls);
            entry.tubeView.RefreshWaterColumns();

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
                entry.tubeView.SetCompletedVisualImmediate(completedColor, ResolveCompletedLazhuTint(completedColor));
            }
        }

        if (introDropStates.Count > 0)
        {
            introDropInProgress = true;
            StartCoroutine(PlayIntroDropRoutine(introDropStates));
        }
    }

    public List<Color> GetLevelWaterColors()
    {
        List<Color> colors = new List<Color>();
        if (ballVisualPalette == null) return colors;
        foreach (TubeLevelEntry entry in levelEntries)
        {
            if (entry == null || entry.tubeView == null) continue;
            foreach (WaterColumnEntry column in entry.columns)
            {
                if (column != null && column.length > 0
                    && ballVisualPalette.TryGetVisual(column.color, out BallVisualPalette.BallVisualEntry visual)
                    && !colors.Contains(visual.tint))
                {
                    colors.Add(visual.tint);
                }
            }
        }
        return colors;
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
            ball.ShowWaterColumn(dropBall.length);
            ball.gameObject.SetActive(false);

            dropSequence.InsertCallback(startAt, () =>
            {
                if (ball != null)
                {
                    ball.gameObject.SetActive(true);
                }
            });
            Sequence columnDrop = ball.CreateDropSequence(
                dropBall.slotPosition, Mathf.Max(0.01f, introDropDuration));
            BindLandingSurfaceColor(columnDrop, dropBall.tubeView, dropBall.ballIndex, ball,
                0f, Mathf.Max(0.01f, introDropDuration));
            dropSequence.Insert(startAt, columnDrop);

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
            if (state != null && state.tubeView != null)
            {
                state.tubeView.RefreshWaterColumns();
            }
            if (state != null
                && state.model != null
                && state.model.IsCompleted
                && state.tubeView != null)
            {
                BallColorType completedColor = state.model.TopColor;
                state.tubeView.SetCompletedVisualImmediate(completedColor, ResolveCompletedLazhuTint(completedColor));
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

                int length = state.tubeView.GetWaterColumnLength(ballIndex);

                dropBalls.Add(new IntroDropBallState
                {
                    tubeView = state.tubeView,
                    ball = ball,
                    ballIndex = ballIndex,
                    length = length,
                    slotPosition = state.tubeView.GetSlotWorldPosition(ballIndex)
                });
                ballIndex += length - 1;
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
            return slotPosition + Vector3.up * introDropSpawnHeight;
        }

        Vector3 startPosition = slotPosition;
        startPosition.y = mouthAnchor.position.y + introDropSpawnHeight;
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
        if (cameraToUse == null)
        {
            tubeView = null;
            return false;
        }

        Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(screenPosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.zero);
        if (!hit.collider)
        {
            tubeView = null;
            return false;
        }

        tubeView = hit.collider.GetComponent<TubeView>();
        if (tubeView == null)
        {
            tubeView = hit.collider.GetComponentInParent<TubeView>();
        }

        return tubeView != null;
    }

    private void HandleTubeClicked(TubeView clickedView)
    {
        if (clickedView == null)
        {
            return;
        }

        if (!tubeModels.TryGetValue(clickedView, out TubeModel clickedModel))
        {
            return;
        }

        if (currentSelection == null)
        {
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

        // 接收中的管子已经预占容量，可以继续接收；出水中的管子仍由跟随流程占用。
        if (GetPendingMoveCount(pendingOutgoingMoves, clickedView) > 0) return;
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

        SelectionState selectionToSwitch = currentSelection;
        currentSelection = null;
        BeginSwitchSelection(selectionToSwitch, clickedView, clickedModel);
    }

    private void BeginSelectTube(TubeView sourceView, TubeModel sourceModel)
    {
        if (sourceView == null
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
        StartTubeTransition(sourceView, PlaySelectTubeRoutine(selection));
    }

    private IEnumerator PlaySelectTubeRoutine(SelectionState selection)
    {
        if (selection == null || selection.sourceView == null || selection.sourceModel == null)
        {
            yield break;
        }

        PlaySfxSafe("点击");

        int selectedCount = Mathf.Min(TubeModel.TransferHeight, selection.sourceModel.GetTopRunCount());
        List<BallView> selectedBalls = selection.sourceView.GetTopBallViews(selectedCount);
        selection.sourceView.RefreshWaterColumns(selection.sourceModel.Count - selectedCount);
        int topSlotIndex = selection.sourceModel.Count - 1;
        float resolvedLiftHeight = ResolveSelectionLiftHeight(selection.sourceView, topSlotIndex);
        Vector3 startPosition = selection.sourceView.GetSlotWorldPosition(topSlotIndex - selectedCount + 1);
        Vector3 liftedPosition = selection.sourceView.GetSlotWorldPosition(topSlotIndex)
            + Vector3.up * resolvedLiftHeight;

        Sequence sequence = DOTween.Sequence().SetTarget(selection.sourceView)
            .SetLink(selection.sourceView.gameObject);
        for (int i = 0; i < selectedBalls.Count; i++)
        {
            BallView ball = selectedBalls[i];
            if (ball == null)
            {
                continue;
            }

            int slotIndex = selection.sourceModel.Count - 1 - i;
            if (i > 0)
            {
                ball.HideVisuals();
                continue;
            }

            ball.SnapTo(startPosition);
            ball.BeginLiftVisual();
            ball.SetSortingOrder(selection.sourceView.GetSelectedBallSortingOrder(selectionSortingBoost, slotIndex));
            sequence.Insert(0f, ball.AnimateToNoBounce(
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
            return Mathf.Max(0f, selectionLiftHeight);
        }

        Vector3 topSlotPosition = sourceView.GetSlotWorldPosition(topSlotIndex);
        Transform mouthAnchor = sourceView.TubeMouthAnchor;
        float targetTopBallY = mouthAnchor != null
            ? mouthAnchor.position.y + selectionTopBallMouthOffset
            : topSlotPosition.y + selectionLiftHeight;

        float liftHeight = targetTopBallY - topSlotPosition.y;
        return Mathf.Max(Mathf.Max(0f, selectionLiftHeight), liftHeight);
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

        int selectedCount = Mathf.Min(TubeModel.TransferHeight, selection.sourceModel.GetTopRunCount());
        List<BallView> selectedBalls = selection.sourceView.GetTopBallViews(selectedCount);
        Sequence sequence = DOTween.Sequence().SetTarget(selection.sourceView)
            .SetLink(selection.sourceView.gameObject);
        int bottomSlotIndex = selection.sourceModel.Count - selectedCount;
        sequence.Append(CreateColumnReturnSequence(
            selection.sourceView, selectedBalls, bottomSlotIndex));

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

        Sequence batchSequence = DOTween.Sequence().SetTarget(this).SetLink(gameObject);
        BallView movingColumn = preparedMove.movedBalls[0];
        Vector3 liftedPosition = movingColumn.transform.position;
        for (int i = 1; i < preparedMove.movedBalls.Count; i++)
        {
            preparedMove.movedBalls[i].HideVisuals();
        }

        int targetBottomSlot = preparedMove.targetStartCount;
        movingColumn.SetWaterColumnHeight(preparedMove.movedBalls.Count);
        Vector3 targetSlotPosition = preparedMove.targetView.GetSlotWorldPosition(targetBottomSlot);
        Vector3 sourceExitPosition = new Vector3(
            liftedPosition.x,
            preparedMove.sourceView.TubeMouthAnchor.position.y + transferHoverHeight,
            liftedPosition.z);
        Vector3 targetHoverPosition = new Vector3(
            targetSlotPosition.x,
            preparedMove.targetView.TubeMouthAnchor.position.y + transferHoverHeight,
            targetSlotPosition.z);

        Sequence transfer = movingColumn.CreateTransferSequence(
            sourceExitPosition, targetHoverPosition, targetSlotPosition,
            raiseDuration, travelDuration, dropDuration, transferArcHeight);
        BindLandingSurfaceColor(transfer, preparedMove.targetView, targetBottomSlot,
            movingColumn, Mathf.Max(0f, raiseDuration) + Mathf.Max(0f, travelDuration), dropDuration);
        batchSequence.Insert(0f, transfer);
        batchSequence.InsertCallback(Mathf.Max(0f, raiseDuration) + Mathf.Max(0f, travelDuration), () =>
        {
            movingColumn.FadeOutSelectedVisual();
            movingColumn.SetSortingOrder(preparedMove.targetView.GetBallSortingOrder(targetBottomSlot));
        });

        if (batchSequence.IsActive())
        {
            yield return batchSequence.WaitForCompletion();
        }

        if (preparedMove.targetCompletedByMove)
        {
            // 满管后的完成动画独立播放，不阻塞后续操作。
            preparedMove.targetView.PlayCompletedAnimation(
                preparedMove.move.Color,
                ResolveCompletedLazhuTint(preparedMove.move.Color));
        }

        if (preparedMove.puzzleSolvedByMove && !puzzleSolvedInvoked)
        {
            puzzleSolvedInvoked = true;
            onPuzzleSolved?.Invoke();
        }
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
            movedBalls = selection.sourceView.GetTopBallViews(move.TransferCount)
        };

        preparedMove.targetStartCount = targetView.RuntimeBallViews.Count;

        selection.sourceView.RemoveRuntimeBalls(preparedMove.movedBalls);
        selection.sourceView.RefreshWaterColumns();
        targetView.AddRuntimeBalls(preparedMove.movedBalls, false);

        selection.sourceModel.RemoveTop(move.TransferCount);
        bool targetWasCompleted = targetModel.IsCompleted;
        targetModel.AddTop(move.Color, move.TransferCount);
        preparedMove.targetCompletedByMove = !targetWasCompleted && targetModel.IsCompleted;
        if (preparedMove.targetCompletedByMove)
        {
            RecordCompletedTube();
        }

        preparedMove.puzzleSolvedByMove = CheckWinCondition();
        if (preparedMove.puzzleSolvedByMove)
        {
            GameConfig.Instance?.AddTargetLevelCount();
            gamePhase = GamePhase.Win;
        }

        AddPendingIncoming(targetView);

        return preparedMove;
    }

    private Sequence CreateColumnReturnSequence(TubeView tubeView, List<BallView> balls, int bottomSlotIndex)
    {
        for (int i = 1; i < balls.Count; i++)
        {
            balls[i].HideVisuals();
        }

        BallView column = balls[0];
        column.SetWaterColumnHeight(balls.Count);
        column.SetSortingOrder(tubeView.GetBallSortingOrder(bottomSlotIndex));
        column.FadeOutSelectedVisual();
        Sequence drop = column.CreateDropSequence(
            tubeView.GetSlotWorldPosition(bottomSlotIndex),
            Mathf.Max(0.01f, selectionTweenDuration));
        drop.InsertCallback(0f, () => PlaySfxSafe("下落"));
        BindLandingSurfaceColor(drop, tubeView, bottomSlotIndex, column,
            0f, Mathf.Max(0.01f, selectionTweenDuration));
        return drop;
    }

    private void BindLandingSurfaceColor(Sequence sequence, TubeView tube,
        int bottomSlotIndex, BallView incoming, float dropStartTime, float fallDuration)
    {
        if (bottomSlotIndex <= 0) return;
        var balls = tube.RuntimeBallViews;
        int below = bottomSlotIndex - 1;
        BallColorType color = balls[below].ColorType;
        while (below > 0 && balls[below - 1].ColorType == color) below--;
        WaterBodyTopAnchor receivingSurface = balls[below].BodyTopAnchor;
        WaterBodyTopAnchor incomingSurface = incoming.BodyTopAnchor;
        if (receivingSurface == null || incomingSurface == null) return;

        // InQuad distance is time squared; trigger after 70% of the fall distance.
        float colorChangeTime = dropStartTime + Mathf.Max(0f, fallDuration) * Mathf.Sqrt(0.7f);
        sequence.InsertCallback(colorChangeTime, () =>
        {
            if (receivingSurface != null) receivingSurface.SetIncomingColor(incomingSurface);
        });
        // Restore after the reveal, and also when a transition is interrupted.
        TweenCallback restore = () =>
        {
            if (receivingSurface != null) receivingSurface.ClearIncomingColor(incomingSurface);
        };
        sequence.OnComplete(restore);
        sequence.OnKill(restore);
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

    private void RecordTubeClick()
    {
        if (GameConfig.Instance == null)
        {
            return;
        }

        GameConfig.Instance.AddTargetStepCount();
        GameConfig.Instance.OpRecord();
    }

    private void RecordCompletedTube()
    {
        completedTubeAnalyticsCount++;
        Luna.Unity.Analytics.LogEvent("tube_completed", completedTubeAnalyticsCount);

        if (GameConfig.Instance == null)
        {
            return;
        }

        GameConfig.Instance.AddTargetCompleteCount();
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
        return tubeView != null
               && (GetPendingMoveCount(pendingIncomingMoves, tubeView) > 0
                   || GetPendingMoveCount(pendingOutgoingMoves, tubeView) > 0);
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
        TubeView source = preparedMove.sourceView;
        TubeView target = preparedMove.targetView;
        BallColorType color = preparedMove.move.Color;
        AddPendingOutgoing(source);
        AddPendingIncoming(target);
        try
        {
            PreparedMoveState next = preparedMove;
            while (true)
            {
                StartCoroutine(RunMovingColumn(next));

                TubeModel sourceModel = tubeModels[source];
                TubeModel targetModel = tubeModels[target];
                if (sourceModel.IsEmpty || sourceModel.TopColor != color) break;
                MoveEvaluation nextMove = sourceModel.EvaluateMoveTo(targetModel);
                if (!nextMove.IsValid) break;

                // Launch the next segment while earlier segments are still in flight.
                if (followInterval > 0f) yield return new WaitForSeconds(followInterval);
                nextMove = sourceModel.EvaluateMoveTo(targetModel);
                if (!nextMove.IsValid) break;
                SelectionState following = CreateSelection(source, sourceModel);
                yield return PlaySelectTubeRoutine(following);
                // 抬起期间，其他源管可能已经占用目标的剩余容量。
                nextMove = sourceModel.EvaluateMoveTo(targetModel);
                if (!nextMove.IsValid)
                {
                    yield return AnimateSelectionReturnRoutine(following);
                    break;
                }
                next = PrepareMove(following, target, targetModel, nextMove);
            }
        }
        finally
        {
            // 所有跟随段出发后立即释放源管，飞行中的水柱已归目标管所有。
            RemovePendingOutgoing(source);
            RemovePendingIncoming(target);
        }
        RefreshTubeColumnsIfIdle(source);
        RefreshTubeColumnsIfIdle(target);
    }

    private IEnumerator RunMovingColumn(PreparedMoveState move)
    {
        try
        {
            yield return ExecuteMoveRoutine(move);
        }
        finally
        {
            RemovePendingIncoming(move.targetView);
        }
        // Do not regroup a target that still has incoming segments.
        RefreshTubeColumnsIfIdle(move.sourceView);
        RefreshTubeColumnsIfIdle(move.targetView);
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
        RefreshTubeColumnsIfIdle(tubeView);
    }

    private void RefreshTubeColumnsIfIdle(TubeView tubeView)
    {
        if (tubeView == null || IsTubeSelectionLocked(tubeView)
            || tubeTransitionCoroutines.ContainsKey(tubeView)
            || (currentSelection != null && currentSelection.sourceView == tubeView))
        {
            return;
        }
        tubeView.RefreshWaterColumns();
    }

    private void StopTubeTransition(TubeView tubeView)
    {
        if (tubeView == null)
        {
            return;
        }

        DOTween.Kill(tubeView);

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
