using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class SpriteFillCameraWidth : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [Min(0f)] [SerializeField] private float edgeOverlapWorld = 0.05f;

    private SpriteRenderer spriteRenderer;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        FitWidth();
    }

    private void LateUpdate()
    {
        FitWidth();
    }

    private void FitWidth()
    {
        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null || !cameraToUse.orthographic || cameraToUse.aspect <= 0f)
        {
            return;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer.sprite == null)
        {
            return;
        }

        float parentScaleX = transform.parent != null ? Mathf.Abs(transform.parent.lossyScale.x) : 1f;
        float unscaledWidth = spriteRenderer.sprite.bounds.size.x * parentScaleX;
        if (unscaledWidth <= Mathf.Epsilon)
        {
            return;
        }

        float visibleWidth = cameraToUse.orthographicSize * 2f * cameraToUse.aspect;
        float requiredScaleX = (visibleWidth + edgeOverlapWorld) / unscaledWidth;
        Vector3 scale = transform.localScale;
        float signedScaleX = Mathf.Sign(scale.x) * requiredScaleX;
        if (!Mathf.Approximately(scale.x, signedScaleX))
        {
            scale.x = signedScaleX;
            transform.localScale = scale;
        }

        Vector3 position = transform.position;
        float cameraX = cameraToUse.transform.position.x;
        if (!Mathf.Approximately(position.x, cameraX))
        {
            position.x = cameraX;
            transform.position = position;
        }
    }
}
