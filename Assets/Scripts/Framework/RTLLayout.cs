using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RTLLayout : MonoBehaviour
{
    void Start()
    {
        ChangeLayout();
    }

    void ChangeLayout()
    {
        var curLanguage = GameLanguageManager.Instance.CurrentLanguage;
        // 定义需要RTL处理的语言
        HashSet<SystemLanguage> rtlLanguages = new HashSet<SystemLanguage>
        {
            SystemLanguage.Arabic,
            SystemLanguage.Hebrew
            // 可以根据需要添加更多RTL语言
        };

        // 如果是RTL语言，则反转内容
        if (rtlLanguages.Contains(curLanguage))
        {
            // 按X坐标从左到右排序，再反转位置实现RTL
            int childCount = transform.childCount;
            if (childCount <= 1) return;

            List<Transform> children = new List<Transform>(childCount);
            for (int i = 0; i < childCount; i++)
            {
                children.Add(transform.GetChild(i));
            }

            var sorted = children.OrderBy(t => t.localPosition.x).ToList();
            List<Vector3> positions = sorted.Select(t => t.localPosition).ToList();

            for (int i = 0; i < childCount; i++)
            {
                sorted[i].localPosition = positions[childCount - 1 - i];
            }
        }
    }
}