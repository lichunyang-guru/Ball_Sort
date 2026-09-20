
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameConfig : MonoSingleton<GameConfig>
{
    [Tooltip("是否允许首次调用 PlayBGM 时播放背景音乐。")]
    public bool isEnableBGM = true;
    [LunaPlaygroundField("每轮点击几次后跳转商店", 0, "游戏跳转参数")]
    [Tooltip("玩家点击试管累计达到该次数后触发商店跳转。")]
    public int targetStepCount = 20;
    [LunaPlaygroundField("跳转商店几轮后，每次点击都会直接跳转", 0, "游戏跳转参数")]
    [Tooltip("点击跳转循环轮数；递减到 0 后，每次点击都会触发商店跳转。")]
    public int cycleStepCount = 3;
    [LunaPlaygroundField("满管几根后跳转商店", 0, "游戏跳转参数")]
    [Tooltip("玩家完成长管数量达到该值后触发商店跳转。")]
    public int targetCompleteCount = 1;
    [LunaPlaygroundField("完成第几关自动跳转", 0, "游戏跳转参数")]
    [Tooltip("玩家通关次数达到该值后触发商店跳转。")]
    public int targetLevelCount = 1;
    int curStepCount = 0;
    int curCompleteCount = 0;
    int curLevelCount = 0;
    [LunaPlaygroundField("空闲多长时间后跳转商店", 0, "游戏跳转参数")]
    [Tooltip("玩家空闲超过该秒数后，下一次操作会触发商店跳转。")]
    public float MaxIdleTime = 5f;
    float curIdleTime = 0f;
    bool isStartIdle = false;
    bool isPlayBGM = false;
    bool hasReceivedScreenInput = false;

    void Update()
    {
        TryPlayBGMOnInput();

        if (isStartIdle)
        {
            curIdleTime += Time.deltaTime;
        }
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

    public void AddTargetCompleteCount()
    {
        curCompleteCount++;
        if (curCompleteCount >= targetCompleteCount)
        {
            Utils.OpenStore();
        }
    }

    public void AddTargetLevelCount()
    {
        curLevelCount++;
        if (curLevelCount >= targetLevelCount)
        {
            Utils.OpenStore();
        }
    }

    public void OpRecord()
    {
        isStartIdle = true;
        if (curIdleTime >= MaxIdleTime)
        {
            Utils.OpenStore();
        }
        curIdleTime = 0f;
    }

    public void PlayBGM()
    {
        if (isPlayBGM)
        {
            return;
        }

        if (isEnableBGM && AudioMgr.Instance != null && AudioMgr.Instance.BGM != null)
        {
            isPlayBGM = AudioMgr.Instance.Play(AudioMgr.Instance.BGM.name, AudioMgr.AudioType.BGM, true);
        }
    }

    private void TryPlayBGMOnInput()
    {
        if (isPlayBGM || !isEnableBGM)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            hasReceivedScreenInput = true;
        }

        if (hasReceivedScreenInput)
        {
            PlayBGM();
        }
    }
}
