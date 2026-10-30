using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class MLT_TextSwitch : MonoBehaviour
{
    // 使用Dictionary存储语言和文本的映射关系
    [System.Serializable]
    public class LanguageText
    {
        public SystemLanguage language;
        [TextArea]
        public string text;
    }

    [Header("语言文本配置")]
    public List<LanguageText> languageTexts = new List<LanguageText>();

    [Header("JSON导入配置")]
    [Tooltip("直接粘贴多语言JSON文本，可在Inspector里一键读取到 languageTexts。")]
    [TextArea(6, 20)]
    public string translationJsonText;

    private Text txt;

    private void Awake()
    {
        txt = GetComponent<Text>();
        Switch();
        EventCenter.Instance.AddEventListener("语言切换", Switch);
    }

    void Switch()
    {
        // 获取当前系统语言
        SystemLanguage currentLang = GameLanguageManager.Instance.CurrentLanguage;

        // 查找对应语言的文本
        foreach (var langText in languageTexts)
        {
            if (langText.language == currentLang)
            {
                txt.text = langText.text;
                return;
            }
        }

        // 如果没有找到对应语言，尝试使用英语
        foreach (var langText in languageTexts)
        {
            if (langText.language == SystemLanguage.English)
            {
                txt.text = langText.text;
                return;
            }
        }

        // 如果连英语都没有，使用第一个可用的文本
        if (languageTexts.Count > 0)
        {
            txt.text = languageTexts[0].text;
        }
    }

    private void OnDestroy()
    {
        EventCenter.Instance.RemoveEventListener("语言切换", Switch);
    }
}
