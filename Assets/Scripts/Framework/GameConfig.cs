using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GameConfig : MonoSingleton<GameConfig>
{
    public bool isEnableBGM = true;
    [LunaPlaygroundField("按钮颜色", 0, "视觉配置")]
    public Color buttonColor = new Color32(159, 3, 3, 255);
    [LunaPlaygroundField("蜡烛白色描边大小（px，0关闭）", 0, "视觉配置")]
    [Min(0f)] public float candleOutlineWidth = 1.5f;
    [LunaPlaygroundField("每轮点击几次后跳转商店", 0, "游戏跳转参数")]
    public int targetStepCount = 50;
    [LunaPlaygroundField("跳转商店几轮后，每次点击都会直接跳转", 0, "游戏跳转参数")]
    public int cycleStepCount = 1;
    [FormerlySerializedAs("targetCompleteCount")]
    [LunaPlaygroundField("满管几根后跳转商店", 0, "游戏跳转参数")]
    public int targetCompletedTubeCount = 4;
    public int targetLevelCount = 1;
    public float MaxIdleTime = 5f;

    private int curStepCount;
    private int curCompletedTubeCount;
    private int curLevelCount;
    private float curIdleTime;
    private bool isStartIdle;
    private bool isPlayBGM;
    private bool completedTubeRedirectInvoked;

    private void Start()
    {
        ApplyLegacyButtonColors();
    }

    private void Update()
    {
        TryPlayBGMOnInput();
        if (isStartIdle) curIdleTime += Time.deltaTime;
    }

    public void AddTargetStepCount()
    {
        curStepCount++;
        if (curStepCount >= targetStepCount || cycleStepCount <= 0)
        {
            cycleStepCount--;
            curStepCount = 0;
            Utils.OpenStore();
        }
    }

    public void OnTubeCompleted()
    {
        if (completedTubeRedirectInvoked) return;
        curCompletedTubeCount++;
        if (curCompletedTubeCount < Mathf.Max(1, targetCompletedTubeCount)) return;
        completedTubeRedirectInvoked = true;
        Utils.OpenStore();
    }

    // The supplied playable records these counters but does not redirect on them.
    public void AddTargetLevelCount()
    {
        curLevelCount++;
    }

    public void OpRecord()
    {
        isStartIdle = true;
        curIdleTime = 0f;
    }

    public void PlayBGM()
    {
        if (isPlayBGM) return;
        if (isEnableBGM && AudioMgr.Instance != null && AudioMgr.Instance.BGM != null)
            isPlayBGM = AudioMgr.Instance.Play(AudioMgr.Instance.BGM.name, AudioMgr.AudioType.BGM, true);
    }

    private void TryPlayBGMOnInput()
    {
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0) PlayBGM();
    }

    private void ApplyLegacyButtonColors()
    {
        // Include both orientation variants before either is first displayed.
        foreach (Canvas canvas in FindObjectsOfType<Canvas>())
        foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
        {
            if (button == null || (button.name != "Button (Legacy)" && button.name != "Button (Legacy) (1)")) continue;
            ColorBlock colors = button.colors;
            Color displayColor = buttonColor;
#if !UNITY_LUNA
            // The source Luna renderer encodes UI vertex colours from linear to sRGB.
            displayColor = buttonColor.gamma;
#endif
            colors.normalColor = displayColor;
            colors.highlightedColor = displayColor;
            colors.pressedColor = displayColor;
            colors.selectedColor = displayColor;
            colors.disabledColor = displayColor;
            button.colors = colors;
        }
    }
}
