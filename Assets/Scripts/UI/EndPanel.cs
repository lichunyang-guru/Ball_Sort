using UnityEngine;
using UnityEngine.UI;

public class EndPanel : MonoBehaviour
{
    public Button button;
    private float openTime;
    private bool autoDownloadPending;

    private void OnEnable()
    {
        openTime = 0f;
        autoDownloadPending = true;
    }

    private void OnDisable()
    {
        autoDownloadPending = false;
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
