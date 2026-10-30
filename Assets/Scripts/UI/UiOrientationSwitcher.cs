using System.Collections.Generic;
using UnityEngine;

public sealed class UiOrientationSwitcher : MonoBehaviour
{
    public List<GameObject> PortraitRoot = new List<GameObject>();
    public List<GameObject> LandscapeRoot = new List<GameObject>();
    public RectTransform title1, title2;
    public Vector2 portraitGameRootPosition = new Vector2(0, -36);
    public Vector2 landscapeGameRootPosition = new Vector2(492, 12);
    public Vector2 portraitTitlePosition = new Vector2(0, 534);
    public Vector2 landscapeTitlePosition = new Vector2(-552, 436);
    private OrientationWatcher watcher;

    private void Start()
    {
        watcher = OrientationWatcher.Instance;
        if (watcher == null) return;
        Apply(watcher.CurrentOrientation);
        watcher.OnOrientationChanged += HandleOrientationChanged;
    }

    private void OnDestroy()
    {
        if (watcher != null) watcher.OnOrientationChanged -= HandleOrientationChanged;
    }

    private void HandleOrientationChanged(AspectOrientation orientation, bool toPortrait, bool toLandscape)
    {
        Apply(orientation);
    }

    public void Apply(AspectOrientation orientation)
    {
        bool portrait = orientation == AspectOrientation.Portrait;
        if (PortraitRoot != null)
            foreach (GameObject root in PortraitRoot) if (root != null) root.SetActive(portrait);
        if (LandscapeRoot != null)
            foreach (GameObject root in LandscapeRoot) if (root != null) root.SetActive(!portrait);
        if (title1 != null)
        {
            title1.anchoredPosition = portrait ? portraitGameRootPosition : landscapeGameRootPosition;
        }
        if (title2 != null)
        {
            title2.anchoredPosition = portrait ? portraitTitlePosition : landscapeTitlePosition;
        }
    }
}
