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

    [Header("Ice Drop")]
    [Tooltip("试管上升到位后，从屏幕上方落下的冰块根节点。冰块顶部按屏幕上缘定位。")]
    [SerializeField] private Transform iceRoot;
    [Tooltip("可见冰块顶部到屏幕上缘固定保留的世界空间距离。")]
    [Min(0f)] [SerializeField] private float iceScreenTopMargin = 0.1f;
    [Min(0f)] [SerializeField] private float iceDropDelay = 0.12f;
    [Min(0.01f)] [SerializeField] private float iceDropDuration = 0.8f;
    [Min(0f)] [SerializeField] private float iceStartClearance = 0.5f;

    [Header("Bee Panel")]
    [Tooltip("冰块落稳后显示的提示面板，点击屏幕任意位置后淡出。")]
    [SerializeField] private CanvasGroup beePanel;
    [Min(0f)] [SerializeField] private float beeShowDuration = 1.2f;
    [Min(0f)] [SerializeField] private float beeDismissDelay = 1.5f;
    [Min(0f)] [SerializeField] private float beeFadeDuration = 0.25f;

    [Header("Water Stream")]
    public Material streamMaterial;
    [Min(0.01f)] public float streamWidth = 0.2f;
    [Tooltip("源头连续出水的时间，结束后水尾继续沿路径流出。")]
    [Min(0.01f)] public float emissionDuration = 2.37f;
    [Tooltip("水流走完整条原路径的时间，彩条、水头和水尾使用同一速度；越小流得越快。")]
    [Min(0.01f)] public float moveDuration = 0.7f;
    [LunaPlaygroundField("水流速度倍率（1为原速，越大越快）", 1, "水流外观参数")]
    [Tooltip("以 Move Duration 为基础调整速度。1 为原速，2 为两倍速，0.5 为半速；彩条、水头、水尾和出水口同步。")]
    [Min(0.01f)] public float flowSpeedMultiplier = 1f;
    public int sortingOrder = 50;
    public PathType pathType = PathType.CatmullRom;

    [Header("彩条外观")]
    [InspectorName("颜色宽度")]
    [LunaPlaygroundField("彩带宽度（越大越宽）", 0, "水流外观参数")]
    [Tooltip("每条颜色沿水流方向的长度。越大颜色越宽；建议 0.4～0.8，运行中调整也会立即生效。")]
    [Min(0.05f)] public float colorBandWidth = 0.55f;
    [Tooltip("用于覆盖不规则轨道的水带宽度，最终按轨道透明轮廓裁切。")]
    [Min(0.01f)] public float trackCoverageWidth = 0.55f;
    private bool clipToTrack;
    private int stripeCount;
    [Header("Six Outlets")]
    [Tooltip("出水口相对于 PathBk 图片的归一化坐标（左下角为 0,0）。")]
    public Vector2[] outletPositions =
    {
        new Vector2(0.083f, 0.045f), new Vector2(0.250f, 0.045f),
        new Vector2(0.42146f, 0.045f), new Vector2(0.58927f, 0.045f),
        new Vector2(0.75805f, 0.045f), new Vector2(0.926f, 0.045f)
    };
    [Tooltip("水流伸出管口的世界空间长度。")]
    [Min(0f)] public float outletExtension = 0.12f;

    private sealed class StreamRibbon
    {
        public Mesh mesh;
        public MeshRenderer renderer;
        public Vector3[] path;
        public float[] distances;
        public Vector3[] tangents;
        public Vector3[] vertices;
        public Vector2[] uv;
        public float sourceDistance;
        public float Length => distances[distances.Length - 1];
    }

    private readonly List<StreamRibbon> streams = new List<StreamRibbon>();
    private Material coloredStreamMaterial;
    private Material mainStreamMaterial;
    private Texture2D coloredStreamTexture;
    private GameObject streamRoot;
    private float flowSpeed;
    private GameController boardController;
    private Vector3 iceLandingPosition;
    private float iceBottomOffset;
    private Sequence iceDropSequence;
    private Tween beeFadeTween;
    private float beeDismissReadyTime;
    private bool waitingForBeeDismiss;
    private bool iceLanded;
    private void Awake()
    {
        if (iceRoot != null)
        {
            iceLandingPosition = iceRoot.position;
            iceRoot.gameObject.SetActive(false);
        }
        if (beePanel != null)
        {
            beePanel.alpha = 0f;
            beePanel.interactable = false;
            beePanel.blocksRaycasts = false;
        }

        boardController = gameRoot != null
            ? gameRoot.GetComponentInChildren<GameController>(true) : null;
        if (boardController == null) boardController = FindObjectOfType<GameController>();

        // 入场前就拦截点击，避免试管尚未就位时被选中。
        if (boardController != null && gameRoot != null)
            boardController.SetBoardEntryInProgress(true);
    }

    // 已保存的场景会覆盖字段初始化值；迁移旧管口坐标。
    private void CorrectLegacyVisualSettings()
    {
        if (outletPositions == null || (outletPositions.Length != 5 && outletPositions.Length != 6)) return;
        float[] oldX = { 0.414f, 0.575f, 0.739f };
        float[] correctedX = { 0.42146f, 0.58927f, 0.75805f };
        for (int i = 0; i < oldX.Length; i++)
        {
            Vector2 position = outletPositions[i + 2];
            if (Mathf.Approximately(position.x, oldX[i]) && Mathf.Approximately(position.y, 0.045f))
                outletPositions[i + 2] = new Vector2(correctedX[i], position.y);
        }

    }

    private void OnValidate()
    {
        CorrectLegacyVisualSettings();
    }

    private void Start()
    {
        CorrectLegacyVisualSettings();
        if (pathRoot == null || pathRoot.childCount < 2 || streamMaterial == null)
        {
            Debug.LogWarning("ShootRoot 需要至少两个路径节点和 Stream Material。", this);
            PlayGameRootEnter();
            return;
        }

        PrepareStream();
        PrepareDelayedGuide();
        StartCoroutine(ShootRoutine());
    }

    private void Update()
    {
        if (!waitingForBeeDismiss || Time.time < beeDismissReadyTime)
            return;

        if (Input.GetMouseButtonDown(0)
            || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            DismissBeePanel();
    }

    private void LateUpdate()
    {
        if (iceLanded) FitIceRootToScreen();
    }

    private void PrepareStream()
    {
        Vector3[] waypoints = new Vector3[pathRoot.childCount];
        for (int i = 0; i < waypoints.Length; i++)
            waypoints[i] = pathRoot.GetChild(i).position;

        streamRoot = new GameObject("ShootWaterStream");
        streamRoot.transform.SetParent(transform, false);
        streamRoot.transform.position = waypoints[0];
        // 仍然采样原 DOPath，保留路径形状。
        Tween pathTween = streamRoot.transform
            .DOPath(waypoints, Mathf.Max(0.01f, moveDuration), pathType)
            .SetEase(Ease.Linear).Pause();
        pathTween.ForceInit();
        Vector3[] sampledPath = pathTween.PathGetDrawPoints(24);
        pathTween.Kill();
        streamRoot.transform.localPosition = Vector3.zero;

        PrepareMaterial();
        StreamRibbon main = CreateRibbon("Main", sampledPath, 0f);
        flowSpeed = main.Length / Mathf.Max(0.01f, moveDuration) * Mathf.Max(0.01f, flowSpeedMultiplier);
        PrepareOutlets(main);
    }

    private void PrepareMaterial()
    {
        GameController controller = gameRoot != null
            ? gameRoot.GetComponentInChildren<GameController>(true) : null;
        if (controller == null) controller = FindObjectOfType<GameController>();
        List<Color> colors = controller != null ? controller.GetLevelWaterColors() : new List<Color>();
        if (colors.Count == 0)
            colors.AddRange(new[] { Color.cyan, Color.blue, Color.magenta, Color.red, Color.yellow, Color.green });

        // 整组配色循环四次，首尾颜色无缝衔接。
        stripeCount = colors.Count * 4;
        Color[] stripeColors = new Color[colors.Count];
        for (int i = 0; i < colors.Count; i++)
        {
            Color.RGBToHSV(colors[i], out float hue, out float saturation, out float value);
            stripeColors[i] = Color.HSVToRGB(hue, Mathf.Max(0.8f, saturation), Mathf.Max(0.95f, value));
        }
        const int width = 1024;
        const int height = 128;
        coloredStreamTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        coloredStreamTexture.name = "DiagonalCandyWater";
        coloredStreamTexture.wrapMode = TextureWrapMode.Repeat;
        coloredStreamTexture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;
                // 横跨水流宽度错开约一条彩条，形成糖杖式斜纹。
                float phase = Mathf.Repeat(u * stripeCount - v * 0.85f, stripeCount);
                int stripe = Mathf.FloorToInt(phase);
                float withinStripe = phase - stripe;
                Color color = stripeColors[stripe % stripeColors.Length];
                // 在接缝两侧各 10% 的范围直接混合邻色，不添加白色分隔。
                const float transition = 0.10f;
                if (withinStripe < transition)
                {
                    Color previous = stripeColors[(stripe + stripeColors.Length - 1) % stripeColors.Length];
                    color = Color.Lerp(previous, color,
                        Mathf.SmoothStep(0f, 1f, (withinStripe + transition) / (2f * transition)));
                }
                else if (withinStripe > 1f - transition)
                {
                    Color next = stripeColors[(stripe + 1) % stripeColors.Length];
                    color = Color.Lerp(color, next,
                        Mathf.SmoothStep(0f, 1f, (withinStripe - (1f - transition)) / (2f * transition)));
                }
                // 轻微圆柱明暗和柔和反光，避免彩条像平面胶带。
                float roundness = Mathf.Sin(v * Mathf.PI);
                color *= 0.78f + 0.22f * roundness;
                float highlight = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(v - 0.35f) / 0.18f), 3f) * 0.18f;
                color = Color.Lerp(color, Color.white, highlight);
                color.a = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / 0.06f);
                pixels[y * width + x] = color;
            }
        }
        coloredStreamTexture.SetPixels(pixels);
        coloredStreamTexture.Apply();
        coloredStreamMaterial = new Material(streamMaterial);
        coloredStreamMaterial.mainTexture = coloredStreamTexture;
        coloredStreamMaterial.color = Color.white;
        ConfigureTrackMask();
    }

    private void ConfigureTrackMask()
    {
        SpriteRenderer track = pathRoot.parent != null ? pathRoot.parent.GetComponent<SpriteRenderer>() : null;
        Shader shader = Shader.Find("BallSort/WaterBodyReveal");
        if (track == null || track.sprite == null || shader == null) return;
        // 此 Shader 已由水柱材质引用。复用其普通贴图采样通道，默认不影响水柱/水滴。
        coloredStreamMaterial.shader = shader;
        coloredStreamMaterial.SetFloat("_RevealProgress", 1f);
        coloredStreamMaterial.SetFloat("_TrackMaskEnabled", 1f);
        coloredStreamMaterial.SetTexture("_TrackMask", track.sprite.texture);
        Rect rect = track.sprite.textureRect;
        Bounds bounds = track.sprite.bounds;
        Matrix4x4 inverse = track.transform.worldToLocalMatrix;
        float scaleU = rect.width / track.sprite.texture.width / bounds.size.x;
        float scaleV = rect.height / track.sprite.texture.height / bounds.size.y;
        Vector4 u = inverse.GetRow(0) * scaleU;
        Vector4 v = inverse.GetRow(1) * scaleV;
        u.w += rect.x / track.sprite.texture.width - bounds.min.x * scaleU;
        v.w += rect.y / track.sprite.texture.height - bounds.min.y * scaleV;
        coloredStreamMaterial.SetVector("_TrackWorldToU", u);
        coloredStreamMaterial.SetVector("_TrackWorldToV", v);
        // 横向主管下缘约位于原图 y=650/730，不能让加宽的主管伸进支管形成短截。
        // 只裁主管；真正的出水口继续沿用完整轨道遮罩。
        mainStreamMaterial = new Material(coloredStreamMaterial);
        mainStreamMaterial.SetFloat("_TrackMinimumV", (rect.y + rect.height * 0.11f) / track.sprite.texture.height);
        clipToTrack = true;
    }

    private StreamRibbon CreateRibbon(string name, Vector3[] points, float sourceDistance, bool isOutlet = false)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(streamRoot.transform, false);
        StreamRibbon ribbon = new StreamRibbon
        {
            path = points, distances = new float[points.Length], tangents = new Vector3[points.Length], sourceDistance = sourceDistance,
            vertices = new Vector3[points.Length * 2], uv = new Vector2[points.Length * 2],
            mesh = new Mesh { name = name + "WaterMesh" },
            renderer = obj.AddComponent<MeshRenderer>()
        };
        for (int i = 1; i < points.Length; i++)
            ribbon.distances[i] = ribbon.distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        for (int i = 0; i < points.Length; i++)
            ribbon.tangents[i] = (points[Mathf.Min(points.Length - 1, i + 1)]
                - points[Mathf.Max(0, i - 1)]).normalized;
        Color[] colors = new Color[ribbon.vertices.Length];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        int[] triangles = new int[(points.Length - 1) * 6];
        for (int i = 0; i < points.Length - 1; i++)
        {
            int t = i * 6, v = i * 2;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
        }
        ribbon.mesh.vertices = ribbon.vertices;
        ribbon.mesh.colors = colors;
        ribbon.mesh.uv = ribbon.uv;
        ribbon.mesh.triangles = triangles;
        obj.AddComponent<MeshFilter>().sharedMesh = ribbon.mesh;
        ribbon.renderer.sharedMaterial = !isOutlet && mainStreamMaterial != null ? mainStreamMaterial : coloredStreamMaterial;
        // 出水口在主水流下层，接头重叠部分由主水流遮挡。
        ribbon.renderer.sortingOrder = isOutlet ? sortingOrder - 1 : sortingOrder;
        ribbon.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ribbon.renderer.receiveShadows = false;
        ribbon.renderer.enabled = false;
        streams.Add(ribbon);
        return ribbon;
    }

    private void PrepareOutlets(StreamRibbon main)
    {
        SpriteRenderer channel = pathRoot.parent != null ? pathRoot.parent.GetComponent<SpriteRenderer>() : null;
        if (channel == null || channel.sprite == null || outletPositions == null) return;
        Bounds bounds = channel.sprite.bounds;
        foreach (Vector2 outlet in outletPositions)
        {
            Vector3 mouth = channel.transform.TransformPoint(new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, outlet.x),
                Mathf.Lerp(bounds.min.y, bounds.max.y, outlet.y), 0f));
            float nearest = float.MaxValue, distance = 0f;
            Vector3 junction = main.path[0];
            Vector3 localMouth = channel.transform.InverseTransformPoint(mouth);
            // 沿管口竖直向上找主路径交点，避免弯道处的最近点让支流斜穿管壁。
            for (int i = 1; i < main.path.Length; i++)
            {
                Vector3 a = channel.transform.InverseTransformPoint(main.path[i - 1]);
                Vector3 b = channel.transform.InverseTransformPoint(main.path[i]);
                float dx = b.x - a.x;
                if (Mathf.Abs(dx) < 0.00001f) continue;
                float t = (localMouth.x - a.x) / dx;
                if (t < 0f || t > 1f) continue;
                float rise = Mathf.Lerp(a.y, b.y, t) - localMouth.y;
                if (rise < 0f || rise >= nearest) continue;
                nearest = rise;
                junction = Vector3.Lerp(main.path[i - 1], main.path[i], t);
                distance = Mathf.Lerp(main.distances[i - 1], main.distances[i], t);
            }
            if (nearest == float.MaxValue)
            {
                Debug.LogWarning("出水口上方没有主水流路径，请检查 Outlet Positions。", this);
                continue;
            }
            Vector3 end = mouth - channel.transform.up * outletExtension;
            Vector3[] points = new Vector3[20];
            for (int i = 0; i < points.Length; i++)
                points[i] = Vector3.Lerp(junction, end, (float)i / (points.Length - 1));
            CreateRibbon("Outlet" + streams.Count, points, distance, true);
        }
    }

    private IEnumerator ShootRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        float emitTime = Mathf.Max(0.01f, emissionDuration);
        float lastDistance = 0f;
        foreach (StreamRibbon stream in streams)
            lastDistance = Mathf.Max(lastDistance, stream.sourceDistance + stream.Length);
        float elapsed = 0f;
        while (elapsed < emitTime + lastDistance / Mathf.Max(0.001f, flowSpeed))
        {
            foreach (StreamRibbon stream in streams) UpdateStream(stream, elapsed, emitTime);
            yield return null;
            elapsed += Time.deltaTime;
        }
        HideStream();
        PlayMoveShootExit();
    }

    private void UpdateStream(StreamRibbon stream, float elapsed, float emitTime)
    {
        float travelledDistance = elapsed * flowSpeed;
        float head = Mathf.Clamp(travelledDistance - stream.sourceDistance, 0f, stream.Length);
        float tail = Mathf.Clamp(travelledDistance - emitTime * flowSpeed - stream.sourceDistance, 0f, stream.Length);
        stream.renderer.enabled = head - tail > 0.001f;
        if (!stream.renderer.enabled) return;

        int pathIndex = 1;
        float radius = Mathf.Max(0.001f, (clipToTrack ? Mathf.Max(streamWidth, trackCoverageWidth) : streamWidth) * 0.5f);
        // 已充满的首尾延伸半径，让圆头覆盖轨道端帽，超出轮廓的部分由遮罩裁掉。
        if (clipToTrack)
        {
            if (tail <= 0f) tail = -radius;
            if (head >= stream.Length) head = stream.Length + radius;
        }
        for (int i = 0; i < stream.path.Length; i++)
        {
            float distance = Mathf.Lerp(tail, head, (float)i / (stream.path.Length - 1));
            while (pathIndex < stream.path.Length - 1 && stream.distances[pathIndex] < distance) pathIndex++;
            float segmentLength = stream.distances[pathIndex] - stream.distances[pathIndex - 1];
            float t = segmentLength > 0f ? (distance - stream.distances[pathIndex - 1]) / segmentLength : 0f;
            Vector3 center = Vector3.LerpUnclamped(stream.path[pathIndex - 1], stream.path[pathIndex], t);
            Vector3 tangent = Vector3.Lerp(stream.tangents[pathIndex - 1], stream.tangents[pathIndex], Mathf.Clamp01(t)).normalized;
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
            float edge = Mathf.Min(distance - tail, head - distance);
            // 支管连接处保持全宽，水头和离开源头的水尾采用圆润收口。
            if (stream.sourceDistance > 0f && tail <= 0f) edge = head - distance;
            float cap = Mathf.Clamp01(edge / radius);
            float halfWidth = radius * Mathf.Sqrt(1f - (1f - cap) * (1f - cap));
            int vertex = i * 2;
            stream.vertices[vertex] = streamRoot.transform.InverseTransformPoint(center - normal * halfWidth);
            stream.vertices[vertex + 1] = streamRoot.transform.InverseTransformPoint(center + normal * halfWidth);
            // 彩条与水头/水尾共用行程，颜色随水一起移动，支管与主流共享相位。
            float u = (stream.sourceDistance + distance - travelledDistance)
                / (Mathf.Max(0.05f, colorBandWidth) * stripeCount);
            stream.uv[vertex] = new Vector2(u, 0f);
            stream.uv[vertex + 1] = new Vector2(u, 1f);
        }
        stream.mesh.vertices = stream.vertices;
        stream.mesh.uv = stream.uv;
        stream.mesh.RecalculateBounds();
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
            PlayIceDrop();
            return;
        }

        gameRoot.DOKill();
        gameRoot.DOMoveY(1003f, 0.5f).OnComplete(PlayIceDrop);
    }

    private void PlayIceDrop()
    {
        if (iceRoot == null)
        {
            CompleteGameRootEnter();
            return;
        }

        iceRoot.gameObject.SetActive(true);
        FitIceRootToScreen();
        CacheIceBottomOffset();

        Camera camera = Camera.main;
        float screenTop = camera != null
            ? GetCameraTopWorldY(camera, iceLandingPosition.z)
            : iceLandingPosition.y + 8f;
        float startY = Mathf.Max(screenTop + iceBottomOffset + iceStartClearance,
            iceLandingPosition.y + iceStartClearance);
        iceRoot.position = new Vector3(iceLandingPosition.x, startY, iceLandingPosition.z);

        iceDropSequence?.Kill();
        iceDropSequence = DOTween.Sequence().SetTarget(iceRoot);
        iceDropSequence.AppendInterval(Mathf.Max(0f, iceDropDelay));
        iceDropSequence.Append(iceRoot.DOMoveY(iceLandingPosition.y,
            Mathf.Max(0.01f, iceDropDuration)).SetEase(Ease.OutBounce));
        iceDropSequence.OnComplete(CompleteGameRootEnter);
    }

    private void FitIceRootToScreen()
    {
        if (iceRoot == null) return;

        Camera camera = Camera.main;
        if (camera != null && TryGetIceTop(out float iceTop))
        {
            float screenTop = GetCameraTopWorldY(camera, iceRoot.position.z);
            float offset = screenTop - Mathf.Max(0f, iceScreenTopMargin) - iceTop;
            if (!Mathf.Approximately(offset, 0f))
                iceRoot.position += Vector3.up * offset;
        }

        iceLandingPosition = iceRoot.position;
    }

    private static float GetCameraTopWorldY(Camera camera, float worldZ)
    {
        if (camera.orthographic)
            return camera.transform.position.y + camera.orthographicSize;

        float depth = Mathf.Abs(worldZ - camera.transform.position.z);
        return camera.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth)).y;
    }

    private bool TryGetIceTop(out float iceTop)
    {
        iceTop = float.NegativeInfinity;
        foreach (SpriteRenderer renderer in iceRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null || renderer.gameObject.name != "ice"
                || !renderer.enabled || renderer.color.a <= 0.01f) continue;
            iceTop = Mathf.Max(iceTop, renderer.bounds.max.y);
        }
        return !float.IsNegativeInfinity(iceTop);
    }

    private void CacheIceBottomOffset()
    {
        iceBottomOffset = 0f;
        foreach (SpriteRenderer renderer in iceRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite != null && renderer.enabled)
                iceBottomOffset = Mathf.Max(iceBottomOffset, iceRoot.position.y - renderer.bounds.min.y);
        }
    }

    private void CompleteGameRootEnter()
    {
        iceLanded = true;
        FitIceRootToScreen();

        if (beePanel != null)
        {
            beeFadeTween?.Kill();
            beeFadeTween = beePanel.DOFade(1f, Mathf.Max(0f, beeShowDuration))
                .SetEase(Ease.OutSine).OnComplete(() =>
                {
                    beeDismissReadyTime = Time.time + Mathf.Max(0f, beeDismissDelay);
                    waitingForBeeDismiss = true;
                });
            return;
        }

        EnableBoardInteraction();
    }

    private void DismissBeePanel()
    {
        waitingForBeeDismiss = false;
        beeFadeTween?.Kill();
        beeFadeTween = beePanel.DOFade(0f, Mathf.Max(0f, beeFadeDuration))
            .OnComplete(EnableBoardInteraction);
    }

    private void EnableBoardInteraction()
    {
        if (boardController != null)
            boardController.SetBoardEntryInProgress(false);
        ShowDelayedGuide();
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
        iceDropSequence?.Kill();
        beeFadeTween?.Kill();
        HideStream();
    }

    private void HideStream()
    {
        foreach (StreamRibbon stream in streams)
        {
            if (stream.renderer != null) stream.renderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        foreach (StreamRibbon stream in streams)
            if (stream.mesh != null) Destroy(stream.mesh);
        if (coloredStreamMaterial != null) Destroy(coloredStreamMaterial);
        if (mainStreamMaterial != null) Destroy(mainStreamMaterial);
        if (coloredStreamTexture != null) Destroy(coloredStreamTexture);
        if (streamRoot != null) Destroy(streamRoot);
    }
}
