using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class GuidePanel : MonoBehaviour
{
    public enum GuideMode
    {
        Drag,
        TwoStepClick
    }

    [Tooltip("是否在 Start 时自动展示引导；关闭后可由外部在合适时机调用 StartGuide。")]
    public bool playOnStart = true;
    [Tooltip("引导模式：Drag 为拖拽手势，TwoStepClick 为两步点击提示。")]
    public GuideMode guideMode = GuideMode.Drag;
    [Tooltip("手势图标相对目标点的 UI X 轴偏移，正数向右。")]
    public float offsetX = 0f;
    [Tooltip("手势图标相对目标点的 UI Y 轴偏移。")]
    public float offsetY = 5f;
    [Tooltip("两步点击模式第一步额外偏移。")]
    public Vector2 firstStepOffset = Vector2.zero;
    [Tooltip("两步点击模式第二步额外偏移。")]
    public Vector2 secondStepOffset = Vector2.zero;
    [Tooltip("拖拽引导中手势从起点飞到终点的速度，单位为 UI 像素/秒。")]
    public float flySpeed = 200f;
    [Tooltip("手势图标对象，需要带 CanvasGroup 才能淡入淡出。")]
    public GameObject guideFinger;
    [Tooltip("用于把世界坐标转换为屏幕坐标的相机。")]
    public Camera mainCamera;
    [Tooltip("手势所在 Canvas 的 RectTransform，用于计算本地 UI 坐标。")]
    public RectTransform canvasRect;
    [Tooltip("引导点列表；拖拽模式使用前两个点，两步点击模式按当前步骤取点。")]
    public List<Transform> guidePoints = new List<Transform>();
    [Tooltip("拖拽引导模式下，玩家第一次按下屏幕时是否隐藏引导。")]
    public bool hideOnFirstPointerDown = true;

    [Header("Two Step Click Guide")]
    [Tooltip("两步点击模式第一步允许点击的目标；为空表示任意点击都有效。")]
    public List<GameObject> firstStepTargets = new List<GameObject>();
    [Tooltip("两步点击模式第二步允许点击的目标；为空表示任意点击都有效。")]
    public List<GameObject> secondStepTargets = new List<GameObject>();
    [Tooltip("两步点击模式中手势淡入的时长。")]
    public float showDuration = 0.2f;
    [Tooltip("两步点击模式中手势按下缩小的时长。")]
    public float shrinkDuration = 0.35f;
    [Tooltip("两步点击模式中手势淡出的时长。")]
    public float hideDuration = 0.3f;
    [Tooltip("手势循环播放之间的等待时间。")]
    public float loopInterval = 0.5f;
    [Tooltip("手势显示时的初始缩放。")]
    public float shownScale = 1f;
    [Tooltip("模拟点击按下时手势缩小到的缩放。")]
    public float shrinkScale = 0.75f;

    bool isGuideShow = false;
    Tween guideTween = null;
    private CanvasGroup guideCanvas = null;
    int guideTimerId = -1;
    int currentStep = 0;
    readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    void Start()
    {
        if (guideFinger == null)
        {
            return;
        }

        guideCanvas = guideFinger.GetComponent<CanvasGroup>();
        if (guideCanvas == null || guidePoints.Count < 2 || guidePoints[0] == null || guidePoints[1] == null)
        {
            return;
        }

        if (!playOnStart)
        {
            HideGuideImmediate();
            return;
        }

        StartGuide();
    }

    void Update()
    {
        if (!isGuideShow)
        {
            return;
        }

        if (guideMode == GuideMode.Drag)
        {
            if (hideOnFirstPointerDown && IsPointerDownThisFrame())
            {
                HideGuide();
            }
            return;
        }

        RefreshClickGuidePosition();

        if (TryGetPointerDownPosition(out Vector2 screenPosition))
        {
            HandlePointerDown(screenPosition);
        }
    }

    public void StartGuide()
    {
        if (guideFinger == null || guidePoints.Count < 2 || guidePoints[0] == null || guidePoints[1] == null)
        {
            return;
        }

        if (guideCanvas == null)
        {
            guideCanvas = guideFinger.GetComponent<CanvasGroup>();
        }
        if (guideCanvas == null)
        {
            return;
        }

        isGuideShow = true;
        currentStep = 0;

        if (guideMode == GuideMode.Drag)
        {
            ShowDragGuide(guidePoints[0], guidePoints[1]);
            return;
        }

        ShowClickGuideForCurrentStep();
    }

    public void ShowGuide(Transform fromTrans, Transform toTrans)
    {
        if (fromTrans == null || toTrans == null)
        {
            return;
        }

        if (guidePoints.Count < 2)
        {
            guidePoints = new List<Transform> { fromTrans, toTrans };
        }
        else
        {
            guidePoints[0] = fromTrans;
            guidePoints[1] = toTrans;
        }

        isGuideShow = true;
        currentStep = 0;

        if (guideMode == GuideMode.Drag)
        {
            ShowDragGuide(fromTrans, toTrans);
            return;
        }

        ShowClickGuideForCurrentStep();
    }

    private void ShowDragGuide(Transform fromTrans, Transform toTrans)
    {
        if (!isGuideShow || guideFinger == null || guideCanvas == null || mainCamera == null ||
            canvasRect == null || fromTrans == null || toTrans == null)
        {
            return;
        }

        Vector2 from = GetGuidePosition(fromTrans.position);
        Vector2 to = GetGuidePosition(toTrans.position);
        KillGuideTween();
        guideCanvas.alpha = 1f;
        guideFinger.transform.localScale = Vector3.one;
        guideFinger.transform.localPosition = from;
        guideFinger.SetActive(true);
        guideTween = guideFinger.transform.DOScale(Vector3.one * 0.75f, 0.35f).SetEase(Ease.InSine).OnComplete(() =>
        {
            guideTween = null;
            float flyTime = Vector2.Distance(from, to) / flySpeed;
            guideTween = guideFinger.transform.DOLocalMove(to, flyTime).SetEase(Ease.Linear).OnComplete(() =>
            {
                guideTween = null;
                guideTween = guideFinger.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutSine).OnComplete(() =>
                {
                    guideTween = null;
                    guideTween = guideCanvas.DOFade(0, 0.3f).OnComplete(() =>
                    {
                        guideTween = null;
                        guideTimerId = TimerMgr.Instance.CreateNewTimer(loopInterval, () =>
                        {
                            guideTimerId = -1;
                            ShowDragGuide(fromTrans, toTrans);
                        });
                    });
                });
            });
        });
    }

    private void ShowClickGuideForCurrentStep()
    {
        if (!isGuideShow || guideFinger == null || guideCanvas == null || guidePoints.Count < 2)
        {
            return;
        }

        Transform point = guidePoints[Mathf.Clamp(currentStep, 0, guidePoints.Count - 1)];
        if (point == null)
        {
            return;
        }

        Vector2 position = GetGuidePosition(point.position);
        KillGuideTween();
        guideCanvas.alpha = 1f;
        guideFinger.transform.localScale = Vector3.one * shownScale;
        guideFinger.transform.localPosition = position;
        guideFinger.SetActive(true);
        guideTween = guideFinger.transform
            .DOScale(Vector3.one * shrinkScale, Mathf.Max(0.01f, shrinkDuration))
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private Vector2 GetGuidePosition(Vector3 worldPos)
    {
        Vector2 position = WorldToCanvasPosition(worldPos);
        position.x += offsetX;
        position.y += offsetY;
        if (guideMode == GuideMode.TwoStepClick)
        {
            position += currentStep == 0 ? firstStepOffset : secondStepOffset;
        }

        return position;
    }

    private void RefreshClickGuidePosition()
    {
        if (guideMode != GuideMode.TwoStepClick || guideFinger == null || guidePoints.Count < 2)
        {
            return;
        }

        Transform point = guidePoints[Mathf.Clamp(currentStep, 0, guidePoints.Count - 1)];
        if (point == null)
        {
            return;
        }

        guideFinger.transform.localPosition = GetGuidePosition(point.position);
    }

    private Vector2 WorldToCanvasPosition(Vector3 worldPos)
    {
        if (mainCamera == null || canvasRect == null)
        {
            return Vector2.zero;
        }

        Vector2 screenPos = mainCamera.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, null, out Vector2 localPos);
        return localPos;
    }

    private void KillGuideTween()
    {
        if (guideTween != null)
        {
            guideTween.Kill();
            guideTween = null;
        }
        if (guideTimerId != -1)
        {
            TimerMgr.Instance.RemoveTimer(guideTimerId);
            guideTimerId = -1;
        }
    }

    public void HideGuide()
    {
        if (!isGuideShow)
        {
            return;
        }
        isGuideShow = false;

        KillGuideTween();
        if (guideFinger == null)
        {
            return;
        }
        if (guideCanvas == null)
        {
            guideCanvas = guideFinger.GetComponent<CanvasGroup>();
        }
        if (guideCanvas == null)
        {
            guideFinger.SetActive(false);
            return;
        }
        guideCanvas.DOFade(0, 0.5f).OnComplete(() =>
        {
            if (guideFinger != null)
            {
                guideFinger.SetActive(false);
            }
        });
    }

    public void HideGuideImmediate()
    {
        isGuideShow = false;
        KillGuideTween();
        if (guideFinger == null)
        {
            return;
        }

        if (guideCanvas == null)
        {
            guideCanvas = guideFinger.GetComponent<CanvasGroup>();
        }
        if (guideCanvas != null)
        {
            guideCanvas.alpha = 0f;
        }

        guideFinger.SetActive(false);
    }

    private void HandlePointerDown(Vector2 screenPosition)
    {
        if (guideMode != GuideMode.TwoStepClick)
        {
            return;
        }

        if (currentStep == 0)
        {
            if (IsClickValid(screenPosition, firstStepTargets))
            {
                currentStep = 1;
                ShowClickGuideForCurrentStep();
            }
            return;
        }

        if (currentStep == 1 && IsClickValid(screenPosition, secondStepTargets))
        {
            HideGuide();
        }
    }

    private bool IsPointerDownThisFrame()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                return true;
            }
        }

        return Input.GetMouseButtonDown(0);
    }

    private bool IsClickValid(Vector2 screenPosition, List<GameObject> validTargets)
    {
        if (validTargets == null || validTargets.Count == 0)
        {
            return true;
        }

        if (IsValidUiTargetClicked(screenPosition, validTargets))
        {
            return true;
        }

        return IsValidWorldTargetClicked(screenPosition, validTargets);
    }

    private bool IsValidUiTargetClicked(Vector2 screenPosition, List<GameObject> validTargets)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            if (ContainsTarget(validTargets, uiRaycastResults[i].gameObject))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidWorldTargetClicked(Vector2 screenPosition, List<GameObject> validTargets)
    {
        if (mainCamera == null)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit) && ContainsTarget(validTargets, hit.collider.gameObject))
        {
            return true;
        }

        RaycastHit2D hit2D = Physics2D.GetRayIntersection(ray);
        return hit2D.collider != null && ContainsTarget(validTargets, hit2D.collider.gameObject);
    }

    private bool ContainsTarget(List<GameObject> validTargets, GameObject clickedObject)
    {
        if (clickedObject == null)
        {
            return false;
        }

        for (int i = 0; i < validTargets.Count; i++)
        {
            GameObject target = validTargets[i];
            if (target == null)
            {
                continue;
            }

            if (clickedObject == target || clickedObject.transform.IsChildOf(target.transform))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetPointerDownPosition(out Vector2 screenPosition)
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

        screenPosition = Vector2.zero;
        return false;
    }

    private void OnDisable()
    {
        KillGuideTween();
    }

    private void OnDestroy()
    {
        KillGuideTween();
    }
}
