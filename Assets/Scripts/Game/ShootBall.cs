using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 沿 ShootRoot 原发射路径显示连续水流，结束后进入试管关卡。
/// </summary>
public class ShootBall : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("沿用原小球 DOPath 的路径节点和顺序。")]
    public Transform pathRoot;
    public Transform moveShoot;
    public Transform gameRoot;
    public GuidePanel guidePanel;

    [Header("Water Stream")]
    public Material streamMaterial;
    [Min(0.01f)] public float streamWidth = 0.2f;
    [Tooltip("源头连续出水的时间，结束后水尾继续沿路径流出。")]
    [Min(0.01f)] public float emissionDuration = 2.37f;
    [Tooltip("水头或水尾走完整条原路径的时间。")]
    [Min(0.01f)] public float moveDuration = 0.7f;
    public int sortingOrder = 50;
    public PathType pathType = PathType.CatmullRom;

    private LineRenderer stream;
    private Vector3[] sampledPath;
    private float[] pathDistances;

    private void Start()
    {
        if (pathRoot == null || pathRoot.childCount < 2 || streamMaterial == null)
        {
            Debug.LogWarning("ShootRoot 需要至少两个路径节点和 Stream Material。", this);
            return;
        }

        PrepareStream();
        PrepareDelayedGuide();
        StartCoroutine(ShootRoutine());
    }

    private void PrepareStream()
    {
        Vector3[] waypoints = new Vector3[pathRoot.childCount];
        for (int i = 0; i < waypoints.Length; i++)
        {
            waypoints[i] = pathRoot.GetChild(i).position;
        }

        GameObject streamObject = new GameObject("ShootWaterStream");
        streamObject.transform.SetParent(transform, false);
        streamObject.transform.position = waypoints[0];
        stream = streamObject.AddComponent<LineRenderer>();
        stream.enabled = false;
        stream.useWorldSpace = true;
        stream.sharedMaterial = streamMaterial;
        stream.textureMode = LineTextureMode.Stretch;
        stream.alignment = LineAlignment.TransformZ;
        stream.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        stream.widthMultiplier = streamWidth;
        // Luna leaves a dynamically added LineRenderer's gradient uninitialized.
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        stream.colorGradient = gradient;
        stream.sortingOrder = sortingOrder;
        stream.numCapVertices = 0;
        stream.numCornerVertices = 0;
        stream.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        stream.receiveShadows = false;

        // 直接采样原 DOPath，避免另写曲线算法导致与 Path 节点的实际路线不同。
        Tween pathTween = streamObject.transform
            .DOPath(waypoints, Mathf.Max(0.01f, moveDuration), pathType)
            .SetEase(Ease.Linear).Pause();
        pathTween.ForceInit();
        sampledPath = pathTween.PathGetDrawPoints(12);
        pathTween.Kill();
        // Path samples are world positions; keep the renderer origin at world zero for Luna.
        streamObject.transform.position = Vector3.zero;
        streamObject.transform.rotation = Quaternion.identity;

        pathDistances = new float[sampledPath.Length];
        for (int i = 1; i < sampledPath.Length; i++)
        {
            pathDistances[i] = pathDistances[i - 1]
                + Vector3.Distance(sampledPath[i - 1], sampledPath[i]);
        }
    }

    private IEnumerator ShootRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        float travelTime = Mathf.Max(0.01f, moveDuration);
        float emitTime = Mathf.Max(0.01f, emissionDuration);
        float elapsed = 0f;
        while (elapsed < emitTime + travelTime)
        {
            UpdateStream(elapsed, travelTime, emitTime);
            yield return null;
            elapsed += Time.deltaTime;
        }
        stream.enabled = false;
        PlayMoveShootExit();
    }

    private void UpdateStream(float elapsed, float travelTime, float emitTime)
    {
        float length = pathDistances[pathDistances.Length - 1];
        float head = Mathf.Clamp01(elapsed / travelTime) * length;
        float tail = Mathf.Clamp01((elapsed - emitTime) / travelTime) * length;
        stream.enabled = head - tail > 0.001f;
        if (!stream.enabled)
        {
            return;
        }

        int pointCount = 2;
        for (int i = 1; i < sampledPath.Length - 1; i++)
        {
            if (pathDistances[i] > tail && pathDistances[i] < head) pointCount++;
        }
        stream.positionCount = pointCount;
        stream.SetPosition(0, SamplePath(tail));
        int index = 1;
        for (int i = 1; i < sampledPath.Length - 1; i++)
        {
            if (pathDistances[i] > tail && pathDistances[i] < head)
            {
                stream.SetPosition(index++, sampledPath[i]);
            }
        }
        stream.SetPosition(index, SamplePath(head));
    }

    private Vector3 SamplePath(float distance)
    {
        for (int i = 1; i < sampledPath.Length; i++)
        {
            if (distance <= pathDistances[i])
            {
                float segmentLength = pathDistances[i] - pathDistances[i - 1];
                float t = segmentLength > 0f ? (distance - pathDistances[i - 1]) / segmentLength : 0f;
                return Vector3.Lerp(sampledPath[i - 1], sampledPath[i], t);
            }
        }
        return sampledPath[sampledPath.Length - 1];
    }

    private void PlayMoveShootExit()
    {
        if (moveShoot == null)
        {
            PlayGameRootEnter();
            return;
        }

        moveShoot.DOKill();
        moveShoot.DOMoveY(8.3f, 0.25f).OnComplete(() =>
        {
            moveShoot.gameObject.SetActive(false);
            PlayGameRootEnter();
        });
    }

    private void PlayGameRootEnter()
    {
        if (gameRoot == null)
        {
            ShowDelayedGuide();
            return;
        }

        gameRoot.DOKill();
        gameRoot.DOMoveY(1003f, 0.5f).OnComplete(ShowDelayedGuide);
    }

    private void PrepareDelayedGuide()
    {
        if (guidePanel == null)
        {
            guidePanel = FindObjectOfType<GuidePanel>();
        }

        if (guidePanel != null && !guidePanel.playOnStart)
        {
            guidePanel.HideGuideImmediate();
        }
    }

    private void ShowDelayedGuide()
    {
        if (guidePanel == null)
        {
            guidePanel = FindObjectOfType<GuidePanel>();
        }

        if (guidePanel != null)
        {
            guidePanel.StartGuide();
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (stream != null)
        {
            stream.enabled = false;
        }
    }
}
