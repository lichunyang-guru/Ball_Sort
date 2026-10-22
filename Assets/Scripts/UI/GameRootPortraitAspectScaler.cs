using UnityEngine;

public sealed class GameRootPortraitAspectScaler : MonoBehaviour
{
    public float referenceAspect = .5625f;
    public float minimumScale = .7f;
    private Vector3 originalLocalScale = Vector3.one;
    private Vector3 contentPivotLocal;
    private Transform[] directChildren = new Transform[0];
    private Vector3[] originalChildLocalPositions = new Vector3[0];
    private bool originalScaleCached, contentPivotCached;
    private int lastScreenWidth = -1, lastScreenHeight = -1;

    public float CurrentScaleFactor
    {
        get
        {
            CacheOriginalScale();
            float originalY = Mathf.Abs(originalLocalScale.y);
            return originalY <= .0001f ? 1f : Mathf.Abs(transform.localScale.y) / originalY;
        }
    }

    private void Awake() { CacheOriginalScale(); TryCacheContentPivot(); }
    private void OnEnable() { CacheOriginalScale(); ApplyScale(true); }
    private void Update()
    {
        if (!contentPivotCached) TryCacheContentPivot();
        if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height) ApplyScale(false);
    }

    private void ApplyScale(bool force)
    {
        if (!force && lastScreenWidth == Screen.width && lastScreenHeight == Screen.height) return;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        CacheOriginalScale();
        float scale = 1;
        if (Screen.width > 0 && Screen.height > 0)
        {
            float aspect = (float)Screen.width / Screen.height;
            float reference = Mathf.Max(.01f, referenceAspect);
            if (aspect < reference) scale = Mathf.Clamp(aspect / reference, Mathf.Clamp(minimumScale, .01f, 1), 1);
        }
        if (!TryCacheContentPivot()) { transform.localScale = originalLocalScale; return; }
        transform.localScale = originalLocalScale * scale;
        Vector3 offset = contentPivotLocal * (1f / Mathf.Max(.01f, scale) - 1f);
        for (int i = 0; i < directChildren.Length; i++)
            if (directChildren[i] != null) directChildren[i].localPosition = originalChildLocalPositions[i] + offset;
    }

    private bool TryCacheContentPivot()
    {
        if (contentPivotCached) return true;
        Bounds bounds;
        if (!TryGetContentBounds(out bounds)) return false;
        contentPivotLocal = transform.InverseTransformPoint(bounds.center);
        directChildren = new Transform[transform.childCount];
        originalChildLocalPositions = new Vector3[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            directChildren[i] = transform.GetChild(i);
            originalChildLocalPositions[i] = directChildren[i].localPosition;
        }
        contentPivotCached = true;
        return true;
    }

    private bool TryGetContentBounds(out Bounds bounds)
    {
        bounds = default(Bounds);
        bool found = false;
        foreach (Renderer item in GetComponentsInChildren<Renderer>(true))
        {
            if (item == null || !item.enabled || !item.gameObject.activeInHierarchy) continue;
            if (found) bounds.Encapsulate(item.bounds);
            else { bounds = item.bounds; found = true; }
        }
        return found;
    }

    private void CacheOriginalScale()
    {
        if (originalScaleCached) return;
        originalLocalScale = transform.localScale;
        if (Mathf.Approximately(originalLocalScale.x, 0) || Mathf.Approximately(originalLocalScale.y, 0) || Mathf.Approximately(originalLocalScale.z, 0))
            originalLocalScale = Vector3.one;
        originalScaleCached = true;
    }
}
