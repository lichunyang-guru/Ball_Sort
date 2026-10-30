using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class EndPanel : MonoBehaviour
{
    public Button button;
    void Start()
    {
        button.onClick.AddListener(Download);
    }
    public void Download()
    {
        Utils.OpenStore();
    }
}
