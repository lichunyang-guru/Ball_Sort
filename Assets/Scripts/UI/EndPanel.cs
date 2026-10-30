using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class EndPanel : MonoBehaviour
{
    public Button button;
    private float openTime;
    private bool autoDownloadPending;
    private CanvasGroup canvasGroup;
    private Vector3 originalScale;
    private Sequence showSequence;

    private void Awake()
    {
        originalScale = transform.localScale;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        openTime = 0f;
        autoDownloadPending = true;
        showSequence?.Kill();
        transform.localScale = originalScale * 0.8f;
        canvasGroup.alpha = 0f;
        showSequence = DOTween.Sequence().SetUpdate(true);
        showSequence.Join(transform.DOScale(originalScale, 0.4f).SetEase(Ease.OutCubic));
        showSequence.Join(canvasGroup.DOFade(1f, 0.4f).SetEase(Ease.OutQuad));
    }

    private void OnDisable()
    {
        autoDownloadPending = false;
        showSequence?.Kill();
        showSequence = null;
        transform.localScale = originalScale;
        canvasGroup.alpha = 1f;
    }

    void Start()
    {
        if (button != null) button.onClick.AddListener(Download);
    }

    private void Update()
    {
        if (!autoDownloadPending) return;
        openTime += Time.unscaledDeltaTime;
        if (openTime >= 3f) Download();
    }

    public void Download()
    {
        autoDownloadPending = false;
        Utils.OpenStore();
    }
}
