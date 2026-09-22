using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音频管理器，继承自MonoSingleton
/// </summary>
public class AudioMgr : MonoSingleton<AudioMgr>
{
    private static readonly Dictionary<string, string[]> AudioNameAliases = new Dictionary<string, string[]>
    {
        { "点击", new[] { "手指点击" } },
        { "下落", new[] { "球下落" } },
        { "完成", new[] { "合成完成音效" } }
    };

    /// <summary>
    /// 音频类型
    /// </summary>
    public enum AudioType
    {
        BGM,    // 背景音乐
        Effect     // 音效
    }

    [Header("音频配置")]
    [LunaPlaygroundAsset("BGM", 0, "资源文件")]
    [Tooltip("背景音乐资源，也会暴露到 Luna Playground 中配置。")]
    public AudioClip BGM;
    [Tooltip("所有可通过名称播放的音效资源列表，播放时按 AudioClip.name 查找。")]
    public List<AudioClip> audioClips = new List<AudioClip>();

    [Header("音量设置")]
    [Tooltip("背景音乐音量，范围建议为 0 到 1。")]
    [SerializeField]
    private float bgmVolume = 0.7f;

    [LunaPlaygroundField("音效音量（0-1）", 0, "游戏配置参数")]
    [Range(0f, 1f)]
    [Tooltip("音效音量，会暴露到 Luna Playground 中配置。")]
    [SerializeField]
    private float effectVolume = 1f;
    [Tooltip("启动时预创建的音效 AudioSource 数量，避免首次密集播放音效时动态 AddComponent。")]
    [SerializeField]
    private int prewarmEffectSourceCount = 8;

    // 存储所有AudioSource组件
    private List<AudioSource> audioSources = new List<AudioSource>();

    // 背景音乐专用的AudioSource
    private AudioSource bgmSource;
    private bool hasReceivedScreenInput;

    public bool HasReceivedScreenInput
    {
        get
        {
            CaptureScreenInput();
            return hasReceivedScreenInput;
        }
    }

    private void Update()
    {
        CaptureScreenInput();
    }

    private void CaptureScreenInput()
    {
        if (hasReceivedScreenInput) return;
        if (Input.GetMouseButtonDown(0))
        {
            hasReceivedScreenInput = true;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            TouchPhase phase = Input.GetTouch(i).phase;
            if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled)
            {
                hasReceivedScreenInput = true;
                return;
            }
        }
    }

    /// <summary>
    /// BGM音量属性
    /// </summary>
    public float BGMVolume
    {
        get { return bgmVolume; }
        set
        {
            bgmVolume = Mathf.Clamp01(value);
            if (bgmSource != null)
            {
                bgmSource.volume = bgmVolume;
            }
        }
    }

    /// <summary>
    /// Effect音量属性
    /// </summary>
    public float EffectVolume
    {
        get { return effectVolume; }
        set
        {
            effectVolume = Mathf.Clamp01(value);
            // 更新所有音效AudioSource的音量
            foreach (var source in audioSources)
            {
                if (source != null && source != bgmSource && source.isPlaying)
                {
                    source.volume = effectVolume;
                }
            }
        }
    }

    protected override void Awake()
    {
        base.Awake();

        // 初始化BGM专用AudioSource
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume;
        audioSources.Add(bgmSource);

        PrewarmEffectAudioSources();
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    protected override void OnDestroy()
    {
        // 停止所有音频
        StopAll();

        // 清理AudioSource列表
        audioSources.Clear();

        // 调用基类的OnDestroy，清理静态实例
        base.OnDestroy();
    }

    /// <summary>
    /// 播放音频
    /// </summary>
    /// <param name="audioName">音频名称</param>
    /// <param name="audioType">音频类型</param>
    /// <param name="loop">是否循环</param>
    public bool Play(string audioName, AudioType audioType = AudioType.Effect, bool loop = false)
    {
        // 所有声音都等首次触屏后播放；在调用处同步检查，避免首触帧的执行顺序影响音效。
        if (!HasReceivedScreenInput) return false;

        // 查找音频文件
        AudioClip clip = FindAudioClip(audioName);
        if (clip == null)
        {
            Debug.LogError($"未找到音频文件: {audioName}");
            return false;
        }

        AudioSource source = null;

        if (audioType == AudioType.BGM)
        {
            // BGM使用专用的AudioSource
            source = bgmSource;
            source.volume = bgmVolume;
        }
        else
        {
            // Effect查找或创建AudioSource
            source = GetAvailableAudioSource();
            source.volume = effectVolume;
        }

        if (source != null)
        {
#if !UNITY_LUNA
            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
#endif

            source.clip = clip;
            source.loop = loop;
            source.Play();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 暂停音频
    /// </summary>
    /// <param name="audioName">音频名称</param>
    public void Pause(string audioName)
    {
        foreach (var source in audioSources)
        {
            if (source != null && source.clip != null && source.clip.name == audioName && source.isPlaying)
            {
                source.Pause();
            }
        }
    }

    /// <summary>
    /// 暂停所有音频
    /// </summary>
    public void PauseAll()
    {
        foreach (var source in audioSources)
        {
            if (source != null && source.isPlaying)
            {
                source.Pause();
            }
        }
    }

    /// <summary>
    /// 停止音频
    /// </summary>
    /// <param name="audioName">音频名称</param>
    public void Stop(string audioName)
    {
        foreach (var source in audioSources)
        {
            if (source != null && source.clip != null && source.clip.name == audioName)
            {
                source.Stop();
            }
        }
    }

    /// <summary>
    /// 停止所有音频
    /// </summary>
    public void StopAll()
    {
        foreach (var source in audioSources)
        {
            if (source != null)
            {
                source.Stop();
            }
        }
    }

    /// <summary>
    /// 查找音频文件
    /// </summary>
    /// <param name="audioName">音频名称</param>
    /// <returns>找到的AudioClip</returns>
    private AudioClip FindAudioClip(string audioName)
    {
        AudioClip clip = FindAudioClipExact(audioName);
        if (clip != null)
        {
            return clip;
        }

        if (AudioNameAliases.TryGetValue(audioName, out string[] aliases))
        {
            foreach (string alias in aliases)
            {
                clip = FindAudioClipExact(alias);
                if (clip != null)
                {
                    return clip;
                }
            }
        }

        return null;
    }

    private AudioClip FindAudioClipExact(string audioName)
    {
        foreach (var clip in audioClips)
        {
            if (clip != null && clip.name == audioName)
            {
                return clip;
            }
        }
        if (BGM != null && BGM.name == audioName)
        {
            return BGM;
        }
        return null;
    }

    /// <summary>
    /// 获取可用的AudioSource组件
    /// </summary>
    /// <returns>可用的AudioSource</returns>
    private AudioSource GetAvailableAudioSource()
    {
        // 清理已销毁的AudioSource
        audioSources.RemoveAll(source => source == null);

        // 查找闲置的AudioSource（排除BGM专用的）
        foreach (var source in audioSources)
        {
            if (source != null && source != bgmSource && !source.isPlaying)
            {
                return source;
            }
        }

        // 没有闲置的，创建新的AudioSource
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        audioSources.Add(newSource);
        return newSource;
    }

    private void PrewarmEffectAudioSources()
    {
        int count = Mathf.Max(0, prewarmEffectSourceCount);
        for (int i = 0; i < count; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = effectVolume;
            audioSources.Add(source);
        }
    }

    /// <summary>
    /// 设置BGM音量
    /// </summary>
    /// <param name="volume">音量值(0-1)</param>
    public void SetBGMVolume(float volume)
    {
        BGMVolume = volume;
    }

    /// <summary>
    /// 设置Effect音量
    /// </summary>
    /// <param name="volume">音量值(0-1)</param>
    public void SetEffectVolume(float volume)
    {
        EffectVolume = volume;
    }
}
