using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(MLT_TextSwitch))]
public class MLT_TextSwitchEditor : Editor
{
    private string sourceText = string.Empty;
    private SystemLanguage sourceLanguage = SystemLanguage.ChineseSimplified;

    // JSON 导入时识别的语言英文名称。
    private readonly Dictionary<SystemLanguage, string> supportedLanguages = new Dictionary<SystemLanguage, string>
    {
        { SystemLanguage.Afrikaans, "Afrikaans" },
        { SystemLanguage.Arabic, "Arabic" },
        { SystemLanguage.Basque, "Basque" },
        { SystemLanguage.Belarusian, "Belarusian" },
        { SystemLanguage.Bulgarian, "Bulgarian" },
        { SystemLanguage.Catalan, "Catalan" },
        { SystemLanguage.Chinese, "Chinese" },
        { SystemLanguage.ChineseSimplified, "Simplified Chinese" },
        { SystemLanguage.ChineseTraditional, "Traditional Chinese" },
        { SystemLanguage.Czech, "Czech" },
        { SystemLanguage.Danish, "Danish" },
        { SystemLanguage.Dutch, "Dutch" },
        { SystemLanguage.English, "English" },
        { SystemLanguage.Estonian, "Estonian" },
        { SystemLanguage.Faroese, "Faroese" },
        { SystemLanguage.Finnish, "Finnish" },
        { SystemLanguage.French, "French" },
        { SystemLanguage.German, "German" },
        { SystemLanguage.Greek, "Greek" },
        { SystemLanguage.Hebrew, "Hebrew" },
        { SystemLanguage.Hungarian, "Hungarian" },
        { SystemLanguage.Icelandic, "Icelandic" },
        { SystemLanguage.Indonesian, "Indonesian" },
        { SystemLanguage.Italian, "Italian" },
        { SystemLanguage.Japanese, "Japanese" },
        { SystemLanguage.Korean, "Korean" },
        { SystemLanguage.Latvian, "Latvian" },
        { SystemLanguage.Lithuanian, "Lithuanian" },
        { SystemLanguage.Norwegian, "Norwegian" },
        { SystemLanguage.Polish, "Polish" },
        { SystemLanguage.Portuguese, "Portuguese" },
        { SystemLanguage.Romanian, "Romanian" },
        { SystemLanguage.Russian, "Russian" },
        { SystemLanguage.SerboCroatian, "Serbo-Croatian" },
        { SystemLanguage.Slovak, "Slovak" },
        { SystemLanguage.Slovenian, "Slovenian" },
        { SystemLanguage.Spanish, "Spanish" },
        { SystemLanguage.Swedish, "Swedish" },
        { SystemLanguage.Thai, "Thai" },
        { SystemLanguage.Turkish, "Turkish" },
        { SystemLanguage.Ukrainian, "Ukrainian" },
        { SystemLanguage.Vietnamese, "Vietnamese" },
        { SystemLanguage.Hindi, "Hindi" },
        { SystemLanguage.Unknown, "Unknown" }
    };

    private void OnEnable()
    {
        // 从 EditorPrefs 恢复源语言设置
        sourceLanguage = (SystemLanguage)EditorPrefs.GetInt("MLT_SourceLanguage", (int)SystemLanguage.ChineseSimplified);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "translationJsonText");
        serializedObject.ApplyModifiedProperties();

        var textSwitch = (MLT_TextSwitch)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("多语言文本工具", EditorStyles.boldLabel);

        DrawSourceLanguageUI();
        DrawSourceTextUI(textSwitch);
        DrawJsonImportUI(textSwitch);
        DrawCopyPromptButtonUI();
    }

    private void DrawSourceLanguageUI()
    {
        EditorGUI.BeginChangeCheck();
        sourceLanguage = (SystemLanguage)EditorGUILayout.EnumPopup("Source Language:", sourceLanguage);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetInt("MLT_SourceLanguage", (int)sourceLanguage);
        }
    }

    private void DrawSourceTextUI(MLT_TextSwitch textSwitch)
    {
        var textComponent = textSwitch.GetComponent<Text>();
        if (textComponent == null)
        {
            sourceText = string.Empty;
            return;
        }

        sourceText = textComponent.text;
        EditorGUILayout.LabelField("Source Text:");
        EditorGUILayout.TextArea(sourceText, GUILayout.Height(40));
    }

    private void DrawJsonImportUI(MLT_TextSwitch textSwitch)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("JSON Import", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "直接粘贴 JSON 文本。支持三种格式：对象映射 {\"English\":\"Hi\"}、数组 [{\"language\":\"English\",\"text\":\"Hi\"}]、包装对象 {\"translations\":...}。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        string updatedJsonText = EditorGUILayout.TextArea(textSwitch.translationJsonText ?? string.Empty, GUILayout.MinHeight(120));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(textSwitch, "Edit Translation JSON");
            textSwitch.translationJsonText = updatedJsonText;
            EditorUtility.SetDirty(textSwitch);
        }

        GUI.enabled = !string.IsNullOrWhiteSpace(textSwitch.translationJsonText);
        if (GUILayout.Button("Load Translations From JSON"))
        {
            ImportTranslationsFromJsonText(textSwitch);
        }
        GUI.enabled = true;
    }

    private void DrawCopyPromptButtonUI()
    {
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(sourceText)))
        {
            if (GUILayout.Button("Copy Prompt", GUILayout.Width(100)))
            {
                CopyTranslationPromptToClipboard();
            }
        }
    }

    private void CopyTranslationPromptToClipboard()
    {
        EditorGUIUtility.systemCopyBuffer = BuildFullTranslationPrompt(sourceLanguage, sourceText);
        Debug.Log("MLT prompt copied to clipboard.");
    }

    private string BuildFullTranslationPrompt(SystemLanguage currentSourceLanguage, string currentSourceText)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("你是一个游戏本地化翻译助手。");
        sb.AppendLine();
        sb.AppendLine("任务：");
        sb.AppendLine("把我提供的源文本翻译成当前项目支持的全部语言，并严格输出为 JSON 对象。");
        sb.AppendLine();
        sb.AppendLine("硬性要求：");
        sb.AppendLine("1. 只输出 JSON，不要输出 Markdown，不要输出解释，不要输出代码块。");
        sb.AppendLine("2. JSON 顶层必须是对象。");
        sb.AppendLine("3. JSON 的 key 必须严格使用下面这些 Unity SystemLanguage 名称，拼写必须完全一致：");
        sb.AppendLine("Afrikaans");
        sb.AppendLine("Arabic");
        sb.AppendLine("Basque");
        sb.AppendLine("Belarusian");
        sb.AppendLine("Bulgarian");
        sb.AppendLine("Catalan");
        sb.AppendLine("Chinese");
        sb.AppendLine("ChineseSimplified");
        sb.AppendLine("ChineseTraditional");
        sb.AppendLine("Czech");
        sb.AppendLine("Danish");
        sb.AppendLine("Dutch");
        sb.AppendLine("English");
        sb.AppendLine("Estonian");
        sb.AppendLine("Faroese");
        sb.AppendLine("Finnish");
        sb.AppendLine("French");
        sb.AppendLine("German");
        sb.AppendLine("Greek");
        sb.AppendLine("Hebrew");
        sb.AppendLine("Hungarian");
        sb.AppendLine("Icelandic");
        sb.AppendLine("Indonesian");
        sb.AppendLine("Italian");
        sb.AppendLine("Japanese");
        sb.AppendLine("Korean");
        sb.AppendLine("Latvian");
        sb.AppendLine("Lithuanian");
        sb.AppendLine("Norwegian");
        sb.AppendLine("Polish");
        sb.AppendLine("Portuguese");
        sb.AppendLine("Romanian");
        sb.AppendLine("Russian");
        sb.AppendLine("SerboCroatian");
        sb.AppendLine("Slovak");
        sb.AppendLine("Slovenian");
        sb.AppendLine("Spanish");
        sb.AppendLine("Swedish");
        sb.AppendLine("Thai");
        sb.AppendLine("Turkish");
        sb.AppendLine("Ukrainian");
        sb.AppendLine("Vietnamese");
        sb.AppendLine("Hindi");
        sb.AppendLine("4. 每个 key 都必须有对应 value，不能遗漏任何一种语言。");
        sb.AppendLine("5. value 必须是对应语言的翻译文本，适合手游 UI，简洁、自然、可直接上屏。");
        sb.AppendLine("6. 保留原文中的占位符、变量、富文本标签、转义字符和换行，例如：");
        sb.AppendLine("{0} {1} %s <color=red>...</color> \\n");
        sb.AppendLine("7. 不要新增任何字段，不要输出注释，不要输出数组。");
        sb.AppendLine("8. 如果源文本本身就是某种目标语言，也仍然要输出该语言字段。");
        sb.AppendLine("9. 输出必须可直接被 JSON.parse 解析。");
        sb.AppendLine("10.\"Chinese\"使用繁体。");
        sb.AppendLine();
        sb.AppendLine("游戏上下文：");
        sb.AppendLine("这是一个休闲三消/排序类手游的 UI 文案，面向全年龄用户，语气简洁、友好、自然。");
        sb.AppendLine();
        sb.AppendLine("源语言：");
        sb.AppendLine(currentSourceLanguage.ToString());
        sb.AppendLine();
        sb.AppendLine("源文本：");
        sb.AppendLine("\"" + EscapePromptSourceText(currentSourceText) + "\"");
        sb.AppendLine();
        sb.AppendLine("请严格按以下结构输出，且必须包含全部语言键：");
        sb.AppendLine("{");
        sb.AppendLine("  \"Afrikaans\": \"...\",");
        sb.AppendLine("  \"Arabic\": \"...\",");
        sb.AppendLine("  \"Basque\": \"...\",");
        sb.AppendLine("  \"Belarusian\": \"...\",");
        sb.AppendLine("  \"Bulgarian\": \"...\",");
        sb.AppendLine("  \"Catalan\": \"...\",");
        sb.AppendLine("  \"Chinese\": \"...\",");
        sb.AppendLine("  \"ChineseSimplified\": \"...\",");
        sb.AppendLine("  \"ChineseTraditional\": \"...\",");
        sb.AppendLine("  \"Czech\": \"...\",");
        sb.AppendLine("  \"Danish\": \"...\",");
        sb.AppendLine("  \"Dutch\": \"...\",");
        sb.AppendLine("  \"English\": \"...\",");
        sb.AppendLine("  \"Estonian\": \"...\",");
        sb.AppendLine("  \"Faroese\": \"...\",");
        sb.AppendLine("  \"Finnish\": \"...\",");
        sb.AppendLine("  \"French\": \"...\",");
        sb.AppendLine("  \"German\": \"...\",");
        sb.AppendLine("  \"Greek\": \"...\",");
        sb.AppendLine("  \"Hebrew\": \"...\",");
        sb.AppendLine("  \"Hungarian\": \"...\",");
        sb.AppendLine("  \"Icelandic\": \"...\",");
        sb.AppendLine("  \"Indonesian\": \"...\",");
        sb.AppendLine("  \"Italian\": \"...\",");
        sb.AppendLine("  \"Japanese\": \"...\",");
        sb.AppendLine("  \"Korean\": \"...\",");
        sb.AppendLine("  \"Latvian\": \"...\",");
        sb.AppendLine("  \"Lithuanian\": \"...\",");
        sb.AppendLine("  \"Norwegian\": \"...\",");
        sb.AppendLine("  \"Polish\": \"...\",");
        sb.AppendLine("  \"Portuguese\": \"...\",");
        sb.AppendLine("  \"Romanian\": \"...\",");
        sb.AppendLine("  \"Russian\": \"...\",");
        sb.AppendLine("  \"SerboCroatian\": \"...\",");
        sb.AppendLine("  \"Slovak\": \"...\",");
        sb.AppendLine("  \"Slovenian\": \"...\",");
        sb.AppendLine("  \"Spanish\": \"...\",");
        sb.AppendLine("  \"Swedish\": \"...\",");
        sb.AppendLine("  \"Thai\": \"...\",");
        sb.AppendLine("  \"Turkish\": \"...\",");
        sb.AppendLine("  \"Ukrainian\": \"...\",");
        sb.AppendLine("  \"Vietnamese\": \"...\",");
        sb.AppendLine("  \"Hindi\": \"...\"");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string EscapePromptSourceText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");
    }

    private void ImportTranslationsFromJsonText(MLT_TextSwitch textSwitch)
    {
        if (textSwitch == null || string.IsNullOrWhiteSpace(textSwitch.translationJsonText))
        {
            EditorUtility.DisplayDialog("JSON Import Failed", "请先粘贴 translationJsonText。", "OK");
            return;
        }

        if (!TryParseTranslationsJson(
                textSwitch.translationJsonText,
                out Dictionary<SystemLanguage, string> translations,
                out string errorMessage))
        {
            EditorUtility.DisplayDialog("JSON Import Failed", errorMessage, "OK");
            return;
        }

        List<SystemLanguage> orderedLanguages = new List<SystemLanguage>(translations.Keys);

        Undo.RecordObject(textSwitch, "Import MLT Text JSON");
        ApplyTranslationsToComponent(textSwitch, translations, sourceLanguage, orderedLanguages);
        EditorUtility.SetDirty(textSwitch);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("JSON Import Success", $"Imported {translations.Count} translations.", "OK");
    }

    private void ApplyTranslationsToComponent(
        MLT_TextSwitch textSwitch,
        Dictionary<SystemLanguage, string> translations,
        SystemLanguage sourceLang,
        List<SystemLanguage> targetLanguages)
    {
        if (textSwitch == null)
        {
            return;
        }

        if (textSwitch.languageTexts == null)
        {
            textSwitch.languageTexts = new List<MLT_TextSwitch.LanguageText>();
        }
        else
        {
            textSwitch.languageTexts.Clear();
        }

        HashSet<SystemLanguage> used = new HashSet<SystemLanguage>();

        AddTranslationIfExists(textSwitch.languageTexts, translations, sourceLang, used);

        for (int i = 0; i < targetLanguages.Count; i++)
        {
            AddTranslationIfExists(textSwitch.languageTexts, translations, targetLanguages[i], used);
        }

        foreach (KeyValuePair<SystemLanguage, string> kv in translations)
        {
            if (used.Contains(kv.Key))
            {
                continue;
            }

            if (string.IsNullOrEmpty(kv.Value))
            {
                continue;
            }

            textSwitch.languageTexts.Add(new MLT_TextSwitch.LanguageText
            {
                language = kv.Key,
                text = kv.Value
            });
            used.Add(kv.Key);
        }
    }

    private void AddTranslationIfExists(
        List<MLT_TextSwitch.LanguageText> output,
        Dictionary<SystemLanguage, string> translations,
        SystemLanguage language,
        HashSet<SystemLanguage> used)
    {
        string translatedText;
        if (used.Contains(language) || !translations.TryGetValue(language, out translatedText) || string.IsNullOrEmpty(translatedText))
        {
            return;
        }

        output.Add(new MLT_TextSwitch.LanguageText
        {
            language = language,
            text = translatedText
        });
        used.Add(language);
    }

    private bool TryParseTranslationsJson(
        string rawJson,
        out Dictionary<SystemLanguage, string> translations,
        out string errorMessage)
    {
        translations = new Dictionary<SystemLanguage, string>();
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            errorMessage = "JSON 内容为空。";
            return false;
        }

        try
        {
            string sanitizedJson = StripJsonComments(rawJson);
            JToken root = JToken.Parse(sanitizedJson);
            return TryExtractTranslations(root, translations, out errorMessage);
        }
        catch (Exception ex)
        {
            errorMessage = "JSON 解析失败: " + ex.Message;
            return false;
        }
    }

    private bool TryExtractTranslations(
        JToken root,
        Dictionary<SystemLanguage, string> translations,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (root == null)
        {
            errorMessage = "JSON 根节点为空。";
            return false;
        }

        if (root.Type == JTokenType.Array)
        {
            return TryReadTranslationsFromArray((JArray)root, translations, out errorMessage);
        }

        if (root.Type != JTokenType.Object)
        {
            errorMessage = "JSON 顶层必须是对象或数组。";
            return false;
        }

        JObject obj = (JObject)root;

        JToken wrappedTranslations;
        if (obj.TryGetValue("languageTexts", StringComparison.OrdinalIgnoreCase, out wrappedTranslations) ||
            obj.TryGetValue("translations", StringComparison.OrdinalIgnoreCase, out wrappedTranslations) ||
            obj.TryGetValue("items", StringComparison.OrdinalIgnoreCase, out wrappedTranslations))
        {
            if (wrappedTranslations.Type == JTokenType.Array)
            {
                return TryReadTranslationsFromArray((JArray)wrappedTranslations, translations, out errorMessage);
            }

            if (wrappedTranslations.Type == JTokenType.Object)
            {
                return TryReadTranslationsFromObject((JObject)wrappedTranslations, translations, out errorMessage);
            }
        }

        return TryReadTranslationsFromObject(obj, translations, out errorMessage);
    }

    private bool TryReadTranslationsFromArray(
        JArray array,
        Dictionary<SystemLanguage, string> translations,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        for (int i = 0; i < array.Count; i++)
        {
            JObject item = array[i] as JObject;
            if (item == null)
            {
                errorMessage = $"第 {i + 1} 项不是对象，无法解析。";
                return false;
            }

            string languageName = GetJsonString(item, "language", "lang", "key", "name");
            string translatedText = GetJsonString(item, "text", "value", "translation", "content");

            if (string.IsNullOrWhiteSpace(languageName))
            {
                errorMessage = $"第 {i + 1} 项缺少 language 字段。";
                return false;
            }

            if (!TryGetLanguageFromJsonKey(languageName, out SystemLanguage language))
            {
                errorMessage = $"无法识别的语言: {languageName}";
                return false;
            }

            translations[language] = translatedText ?? string.Empty;
        }

        if (translations.Count == 0)
        {
            errorMessage = "没有读取到任何翻译项。";
            return false;
        }

        return true;
    }

    private bool TryReadTranslationsFromObject(
        JObject obj,
        Dictionary<SystemLanguage, string> translations,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        foreach (KeyValuePair<string, JToken> entry in obj)
        {
            if (entry.Value == null || entry.Value.Type == JTokenType.Null)
            {
                continue;
            }

            if (!TryGetLanguageFromJsonKey(entry.Key, out SystemLanguage language))
            {
                continue;
            }

            translations[language] = entry.Value.Type == JTokenType.String
                ? entry.Value.ToString()
                : entry.Value.ToString(Formatting.None);
        }

        if (translations.Count == 0)
        {
            errorMessage = "没有找到可识别的语言键。";
            return false;
        }

        return true;
    }

    private string GetJsonString(JObject obj, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            JToken value;
            if (obj.TryGetValue(keys[i], StringComparison.OrdinalIgnoreCase, out value) &&
                value != null &&
                value.Type != JTokenType.Null)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private bool TryGetLanguageFromJsonKey(string languageName, out SystemLanguage language)
    {
        if (TryGetLanguageFromName(languageName, out language))
        {
            return true;
        }

        if (Enum.TryParse(languageName, true, out language))
        {
            return true;
        }

        string normalized = NormalizeLanguageKey(languageName);
        foreach (SystemLanguage candidate in Enum.GetValues(typeof(SystemLanguage)))
        {
            if (NormalizeLanguageKey(candidate.ToString()) == normalized)
            {
                language = candidate;
                return true;
            }

            if (NormalizeLanguageKey(GetLanguageDisplayName(candidate)) == normalized)
            {
                language = candidate;
                return true;
            }
        }

        language = SystemLanguage.Unknown;
        return false;
    }

    private string NormalizeLanguageKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"[\s_\-()]+", string.Empty).ToLowerInvariant();
    }

    private string StripJsonComments(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        string withoutBlockComments = Regex.Replace(input, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlockComments, @"^\s*//.*$", string.Empty, RegexOptions.Multiline);
    }

    private bool TryGetLanguageFromName(string languageName, out SystemLanguage language)
    {
        foreach (KeyValuePair<SystemLanguage, string> kv in supportedLanguages)
        {
            if (string.Equals(kv.Value, languageName, StringComparison.OrdinalIgnoreCase))
            {
                language = kv.Key;
                return true;
            }
        }

        language = SystemLanguage.Unknown;
        return false;
    }

    // 获取语言显示名称
    private static string GetLanguageDisplayName(SystemLanguage language)
    {
        switch (language)
        {
            case SystemLanguage.Afrikaans: return "Afrikaans";
            case SystemLanguage.Arabic: return "العربية";
            case SystemLanguage.Basque: return "Euskera";
            case SystemLanguage.Belarusian: return "Беларуская";
            case SystemLanguage.Bulgarian: return "Български";
            case SystemLanguage.Catalan: return "Català";
            case SystemLanguage.Chinese: return "中文";
            case SystemLanguage.Czech: return "Čeština";
            case SystemLanguage.Danish: return "Dansk";
            case SystemLanguage.Dutch: return "Nederlands";
            case SystemLanguage.English: return "English";
            case SystemLanguage.Estonian: return "Eesti";
            case SystemLanguage.Faroese: return "Føroyskt";
            case SystemLanguage.Finnish: return "Suomi";
            case SystemLanguage.French: return "Français";
            case SystemLanguage.German: return "Deutsch";
            case SystemLanguage.Greek: return "Ελληνικά";
            case SystemLanguage.Hebrew: return "עברית";
            case SystemLanguage.Hungarian: return "Magyar";
            case SystemLanguage.Icelandic: return "Íslenska";
            case SystemLanguage.Indonesian: return "Bahasa Indonesia";
            case SystemLanguage.Italian: return "Italiano";
            case SystemLanguage.Japanese: return "日本語";
            case SystemLanguage.Korean: return "한국어";
            case SystemLanguage.Latvian: return "Latviešu";
            case SystemLanguage.Lithuanian: return "Lietuvių";
            case SystemLanguage.Norwegian: return "Norsk";
            case SystemLanguage.Polish: return "Polski";
            case SystemLanguage.Portuguese: return "Português";
            case SystemLanguage.Romanian: return "Română";
            case SystemLanguage.Russian: return "Русский";
            case SystemLanguage.SerboCroatian: return "Srpsko-hrvatski";
            case SystemLanguage.Slovak: return "Slovenčina";
            case SystemLanguage.Slovenian: return "Slovenščina";
            case SystemLanguage.Spanish: return "Español";
            case SystemLanguage.Swedish: return "Svenska";
            case SystemLanguage.Thai: return "ไทย";
            case SystemLanguage.Turkish: return "Türkçe";
            case SystemLanguage.Ukrainian: return "Українська";
            case SystemLanguage.Vietnamese: return "Tiếng Việt";
            case SystemLanguage.ChineseSimplified: return "简体中文";
            case SystemLanguage.ChineseTraditional: return "繁體中文";
            case SystemLanguage.Hindi: return "हिन्दी";
            default: return language.ToString();
        }
    }
}
