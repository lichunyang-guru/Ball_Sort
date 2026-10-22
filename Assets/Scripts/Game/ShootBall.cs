using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
/// <summary>
/// 发射球
/// </summary>
public class ShootBall : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("路径点根节点，用于获取计算路径点位置。")]
    public Transform pathRoot;
    [Tooltip("发射用小球预制体，需要挂 BallView。")]
    [FormerlySerializedAs("ballPrefab")]
    public GameObject ball;
    [Tooltip("小球颜色与 Sprite 配置表，用于随机上色。")]
    public BallVisualPalette ballVisualPalette;

    public Transform moveShoot;
    public Transform gameRoot;
    public GuidePanel guidePanel;

    [Header("Shoot")]
    [Tooltip("启动后发射的小球数量。")]
    public int ballCount = 10;
    [Tooltip("相邻两个小球的发射间隔。")]
    public float spawnInterval = 0.08f;
    [Tooltip("单个小球走完整条路径的时长。")]
    public float moveDuration = 1.5f;
    [Tooltip("屏幕上同时飞行的小球上限，控制低端设备的峰值压力。")]
    public int maxActiveBalls = 36;
    [Tooltip("开场前预创建的小球数量，避免第一波发射时连续 Instantiate 卡顿。")]
    public int prewarmCount = 40;
    [Tooltip("发射小球的 SpriteRenderer 排序值。")]
    public int sortingOrder = 50;
    [Tooltip("发射实例小球相对预制体的缩放倍率。")]
    public float shootBallScaleMultiplier = 0.8f;
    [Tooltip("DOTween 路径类型，CatmullRom 更平滑，Linear 会直线连接路径点。")]
    public PathType pathType = PathType.CatmullRom;

    private static readonly BallColorType[] RandomColors =
    {
        BallColorType.Green,
        BallColorType.Blue,
        BallColorType.Red
    };

    private readonly List<GameObject> activeBalls = new List<GameObject>();
    private readonly Dictionary<GameObject, BallView> ballViewCache = new Dictionary<GameObject, BallView>();
    private ObjectPoolManager objectPoolManager;
    private string PoolName => $"ShootBall_{ball.name}";

    private void Start()
    {
        if (!CanShoot())
        {
            return;
        }

        objectPoolManager = ObjectPoolManager.Instance;
        objectPoolManager.Prewarm(PoolName, ball, Mathf.Max(0, prewarmCount));
        PrepareDelayedGuide();
        StartCoroutine(ShootRoutine(GetPathPositions()));

    }

    private IEnumerator ShootRoutine(Vector3[] pathPositions)
    {
        yield return new WaitForSeconds(0.2f);

        int count = Mathf.Max(0, ballCount);

        for (int i = 0; i < count; i++)
        {
            while (maxActiveBalls > 0 && activeBalls.Count >= maxActiveBalls)
            {
                yield return null;
            }

            SpawnBall(pathPositions);

            if (spawnInterval > 0f && i < count - 1)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        yield return new WaitUntil(() => activeBalls.Count == 0);

        PlayMoveShootExit();
    }

    private void SpawnBall(Vector3[] pathPositions)
    {
        if (objectPoolManager == null)
        {
            return;
        }

        GameObject shootBall = objectPoolManager.Get(PoolName, ball, this.transform);
        if (shootBall == null)
        {
            return;
        }

        activeBalls.Add(shootBall);
        InitializeBall(shootBall);
        shootBall.transform.position = pathPositions[0];
        shootBall.transform.localRotation = Quaternion.identity;
        shootBall.transform.localScale = ball.transform.localScale * Mathf.Max(0.01f, shootBallScaleMultiplier);
        shootBall.transform.DOKill();

        shootBall.transform
            .DOPath(pathPositions, Mathf.Max(0.01f, moveDuration), pathType)
            .SetEase(Ease.Linear)
            .OnComplete(() => RecycleBall(shootBall));
    }

    private Vector3[] GetPathPositions()
    {
        Vector3[] pathPositions = new Vector3[pathRoot.childCount];
        for (int i = 0; i < pathRoot.childCount; i++)
        {
            pathPositions[i] = pathRoot.GetChild(i).position;
        }

        return pathPositions;
    }

    private bool CanShoot()
    {
        if (ball == null || ballVisualPalette == null || pathRoot == null || pathRoot.childCount < 2)
        {
            return false;
        }
        return true;
    }

    private void InitializeBall(GameObject shootBall)
    {
        if (!ballViewCache.TryGetValue(shootBall, out BallView ballView) || ballView == null)
        {
            ballView = shootBall.GetComponent<BallView>();
            if (ballView != null)
            {
                ballViewCache[shootBall] = ballView;
            }
        }

        if (ballView == null)
        {
            return;
        }

        BallColorType randomColor = RandomColors[Random.Range(0, RandomColors.Length)];
        ballView.Initialize(randomColor, ballVisualPalette);
        ballView.ResetVisuals();
        ballView.SetSortingOrder(sortingOrder);
    }

    private void RecycleBall(GameObject shootBall)
    {
        if (shootBall == null || !activeBalls.Remove(shootBall))
        {
            return;
        }

        shootBall.transform.DOKill();
        if (objectPoolManager != null)
        {
            objectPoolManager.Release(PoolName, shootBall);
            return;
        }

        Destroy(shootBall);
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

        List<GameObject> ballsToRecycle = new List<GameObject>(activeBalls);
        for (int i = 0; i < ballsToRecycle.Count; i++)
        {
            RecycleBall(ballsToRecycle[i]);
        }
    }
}
