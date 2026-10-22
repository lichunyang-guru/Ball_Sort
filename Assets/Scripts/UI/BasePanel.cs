using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class BasePanel : MonoBehaviour
{
    [Tooltip("需要循环播放放大缩小提示动画的按钮列表。")]
    public List<GameObject> btns;

    void Start()
    {
        btns.ForEach(obj => 
        {
            DoBtnAnim(obj);
        });
    }

    void DoBtnAnim(GameObject btn)
    {
        if (btn == null) return;
        Vector3 targetScale = 1.2f * btn.transform.localScale;
        btn.transform.DOScale(targetScale, 0.3f).SetEase(Ease.InOutQuad).SetLoops(4, LoopType.Yoyo).OnComplete(() =>
        {
            TimerMgr.Instance.CreateNewTimer(6f, () =>
            {
                DoBtnAnim(btn);
            });
        });
    }

    public void Download()
    {
        Utils.OpenStore();
    }
}
