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

    [Tooltip("单个彩色色块沿水流方向的平均长度（世界单位）。")]
    [Min(0.05f)] public float colorPatchLength = 0.3f;
    [Tooltip("色块纹理的滚动速度，与水头填充速度分开，保证流动时仍能看清颜色。")]
    [Min(0f)] public float colorScrollSpeed = 0.45f;
    private const int ColorPatchColumns = 24;
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
        public Vector3[] vertices;
        public Vector2[] uv;
        public float sourceDistance;
        public float Length => distances[distances.Length - 1];
    }

    private readonly List<StreamRibbon> streams = new List<StreamRibbon>();
    private Material coloredStreamMaterial;
    private Texture2D coloredStreamTexture;
    private GameObject streamRoot;
    private float flowSpeed;

    // 已保存的场景会覆盖字段初始化值。只迁移旧版三个错误的默认坐标，保留自定义位置。
    private void CorrectLegacyOutletPositions()
    {
        if (outletPositions == null || outletPositions.Length != 6) return;
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
        CorrectLegacyOutletPositions();
    }

    private void Start()
    {
        CorrectLegacyOutletPositions();
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
        flowSpeed = main.Length / Mathf.Max(0.01f, moveDuration);
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

        // 二维错位色块拼接；每个横截面可见多种颜色，避免整段单色和纵向丝带。
        // 环形距离保证循环无接缝；独立随机源不影响关卡的随机序列。
        const int columns = ColorPatchColumns;
        const int rows = 3;
        var random = new System.Random(7319);
        Vector2[] centers = new Vector2[columns * rows];
        Color[] patchColors = new Color[centers.Length];
        int[] colorIndices = new int[centers.Length];
        int[] colorUsage = new int[colors.Count];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int patch = row * columns + column;
                centers[patch] = new Vector2(column + 0.1f + (float)random.NextDouble() * 0.8f,
                    row + 0.1f + (float)random.NextDouble() * 0.8f);
                // 优先使用较少出现的颜色，并避开左侧、上方和循环接缝的同色邻居。
                int selected = random.Next(colors.Count);
                int bestScore = int.MaxValue;
                for (int candidate = 0; candidate < colors.Count; candidate++)
                {
                    int index = (selected + candidate) % colors.Count;
                    int score = colorUsage[index];
                    if (column > 0 && index == colorIndices[patch - 1]) score += 100;
                    if (row > 0 && index == colorIndices[patch - columns]) score += 100;
                    if (column == columns - 1 && index == colorIndices[row * columns]) score += 100;
                    if (row == rows - 1 && index == colorIndices[column]) score += 100;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    colorIndices[patch] = index;
                }
                colorUsage[colorIndices[patch]]++;
                Color baseColor = colors[colorIndices[patch]];
                Color.RGBToHSV(baseColor, out float hue, out float saturation, out float value);
                patchColors[patch] = Color.HSVToRGB(hue, Mathf.Max(0.8f, saturation), Mathf.Max(0.95f, value));
            }
        }

        const int width = 512;
        const int height = 64;
        coloredStreamTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        coloredStreamTexture.name = "BlendedColorPatchWater";
        coloredStreamTexture.wrapMode = TextureWrapMode.Repeat;
        coloredStreamTexture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;
                float first = float.MaxValue, second = float.MaxValue, third = float.MaxValue;
                int firstIndex = 0, secondIndex = 0, thirdIndex = 0;
                for (int patch = 0; patch < centers.Length; patch++)
                {
                    float dx = Mathf.Abs(u * columns - centers[patch].x);
                    float dy = Mathf.Abs(v * rows - centers[patch].y);
                    dx = Mathf.Min(dx, columns - dx);
                    dy = Mathf.Min(dy, rows - dy);
                    float distance = dx * dx + dy * dy;
                    if (distance < first)
                    {
                        third = second; thirdIndex = secondIndex;
                        second = first; secondIndex = firstIndex;
                        first = distance; firstIndex = patch;
                    }
                    else if (distance < second)
                    {
                        third = second; thirdIndex = secondIndex;
                        second = distance; secondIndex = patch;
                    }
                    else if (distance < third)
                    {
                        third = distance; thirdIndex = patch;
                    }
                }
                // 保留完整色块，只在很窄的交界处抗锯齿过渡，不做大范围模糊混色。
                float secondWeight = Mathf.Exp((first - second) * 24f);
                float thirdWeight = Mathf.Exp((first - third) * 24f);
                Color color = (patchColors[firstIndex] + patchColors[secondIndex] * secondWeight
                    + patchColors[thirdIndex] * thirdWeight) / (1f + secondWeight + thirdWeight);
                color *= 0.97f + 0.03f * Mathf.Sin(v * Mathf.PI);
                color.a = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / 0.08f);
                pixels[y * width + x] = color;
            }
        }
        coloredStreamTexture.SetPixels(pixels);
        coloredStreamTexture.Apply();
        coloredStreamMaterial = new Material(streamMaterial);
        coloredStreamMaterial.mainTexture = coloredStreamTexture;
        coloredStreamMaterial.color = Color.white;
    }

    private StreamRibbon CreateRibbon(string name, Vector3[] points, float sourceDistance)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(streamRoot.transform, false);
        StreamRibbon ribbon = new StreamRibbon
        {
            path = points, distances = new float[points.Length], sourceDistance = sourceDistance,
            vertices = new Vector3[points.Length * 2], uv = new Vector2[points.Length * 2],
            mesh = new Mesh { name = name + "WaterMesh" },
            renderer = obj.AddComponent<MeshRenderer>()
        };
        for (int i = 1; i < points.Length; i++)
            ribbon.distances[i] = ribbon.distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
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
        ribbon.renderer.sharedMaterial = coloredStreamMaterial;
        ribbon.renderer.sortingOrder = sortingOrder;
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
            CreateRibbon("Outlet" + streams.Count, points, distance);
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
        float head = Mathf.Clamp(elapsed * flowSpeed - stream.sourceDistance, 0f, stream.Length);
        float tail = Mathf.Clamp((elapsed - emitTime) * flowSpeed - stream.sourceDistance, 0f, stream.Length);
        stream.renderer.enabled = head - tail > 0.001f;
        if (!stream.renderer.enabled) return;

        int pathIndex = 1;
        float radius = Mathf.Max(0.001f, streamWidth * 0.5f);
        for (int i = 0; i < stream.path.Length; i++)
        {
            float distance = Mathf.Lerp(tail, head, (float)i / (stream.path.Length - 1));
            while (pathIndex < stream.path.Length - 1 && stream.distances[pathIndex] < distance) pathIndex++;
            float segmentLength = stream.distances[pathIndex] - stream.distances[pathIndex - 1];
            float t = segmentLength > 0f ? (distance - stream.distances[pathIndex - 1]) / segmentLength : 0f;
            Vector3 center = Vector3.Lerp(stream.path[pathIndex - 1], stream.path[pathIndex], t);
            Vector3 tangent = (stream.path[pathIndex] - stream.path[pathIndex - 1]).normalized;
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
            float edge = Mathf.Min(distance - tail, head - distance);
            // 支管连接处保持全宽，水头和离开源头的水尾采用圆润收口。
            if (stream.sourceDistance > 0f && tail <= 0f) edge = head - distance;
            float cap = Mathf.Clamp01(edge / radius);
            float halfWidth = radius * Mathf.Sqrt(1f - (1f - cap) * (1f - cap));
            int vertex = i * 2;
            stream.vertices[vertex] = streamRoot.transform.InverseTransformPoint(center - normal * halfWidth);
            stream.vertices[vertex + 1] = streamRoot.transform.InverseTransformPoint(center + normal * halfWidth);
            // 使用绝对行程，水头/水尾伸缩时纹理不拉伸，支管与主流共享相位。
            float u = (stream.sourceDistance + distance - elapsed * Mathf.Max(0f, colorScrollSpeed))
                / (Mathf.Max(0.05f, colorPatchLength) * ColorPatchColumns);
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
        if (coloredStreamTexture != null) Destroy(coloredStreamTexture);
        if (streamRoot != null) Destroy(streamRoot);
    }
}
