using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ButtonPulse : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("要播放脉冲动画的目标；为空时使用当前物体。")]
    public Transform target;

    [Header("Animation")]
    [Min(1f)]
    [Tooltip("脉冲放大到的倍率，1 表示不放大。")]
    public float scaleMultiplier = 1.15f;
    [Min(0.01f)]
    [Tooltip("单次放大或缩回的时长。")]
    public float pulseDuration = 0.2f;
    [Min(1)]
    [Tooltip("每一轮连续播放几次放大缩回。")]
    public int pulsesPerCycle = 2;
    [Min(0f)]
    [Tooltip("每轮脉冲结束后的等待时间。")]
    public float interval = 1.2f;
    [Tooltip("启用组件时是否自动开始播放。")]
    public bool playOnEnable = true;
    [Tooltip("是否循环播放脉冲动画。")]
    public bool loop = true;
    [Tooltip("是否使用不受 Time.timeScale 影响的时间。")]
    public bool useUnscaledTime = true;
    [Tooltip("禁用组件时是否把目标缩放还原到初始值。")]
    public bool resetScaleOnDisable = true;

    private Coroutine pulseCoroutine;
    private Vector3 initialScale;

    private void Awake()
    {
        if (target == null)
        {
            target = transform;
        }

        initialScale = target.localScale;
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Stop();

        if (resetScaleOnDisable && target != null)
        {
            target.localScale = initialScale;
        }
    }

    public void Play()
    {
        if (target == null)
        {
            target = transform;
        }

        initialScale = target.localScale;
        Stop();
        pulseCoroutine = StartCoroutine(PulseLoop());
    }

    public void Stop()
    {
        if (pulseCoroutine == null)
        {
            return;
        }

        StopCoroutine(pulseCoroutine);
        pulseCoroutine = null;
    }

    public void PlayOneShot()
    {
        loop = false;
        Play();
    }

    private IEnumerator PulseLoop()
    {
        do
        {
            int pulseCount = Mathf.Max(1, pulsesPerCycle);
            for (int i = 0; i < pulseCount; i++)
            {
                yield return ScaleTo(initialScale * scaleMultiplier, pulseDuration);
                yield return ScaleTo(initialScale, pulseDuration);
            }

            if (interval > 0f)
            {
                yield return WaitForSeconds(interval);
            }
        } while (loop);

        pulseCoroutine = null;
    }

    private IEnumerator ScaleTo(Vector3 targetScale, float duration)
    {
        if (target == null)
        {
            yield break;
        }

        Vector3 startScale = target.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease in/out to keep the pulse feeling soft instead of rigid.
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            target.localScale = Vector3.LerpUnclamped(startScale, targetScale, easedT);
            yield return null;
        }

        target.localScale = targetScale;
    }

    private IEnumerator WaitForSeconds(float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += DeltaTime;
            yield return null;
        }
    }

    private float DeltaTime
    {
        get { return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime; }
    }
}
