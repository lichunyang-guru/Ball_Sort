using UnityEngine;
using UnityEngine.UI;

// Serialized in the source playable, but disabled there. OrientationWatcher and
// UiOrientationSwitcher are the active orientation components in that scene.
[DisallowMultipleComponent]
public class ScreenLayoutAdapter : MonoBehaviour
{
    public enum LayoutOrientation { Portrait, Landscape }
    public enum CameraAdaptMode { Auto, FieldOfView, OrthographicSize }

    [System.Serializable]
    public class UiLayoutSettings
    {
        public Vector2 portraitAnchoredPosition;
        public Vector2 landscapeAnchoredPosition;
    }

    [System.Serializable]
    public class CameraLayoutSettings
    {
        public float portraitFieldOfView = 60;
        public float landscapeFieldOfView = 60;
        public float portraitOrthographicSize = 5;
        public float landscapeOrthographicSize = 5;
    }

    [System.Serializable]
    public class GameRootLayoutSettings
    {
        public Vector3 portraitBaseScale = Vector3.one;
        public Vector3 landscapeBaseScale = Vector3.one;
        public float portraitReferenceAspect = .5625f;
        public float landscapeReferenceAspect = 1.77777779f;
        public float minScale = .5f;
        public float maxScale = 2;
    }

    public RectTransform topUi;
    public Transform gameRoot;
    public RectTransform bottomUi;
    public RectTransform localizedTextOne;
    public RectTransform localizedTextTwo;
    public Camera targetCamera;
    public UiLayoutSettings topUiLayout = new UiLayoutSettings();
    public UiLayoutSettings bottomUiLayout = new UiLayoutSettings();
    public UiLayoutSettings localizedTextOneLayout = new UiLayoutSettings();
    public UiLayoutSettings localizedTextTwoLayout = new UiLayoutSettings();
    public GameRootLayoutSettings gameRootLayout = new GameRootLayoutSettings();
    public RectTransform screenBoundary;
    public float screenBoundaryPadding = 8;
    public bool keepUiInsideScreen = true;
    public CameraAdaptMode cameraAdaptMode = CameraAdaptMode.Auto;
    public CameraLayoutSettings cameraLayout = new CameraLayoutSettings();
    public float landscapeAspectThreshold = 1;
    public bool applyOnEnable = true;
    public bool keepWatchingResolution = true;

    private int lastScreenWidth = -1, lastScreenHeight = -1;
    private LayoutOrientation? lastAppliedOrientation;
    private bool preservePortraitLayoutOnHeightChange;
    private CanvasScaler canvasScaler;
    private float originalCanvasMatchWidthOrHeight;
    private bool canvasScalerSettingsInitialized;
    private Vector3 originalGameRootScale = Vector3.one;
    private bool originalGameRootScaleInitialized;

    private void Awake() { CacheCanvasScalerSettings(); CacheOriginalGameRootScale(); }
    private void OnEnable() { if (applyOnEnable) ApplyCurrentLayout(true); }
    private void Update()
    {
        if (keepWatchingResolution)
        {
            if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height) ApplyCurrentLayout(false);
        }
        else if (!preservePortraitLayoutOnHeightChange) ClampConfiguredUiTargets();
    }

    [ContextMenu("Apply Current Layout")]
    public void ApplyCurrentLayout() { ApplyCurrentLayout(true); }

    private void ApplyCurrentLayout(bool force)
    {
        LayoutOrientation orientation = GetCurrentOrientation();
        bool widthChanged = lastScreenWidth != Screen.width;
        bool heightChanged = lastScreenHeight != Screen.height;
        bool preserve = !force && orientation == LayoutOrientation.Portrait &&
            lastAppliedOrientation == LayoutOrientation.Portrait && !widthChanged && heightChanged;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        if (preserve) { preservePortraitLayoutOnHeightChange = true; return; }
        if (!force && !widthChanged && !heightChanged && lastAppliedOrientation == orientation) return;
        preservePortraitLayoutOnHeightChange = false;
        ApplyCanvasScale(orientation);
        ApplyUiLayout(topUi, topUiLayout, orientation, true);
        ApplyUiLayout(bottomUi, bottomUiLayout, orientation, false);
        ApplyLocalizedTextLayout(localizedTextOne, localizedTextOneLayout, orientation);
        ApplyLocalizedTextLayout(localizedTextTwo, localizedTextTwoLayout, orientation);
        ApplyCameraLayout(orientation);
        ApplyGameRootLayout(orientation);
        ClampConfiguredUiTargets();
        lastAppliedOrientation = orientation;
    }

    private void CacheCanvasScalerSettings()
    {
        canvasScaler = GetComponent<CanvasScaler>();
        if (canvasScaler == null || canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
            canvasScaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight) return;
        originalCanvasMatchWidthOrHeight = canvasScaler.matchWidthOrHeight;
        canvasScalerSettingsInitialized = true;
    }

    private LayoutOrientation GetCurrentOrientation()
    {
        if (Screen.height <= 0) return LayoutOrientation.Portrait;
        return (float)Screen.width / Screen.height >= Mathf.Max(.01f, landscapeAspectThreshold)
            ? LayoutOrientation.Landscape : LayoutOrientation.Portrait;
    }

    private void ApplyUiLayout(RectTransform target, UiLayoutSettings settings, LayoutOrientation orientation, bool top)
    {
        if (target == null) return;
        bool portrait = orientation == LayoutOrientation.Portrait;
        SetAnchors(target, top
            ? (portrait ? new Vector2(.5f, 1) : new Vector2(0, .5f))
            : (portrait ? new Vector2(.5f, 0) : new Vector2(1, .5f)));
        target.anchoredPosition = portrait ? settings.portraitAnchoredPosition : settings.landscapeAnchoredPosition;
    }

    private void ApplyCameraLayout(LayoutOrientation orientation)
    {
        if (targetCamera == null) return;
        CameraAdaptMode mode = cameraAdaptMode;
        if (mode == CameraAdaptMode.Auto)
            mode = targetCamera.orthographic ? CameraAdaptMode.OrthographicSize : CameraAdaptMode.FieldOfView;
        bool portrait = orientation == LayoutOrientation.Portrait;
        if (mode == CameraAdaptMode.OrthographicSize)
            targetCamera.orthographicSize = portrait ? cameraLayout.portraitOrthographicSize : cameraLayout.landscapeOrthographicSize;
        else targetCamera.fieldOfView = portrait ? cameraLayout.portraitFieldOfView : cameraLayout.landscapeFieldOfView;
    }

    private void ApplyLocalizedTextLayout(RectTransform target, UiLayoutSettings settings, LayoutOrientation orientation)
    {
        if (target == null || settings == null) return;
        bool portrait = orientation == LayoutOrientation.Portrait;
        SetAnchors(target, portrait ? new Vector2(.5f, 1) : new Vector2(0, .5f));
        target.anchoredPosition = portrait ? settings.portraitAnchoredPosition : settings.landscapeAnchoredPosition;
    }

    private void CacheOriginalGameRootScale()
    {
        if (originalGameRootScaleInitialized || gameRoot == null) return;
        originalGameRootScale = gameRoot.localScale;
        if (Mathf.Approximately(originalGameRootScale.x, 0) || Mathf.Approximately(originalGameRootScale.y, 0) || Mathf.Approximately(originalGameRootScale.z, 0))
            originalGameRootScale = Vector3.one;
        originalGameRootScaleInitialized = true;
    }

    private void SetAnchors(RectTransform target, Vector2 anchor)
    {
        target.anchorMin = target.anchorMax = target.pivot = anchor;
    }

    // The shipped JS calls these methods but contains no implementations for
    // them. They are unreachable in its disabled ScreenLayoutAdapter component.
    // Keep this compatibility surface without inventing a new layout policy.
    private void ApplyCanvasScale(LayoutOrientation orientation) { }
    private void ApplyGameRootLayout(LayoutOrientation orientation) { }
    private void ClampConfiguredUiTargets() { }
}
