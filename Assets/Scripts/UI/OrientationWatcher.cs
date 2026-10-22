using System;
using UnityEngine;

public enum AspectOrientation { Portrait, Landscape }

public sealed class OrientationWatcher : MonoBehaviour
{
    public static OrientationWatcher Instance;
    public AspectOrientation CurrentOrientation;
    public event Action<AspectOrientation, bool, bool> OnOrientationChanged;
    public event Action<int, int> OnResolutionChanged;
    private int _lastWidth, _lastHeight;
    private AspectOrientation _lastOrientationNotified;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;
        CurrentOrientation = ComputeOrientation(_lastWidth, _lastHeight);
        _lastOrientationNotified = CurrentOrientation;
    }

    private void Update()
    {
        if (_lastWidth == Screen.width && _lastHeight == Screen.height) return;
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;
        if (OnResolutionChanged != null) OnResolutionChanged(_lastWidth, _lastHeight);
        TryNotifyOrientationChange();
    }

    private void TryNotifyOrientationChange()
    {
        AspectOrientation orientation = ComputeOrientation(Screen.width, Screen.height);
        if (orientation == _lastOrientationNotified) return;
        bool toPortrait = orientation == AspectOrientation.Portrait && _lastOrientationNotified == AspectOrientation.Landscape;
        bool toLandscape = orientation == AspectOrientation.Landscape && _lastOrientationNotified == AspectOrientation.Portrait;
        CurrentOrientation = _lastOrientationNotified = orientation;
        if (OnOrientationChanged != null) OnOrientationChanged(orientation, toPortrait, toLandscape);
    }

    private AspectOrientation ComputeOrientation(int width, int height)
    {
        return width >= height ? AspectOrientation.Landscape : AspectOrientation.Portrait;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
