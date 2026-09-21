using System.Collections;
using System.Collections.Generic;
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

    private sealed class StreamSegment
    {
        public LineRenderer renderer;
        public float startTime;
        public float endTime;
    }

    private readonly List<StreamSegment> segments = new List<StreamSegment>();
    private Material coloredStreamMaterial;
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
        PrepareSegments(streamObject.transform);
    }

    private void PrepareSegments(Transform parent)
    {
        GameController controller = gameRoot != null
            ? gameRoot.GetComponentInChildren<GameController>(true)
            : null;
        if (controller == null) controller = FindObjectOfType<GameController>();
        List<Color> colors = controller != null ? controller.GetLevelWaterColors() : new List<Color>();
        if (colors.Count == 0)
        {
            Debug.LogWarning("开场水流未找到当前关卡的水柱配色。", this);
            return;
        }

        // 白色底图保留水柱原色，避免被原蓝色贴图二次染色。
        coloredStreamMaterial = new Material(streamMaterial);
        coloredStreamMaterial.mainTexture = Texture2D.whiteTexture;
        float emitTime = Mathf.Max(0.01f, emissionDuration);
        float travelTime = Mathf.Max(0.01f, moveDuration);
        float start = 0f;
        int previousColor = -1;
        while (start < emitTime)
        {
            int colorIndex = Random.Range(0, colors.Count);
            if (colors.Count > 1 && colorIndex == previousColor)
                colorIndex = (colorIndex + Random.Range(1, colors.Count)) % colors.Count;
            previousColor = colorIndex;
            float end = Mathf.Min(emitTime, start + Random.Range(0.09f, 0.225f) * travelTime);
            GameObject segmentObject = new GameObject("WaterSegment");
            segmentObject.transform.SetParent(parent, false);
            LineRenderer line = segmentObject.AddComponent<LineRenderer>();
            line.enabled = false;
            line.useWorldSpace = true;
            line.sharedMaterial = coloredStreamMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.TransformZ;
            line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            line.widthMultiplier = streamWidth;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(colors[colorIndex], 0f), new GradientColorKey(colors[colorIndex], 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            line.colorGradient = gradient;
            line.sortingOrder = sortingOrder;
            line.numCapVertices = 8;
            line.numCornerVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            segments.Add(new StreamSegment { renderer = line, startTime = start, endTime = end });
            start = end;
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
            foreach (StreamSegment segment in segments)
            {
                UpdateStream(segment, elapsed, travelTime);
            }
            yield return null;
            elapsed += Time.deltaTime;
        }
        HideStream();
        PlayMoveShootExit();
    }

    private void UpdateStream(StreamSegment segment, float elapsed, float travelTime)
    {
        float length = pathDistances[pathDistances.Length - 1];
        LineRenderer stream = segment.renderer;
        float head = Mathf.Clamp01((elapsed - segment.startTime) / travelTime) * length;
        float tail = Mathf.Clamp01((elapsed - segment.endTime) / travelTime) * length;
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
        HideStream();
    }

    private void HideStream()
    {
        foreach (StreamSegment segment in segments)
        {
            if (segment.renderer != null) segment.renderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (coloredStreamMaterial != null) Destroy(coloredStreamMaterial);
    }
}
