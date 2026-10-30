using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIMgr : MonoSingleton<UIMgr>
{
    private Dictionary<string, GameObject> uiDict = new Dictionary<string, GameObject>();
    protected override void Awake()
    {
        base.Awake();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            for (int j = 0; j < child.childCount; j++)
            {
                var uiItem = child.GetChild(j);
                uiDict.Add(uiItem.name, uiItem.gameObject);
            }
        }
    }

    public void ShowUI<T>() where T : MonoBehaviour
    {
        ShowUI(typeof(T).Name);
    }

    public void ShowUI(string uiName = "")
    {
        if (uiDict.TryGetValue(uiName, out var uiItem))
        {
            uiItem.transform.SetAsLastSibling();
            uiItem.SetActive(true);
        }
        else
        {
            Debug.LogError($"不存在该面板，请检查：{uiName}");
        }
    }

    public void HideUI<T>() where T : MonoBehaviour
    {
        HideUI(typeof(T).Name);
    }

    public void HideUI(string uiName = "")
    {
        if (uiDict.TryGetValue(uiName, out var uiItem))
        {
            uiItem.SetActive(false);
        }
        else
        {
            Debug.LogError($"不存在该面板，请检查：{uiName}");
        }
    }

    public T GetUI<T>() where T : MonoBehaviour
    {
        var uiName = typeof(T).Name;
        if (uiDict.TryGetValue(uiName, out var uiItem))
        {
            T panelSrc = uiItem.GetComponent<T>();
            if (panelSrc != null)
            {
                return panelSrc;
            }
            else
            {
                Debug.LogError($"请检查UI脚本:{uiName}");
            }
        }
        else
        {
            Debug.LogError($"不存在该面板，请检查：{uiName}");
        }
        return null;
    }
    
}