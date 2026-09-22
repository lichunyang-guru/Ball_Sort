using DG.Tweening;
using UnityEngine;

public class WaterTransferVisual : MonoBehaviour
{
    private static readonly int WaterUpState = Animator.StringToHash("Base Layer.water-up");
    private const string WaterDownState = "Base Layer.water-down";
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private WaterBodyTopAnchor bodyTopAnchor;
    [SerializeField] private Animator waterUp;
    [SerializeField] private Animator waterDown;
    [Tooltip("落位后，水柱从底部展开到完整高度的时间。")]
    [Min(0.01f)]
    [SerializeField] private float revealDuration = 0.25f;

    private SpriteRenderer upRenderer;
    private SpriteRenderer downRenderer;
    private Material dropMaterial;
    private Material originalUpMaterial;
    private Material originalDownMaterial;

    private void Awake()
    {
        upRenderer = waterUp.GetComponent<SpriteRenderer>();
        downRenderer = waterDown.GetComponent<SpriteRenderer>();
        // 复用水柱已经引用的 Shader，避免运行时查找的 Shader 在导出时被裁剪。
        originalUpMaterial = upRenderer.sharedMaterial;
        originalDownMaterial = downRenderer.sharedMaterial;
        dropMaterial = new Material(body.sharedMaterial);
        dropMaterial.name = "WaterDropColorMatch";
        dropMaterial.SetFloat("_DropColorMatch", 1f);
        dropMaterial.SetFloat("_RevealProgress", 1f);
        upRenderer.sharedMaterial = dropMaterial;
        downRenderer.sharedMaterial = dropMaterial;
    }

    public void PlayUp()
    {
        RestoreBody();
        visualRoot.SetActive(false);
        upRenderer.color = GetWaterBallTint();
        SyncSorting();
        waterUp.gameObject.SetActive(true);
        waterUp.Play(WaterUpState, 0, 0f);
        waterUp.Update(0f);
    }

    public void PlayDown()
    {
        visualRoot.SetActive(false);
        waterUp.gameObject.SetActive(false);
        downRenderer.color = GetWaterBallTint();
        SyncSorting();
        waterDown.gameObject.SetActive(true);
        SetDownProgress(0f);
    }

    private Color GetWaterBallTint()
    {
        return body.color;
    }

    public Tween CreateDownProgressTween(float duration)
    {
        // 与下落位移使用相同的曲线，让帧动画按已下落距离推进。
        return DOVirtual.Float(0f, 1f, duration, SetDownProgress).SetEase(Ease.InQuad);
    }

    private void SetDownProgress(float progress)
    {
        // Luna 不注册完整状态路径的 Hash；string Play 会解析层名，并正确换算归一化进度。
        // 暂时恢复速度，让 Update 走 apply 分支，再暂停自动播放以保持距离驱动。
        waterDown.speed = 1f;
        waterDown.Play(WaterDownState, 0, Mathf.Clamp01(progress));
        waterDown.Update(0f);
        waterDown.speed = 0f;
    }

    public void HideVisuals()
    {
        visualRoot.SetActive(false);
        waterUp.gameObject.SetActive(false);
        waterDown.gameObject.SetActive(false);
    }

    public void SyncSorting()
    {
        // BallView.Awake 和此组件 Awake 的执行顺序不固定。
        if (upRenderer == null || downRenderer == null)
        {
            return;
        }

        upRenderer.sortingLayerID = body.sortingLayerID;
        downRenderer.sortingLayerID = body.sortingLayerID;
        upRenderer.sortingOrder = body.sortingOrder + 1;
        downRenderer.sortingOrder = body.sortingOrder + 1;
    }

    public Sequence CreateRevealSequence()
    {
        Sequence sequence = DOTween.Sequence();
        sequence.AppendCallback(() =>
        {
            waterUp.gameObject.SetActive(false);
            waterDown.gameObject.SetActive(false);
            SetRevealProgress(0f);
            visualRoot.SetActive(true);
        });
        sequence.Append(DOVirtual.Float(0f, 1f, revealDuration, SetRevealProgress)
            .SetEase(Ease.OutQuad));
        sequence.AppendCallback(RestoreBody);
        return sequence;
    }

    private void SetRevealProgress(float progress)
    {
        bodyTopAnchor.SetRevealProgress(progress);
    }

    private void OnDestroy()
    {
        if (upRenderer != null) upRenderer.sharedMaterial = originalUpMaterial;
        if (downRenderer != null) downRenderer.sharedMaterial = originalDownMaterial;
        if (dropMaterial != null) Destroy(dropMaterial);
    }

    public void RestoreBody()
    {
        bodyTopAnchor.SetRevealProgress(1f);
        waterUp.gameObject.SetActive(false);
        waterDown.gameObject.SetActive(false);
        visualRoot.SetActive(true);
    }
}
