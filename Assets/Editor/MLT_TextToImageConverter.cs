using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将MLT_TextSwitch组件的多语言文本转换为图片资源
/// </summary>
public class MLT_TextToImageConverter : EditorWindow
{
    private Vector2 scrollPos;
    private List<TextToImageItem> conversionItems = new List<TextToImageItem>();
    private bool isConverting = false;
    private float progress = 0f;
    private string progressMessage = "";
    private string outputPath = "Assets/Textures/GeneratedTextImages";
    private int imageScale = 1; // 图片缩放倍数
    private Color backgroundColor = new Color(0, 0, 0, 0); // 透明背景
    private int padding = 10; // 文本周围的内边距
    private TextAnchor textAnchor = TextAnchor.MiddleCenter;
    private bool antiAliasing = true;
    private TextureFormat textureFormat = TextureFormat.RGBA32;
    private bool compressTexture = true;
    private int maxTextureSize = 256; // 最大纹理尺寸
    private bool isNativeSize = false;
    private bool isUseTextSize = false;

    [System.Serializable]
    internal class TextToImageItem
    {
        public GameObject gameObject;
        public Text textComponent;
        public MLT_TextSwitch textSwitch;
        public MLT_ImageSwitch imageSwitch;
        public bool selected;
        public string gameObjectPath;
        public List<LanguageTextPreview> languagePreviews = new List<LanguageTextPreview>();

        [System.Serializable]
        public class LanguageTextPreview
        {
            public SystemLanguage language;
            public string text;
            public bool willConvert;
        }

        public TextToImageItem(GameObject obj)
        {
            gameObject = obj;
            textComponent = obj.GetComponent<Text>();
            if (textComponent == null)
            {
                Debug.LogWarning($"GameObject {obj.name} 没有Text组件，无法转换");
                return;
            }

            textSwitch = obj.GetComponent<MLT_TextSwitch>();
            imageSwitch = obj.GetComponent<MLT_ImageSwitch>();
            selected = true;
            gameObjectPath = GetGameObjectPath(obj);

            // 生成语言预览
            if (textSwitch != null)
            {
                foreach (var langText in textSwitch.languageTexts)
                {
                    if (!string.IsNullOrEmpty(langText.text))
                    {
                        languagePreviews.Add(new LanguageTextPreview
                        {
                            language = langText.language,
                            text = langText.text,
                            willConvert = true
                        });
                    }
                }
            }
            else
            {
                Debug.LogWarning($"GameObject {obj.name} 没有MLT_TextSwitch组件");
            }
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }

    [MenuItem("Tools/MLT Translation/Text to Image Converter")]
    public static void ShowWindow()
    {
        var window = GetWindow<MLT_TextToImageConverter>("MLT Text to Image Converter");
        window.minSize = new Vector2(800, 600);
    }

    private void OnEnable()
    {
        RefreshItemList();
    }

    private void RefreshItemList()
    {
        conversionItems.Clear();

        // 查找所有带有MLT_TextSwitch的对象
        var allTextSwitches = Resources.FindObjectsOfTypeAll<MLT_TextSwitch>()
            .Where(t => !EditorUtility.IsPersistent(t) && t.gameObject.scene.name != null)
            .ToArray();

        foreach (var textSwitch in allTextSwitches)
        {
            var item = new TextToImageItem(textSwitch.gameObject);
            if (item.languagePreviews.Count > 0 && item.textComponent != null)
            {
                conversionItems.Add(item);
                Debug.Log($"添加转换项: {item.gameObject?.name}, 语言数量: {item.languagePreviews.Count}");
            }
            else if (item.textComponent == null)
            {
                Debug.LogWarning($"跳过对象 {textSwitch.gameObject.name}: 没有找到Text组件");
            }
            else
            {
                Debug.LogWarning($"跳过对象 {textSwitch.gameObject.name}: 没有语言预览或Text组件为null");
            }
        }

        Debug.Log($"找到 {conversionItems.Count} 个可转换的文本对象");
    }

    private void OnGUI()
    {
        // 标题
        EditorGUILayout.LabelField("文本转图片批量转换工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 输出设置
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);

        outputPath = EditorGUILayout.TextField("输出路径", outputPath);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("选择文件夹", GUILayout.Width(100)))
        {
            string path = EditorUtility.OpenFolderPanel("选择输出文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                outputPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        if (GUILayout.Button("创建文件夹", GUILayout.Width(100)))
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                AssetDatabase.Refresh();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        // 图片设置
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("图片设置", EditorStyles.boldLabel);

        imageScale = EditorGUILayout.IntSlider("渲染缩放", imageScale, 1, 4);
        backgroundColor = EditorGUILayout.ColorField("背景颜色", backgroundColor);
        padding = EditorGUILayout.IntSlider("内边距", padding, 0, 50);
        textAnchor = (TextAnchor)EditorGUILayout.EnumPopup("文本对齐", textAnchor);
        antiAliasing = EditorGUILayout.Toggle("抗锯齿", antiAliasing);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("压缩设置", EditorStyles.miniLabel);
        compressTexture = EditorGUILayout.Toggle("压缩纹理", compressTexture);
        maxTextureSize = EditorGUILayout.IntPopup("最大纹理尺寸", maxTextureSize,
            new string[] { "128", "256", "512", "1024", "2048" },
            new int[] { 128, 256, 512, 1024, 2048 });
        isNativeSize = EditorGUILayout.Toggle("SetNative（试玩无效）", isNativeSize);
        isUseTextSize = EditorGUILayout.Toggle("使用Text组件尺寸", isUseTextSize);
        EditorGUILayout.EndVertical();

        // 刷新按钮
        if (GUILayout.Button("刷新列表"))
        {
            RefreshItemList();
        }

        // 全选/全不选按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选"))
        {
            foreach (var item in conversionItems)
                item.selected = true;
        }
        if (GUILayout.Button("全不选"))
        {
            foreach (var item in conversionItems)
                item.selected = false;
        }
        EditorGUILayout.EndHorizontal();

        // 对象列表
        EditorGUILayout.LabelField($"找到 {conversionItems.Count} 个文本对象", EditorStyles.miniLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var item in conversionItems)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            item.selected = EditorGUILayout.Toggle(item.selected, GUILayout.Width(20));

            EditorGUILayout.LabelField(item.gameObjectPath, EditorStyles.boldLabel);

            if (GUILayout.Button("选中", GUILayout.Width(50)))
            {
                Selection.activeGameObject = item.gameObject;
            }
            EditorGUILayout.EndHorizontal();

            // 显示语言预览
            if (item.selected)
            {
                EditorGUI.indentLevel++;
                foreach (var preview in item.languagePreviews)
                {
                    EditorGUILayout.BeginHorizontal();
                    preview.willConvert = EditorGUILayout.Toggle(preview.willConvert, GUILayout.Width(20));
                    EditorGUILayout.LabelField($"{preview.language}:", GUILayout.Width(120));
                    EditorGUILayout.LabelField(preview.text, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();

        // 进度条
        if (isConverting)
        {
            EditorGUILayout.Space();
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(20)), progress, progressMessage);
        }

        // 转换按钮
        EditorGUILayout.Space();
        GUI.enabled = !isConverting && conversionItems.Any(x => x.selected);
        if (GUILayout.Button("开始转换", GUILayout.Height(30)))
        {
            StartConversion();
        }
        GUI.enabled = true;
    }

    private void StartConversion()
    {
        if (conversionItems.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有找到可转换的文本对象", "确定");
            return;
        }

        isConverting = true;
        progress = 0f;

        try
        {
            var selectedItems = conversionItems.Where(x => x.selected).ToList();
            int totalConversions = selectedItems.Sum(x => x.languagePreviews.Count(p => p.willConvert));
            int currentConversion = 0;
            int successCount = 0;

            foreach (var item in selectedItems)
            {
                if (item.textComponent == null)
                {
                    Debug.LogWarning($"跳过对象 {item.gameObject.name}: Text组件为null");
                    continue;
                }

                progressMessage = $"处理: {item.gameObject.name}";

                var languageImageSettings = new List<MLT_ImageSwitch.LanguageImageSettings>();

                foreach (var preview in item.languagePreviews.Where(p => p.willConvert))
                {
                    if (string.IsNullOrEmpty(preview.text)) continue;

                    currentConversion++;
                    progress = (float)currentConversion / totalConversions;

                    try
                    {
                        // 生成图片
                        var sprite = GenerateTextImage(item, preview.language, preview.text);
                        if (sprite == null)
                        {
                            Debug.LogWarning($"生成精灵失败: {item.gameObject.name} - {preview.language}");
                        }
                        else
                        {
                            Vector2 sizeDelta = Vector2.zero;
                            if (isUseTextSize)
                            {
                                // 获取原始Text组件的尺寸
                                if (item.textComponent != null && item.textComponent.rectTransform != null)
                                {
                                    sizeDelta = item.textComponent.rectTransform.sizeDelta;
                                }
                            }
                            else
                            {
                                Texture2D texture = sprite.texture;
                                int textureWidth = texture.width;
                                int textureHeight = texture.height;
                                sizeDelta = new Vector2(textureWidth, textureHeight);
                            }

                            languageImageSettings.Add(new MLT_ImageSwitch.LanguageImageSettings
                            {
                                language = preview.language,
                                sprite = sprite,
                                preserveAspect = true,
                                imageType = Image.Type.Simple,
                                overrideSize = !isNativeSize,
                                sizeDelta = sizeDelta
                            });
                            successCount++;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"转换过程中出错: {item.gameObject.name} - {preview.language}\n{e.Message}\n{e.StackTrace}");
                    }
                }

                // 添加或更新MLT_ImageSwitch组件
                if (languageImageSettings.Count > 0)
                {
                    Debug.Log($"准备为 {item.gameObject?.name} 设置 {languageImageSettings.Count} 个语言图片");

                    // 验证item的有效性
                    if (item != null && item.gameObject != null)
                    {
                        SetupImageSwitch(item, languageImageSettings);
                    }
                    else
                    {
                        Debug.LogError($"无法为 {item?.gameObject?.name} 设置ImageSwitch，item或gameObject为null");
                    }
                }
                else
                {
                    Debug.LogWarning($"跳过 {item.gameObject?.name} 的ImageSwitch设置，因为没有有效的语言图片");
                }

                // 刷新编辑器
                EditorUtility.SetDirty(item.gameObject);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("转换完成", $"成功转换 {successCount}/{totalConversions} 个文本为图片", "确定");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"转换失败: {e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("错误", $"转换失败: {e.Message}", "确定");
        }
        finally
        {
            isConverting = false;
            progress = 0f;
            progressMessage = "";
        }
    }

    private Sprite GenerateTextImage(TextToImageItem item, SystemLanguage language, string text)
    {
        // 验证输入参数
        if (item == null)
        {
            Debug.LogError("TextToImageItem 为 null");
            return null;
        }

        if (item.textComponent == null)
        {
            Debug.LogError($"{item.gameObject.name} 的 textComponent 为 null");
            return null;
        }

        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning($"文本内容为空，跳过生成: {item.gameObject.name} - {language}");
            return null;
        }

        // 对RTL语言进行文本反转处理
        string processedText = ProcessRTLText(text, language);

        Debug.Log($"开始生成图片: {item.gameObject.name} - {language} - 文本: '{processedText}'");

        GameObject tempGO = null;
        GameObject tempCanvas = null;
        GameObject tempCameraGO = null;
        RenderTexture renderTexture = null;

        try
        {
            // 创建临时Canvas
            tempCanvas = new GameObject("TempCanvas");
            tempCanvas.hideFlags = HideFlags.HideAndDontSave;
            tempCanvas.layer = 5; // UI Layer
            var canvas = tempCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasScaler = tempCanvas.AddComponent<CanvasScaler>();
            var raycaster = tempCanvas.AddComponent<GraphicRaycaster>();

            // 创建临时GameObject用于渲染
            tempGO = new GameObject("TempTextRenderer");
            tempGO.hideFlags = HideFlags.HideAndDontSave;
            tempGO.layer = 5; // UI Layer
            tempGO.transform.SetParent(tempCanvas.transform, false);

            // 复制原始Text组件的设置
            var tempText = tempGO.AddComponent<Text>();
            CopyTextSettings(item.textComponent, tempText);
            tempText.text = processedText;
            tempText.alignment = textAnchor;

            // 设置RectTransform
            var rect = tempGO.GetComponent<RectTransform>();
            rect.sizeDelta = item.textComponent.rectTransform.sizeDelta * imageScale;
            rect.anchoredPosition = Vector2.zero;

            // 强制更新Canvas和文本以获取准确的preferredSize
            Canvas.ForceUpdateCanvases();
            tempText.SetAllDirty();

            // 使用更精确的方法计算文本实际尺寸
            Vector2 preferredSize = new Vector2(
                tempText.preferredWidth,
                tempText.preferredHeight
            );

            // 如果preferredSize仍然无效，使用TextGenerator计算
            if (preferredSize.x <= 0 || preferredSize.y <= 0)
            {
                var textGen = tempText.cachedTextGenerator;
                textGen.Populate(tempText.text, tempText.GetGenerationSettings(rect.rect.size));
                var bounds = GetTextBounds(textGen);
                preferredSize = new Vector2(bounds.width, bounds.height);
            }

            // 如果preferredSize还是无效，使用RectTransform的尺寸
            if (preferredSize.x <= 0 || preferredSize.y <= 0)
            {
                preferredSize = rect.sizeDelta;
            }

            // 计算最终纹理尺寸（加上padding）
            int width = Mathf.CeilToInt(preferredSize.x) + padding * 2;
            int height = Mathf.CeilToInt(preferredSize.y) + padding * 2;

            Debug.Log($"Text size: {preferredSize}, Texture size: {width}x{height}");

            // 限制最大尺寸，确保文本不会被裁剪
            if (width > maxTextureSize || height > maxTextureSize)
            {
                // 计算保持宽高比的缩放因子
                float scale = Mathf.Min((float)maxTextureSize / width, (float)maxTextureSize / height);

                // 按比例缩放尺寸
                width = Mathf.CeilToInt(width * scale);
                height = Mathf.CeilToInt(height * scale);

                // 同时按比例缩放文本字体大小
                if (tempText != null)
                {
                    tempText.fontSize = Mathf.Max(1, Mathf.RoundToInt(tempText.fontSize * scale));
                }
            }

            // 创建RenderTexture
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            renderTexture.antiAliasing = antiAliasing ? 4 : 1;

            // 创建相机用于渲染
            tempCameraGO = new GameObject("TempCamera");
            tempCameraGO.hideFlags = HideFlags.HideAndDontSave;
            var camera = tempCameraGO.AddComponent<Camera>();
            camera.targetTexture = renderTexture;
            camera.orthographic = true;
            camera.orthographicSize = height / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.cullingMask = 1 << 5; // UI Layer

            // 设置Canvas为世界空间以确保正确渲染
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(width, height);
            canvasRect.anchoredPosition = Vector2.zero;

            // 设置相机参数以正确覆盖Canvas
            tempCameraGO.transform.position = new Vector3(0, 0, -10);
            tempCameraGO.transform.LookAt(Vector3.zero);

            // 调整正交相机大小以紧密覆盖Canvas
            camera.orthographicSize = height / 2f;
            camera.aspect = (float)width / height;

            // 调整文本位置到Canvas左下角，然后居中显示
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            // 根据文本对齐方式调整文本位置
            switch (tempText.alignment)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.MiddleLeft:
                case TextAnchor.LowerLeft:
                    rect.pivot = new Vector2(0, 0.5f);
                    rect.anchorMin = new Vector2(0, 0.5f);
                    rect.anchorMax = new Vector2(0, 0.5f);
                    rect.anchoredPosition = new Vector2(padding, 0);
                    break;
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                case TextAnchor.LowerCenter:
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    break;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    rect.pivot = new Vector2(1, 0.5f);
                    rect.anchorMin = new Vector2(1, 0.5f);
                    rect.anchorMax = new Vector2(1, 0.5f);
                    rect.anchoredPosition = new Vector2(-padding, 0);
                    break;
            }

            // 确保文本有内容
            Debug.Log($"Rendering text: '{tempText.text}', Color: {tempText.color}");

            // 强制更新Canvas和文本
            Canvas.ForceUpdateCanvases();
            tempText.SetAllDirty();

            // 手动触发文本生成
            tempText.cachedTextGenerator.Populate(tempText.text, tempText.GetGenerationSettings(rect.rect.size));

            // 再次更新以确保所有更改生效
            Canvas.ForceUpdateCanvases();

            // 添加调试信息
            Debug.Log($"Canvas size: {canvasRect.sizeDelta}, Camera ortho size: {camera.orthographicSize}");
            Debug.Log($"Text rect size: {tempText.rectTransform.sizeDelta}, position: {tempText.rectTransform.position}");
            Debug.Log($"RenderTexture size: {renderTexture.width}x{renderTexture.height}");

            // 渲染
            camera.Render();

            // 检查渲染结果，如果大部分是透明的，尝试优化
            Texture2D texture = null;

            // 从RenderTexture创建Texture2D
            texture = new Texture2D(width, height, textureFormat, false);
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            // 检查图片内容，如果透明区域过多则尝试优化
            if (IsTextureMostlyTransparent(texture))
            {
                Debug.Log("检测到图片透明区域过多，尝试优化布局...");
                // 销毁之前创建的texture
                DestroyImmediate(texture);

                // 重新计算更紧密的边界
                var textGen = tempText.cachedTextGenerator;
                var bounds = GetTextBounds(textGen);

                // 使用更紧密的尺寸
                int tightWidth = Mathf.CeilToInt(bounds.width) + padding * 2;
                int tightHeight = Mathf.CeilToInt(bounds.height) + padding * 2;

                // 限制最大尺寸，确保文本不会被裁剪
                float scale = 1f;
                if (tightWidth > maxTextureSize || tightHeight > maxTextureSize)
                {
                    // 计算保持宽高比的缩放因子
                    scale = Mathf.Min((float)maxTextureSize / tightWidth, (float)maxTextureSize / tightHeight);
                    tightWidth = Mathf.CeilToInt(tightWidth * scale);
                    tightHeight = Mathf.CeilToInt(tightHeight * scale);

                    // 同时按比例缩放文本字体大小
                    if (tempText != null)
                    {
                        tempText.fontSize = Mathf.Max(1, Mathf.RoundToInt(tempText.fontSize * scale));
                    }
                }

                // 重新创建RenderTexture
                if (renderTexture != null) DestroyImmediate(renderTexture);
                renderTexture = new RenderTexture(tightWidth, tightHeight, 24, RenderTextureFormat.ARGB32);
                renderTexture.antiAliasing = antiAliasing ? 4 : 1;
                camera.targetTexture = renderTexture;

                // 调整相机设置
                camera.orthographicSize = tightHeight / 2f;
                camera.aspect = (float)tightWidth / tightHeight;

                // 调整Canvas尺寸
                canvasRect.sizeDelta = new Vector2(tightWidth, tightHeight);

                // 重新渲染
                camera.Render();

                // 重新创建texture
                texture = new Texture2D(tightWidth, tightHeight, textureFormat, false);
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, tightWidth, tightHeight), 0, 0);
                texture.Apply();
                RenderTexture.active = null;

                // 更新尺寸变量
                width = tightWidth;
                height = tightHeight;

                Debug.Log($"优化后尺寸: {width}x{height}");
            }
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            // 使用Text组件的GameObject完整路径作为基础创建唯一文件夹名
            string sanitizedPath = SanitizeFileName(item.gameObjectPath);
            // 使用路径的哈希值作为文件夹名，确保唯一性同时避免文件系统不支持的字符
            string textFolderName = Mathf.Abs(sanitizedPath.GetHashCode()).ToString();
            string textFolder = Path.Combine(outputPath, textFolderName);
            Directory.CreateDirectory(textFolder);

            // 使用语言作为文件名
            string fileName = SanitizeFileName($"{language}.png");
            string filePath = Path.Combine(textFolder, fileName);

            // 确保目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            // 保存PNG
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);

            AssetDatabase.Refresh();

            // 设置纹理导入设置
            var importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;

                if (compressTexture)
                {
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.compressionQuality = 50;
                }
                else
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                }

                importer.maxTextureSize = maxTextureSize;

                importer.SaveAndReimport();
            }

            // 刷新资源数据库以确保纹理被识别
            AssetDatabase.Refresh();

            // 加载并返回Sprite
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
            if (sprite == null)
            {
                Debug.LogError($"Failed to load sprite from {filePath}");
            }
            else
            {
                Debug.Log($"成功生成精灵: {fileName} ({width}x{height})");
            }

            // 清理纹理
            DestroyImmediate(texture);

            return sprite;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"生成文本图片失败: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
        finally
        {
            // 清理临时对象
            if (tempGO != null) DestroyImmediate(tempGO);
            if (tempCanvas != null) DestroyImmediate(tempCanvas);
            if (tempCameraGO != null) DestroyImmediate(tempCameraGO);
            if (renderTexture != null) DestroyImmediate(renderTexture);
        }
    }

    private void CopyTextSettings(Text source, Text target)
    {
        // 检查并设置字体，如果源字体为null，使用默认字体
        if (source.font != null)
        {
            target.font = source.font;
        }
        else
        {
            // 尝试使用Arial作为默认字体
            target.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (target.font == null)
            {
                // 如果Arial也不可用，尝试项目中的字体
                target.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansSC-VariableFont_wght.ttf");
            }
            Debug.LogWarning($"Text组件 {source.gameObject.name} 没有设置字体，使用默认字体");
        }

        target.fontSize = Mathf.RoundToInt(source.fontSize * imageScale);
        target.fontStyle = source.fontStyle;
        target.lineSpacing = source.lineSpacing;
        target.supportRichText = source.supportRichText;
        target.color = source.color;

        // 材质可能也为null
        if (source.material != null)
        {
            target.material = source.material;
        }

        target.horizontalOverflow = source.horizontalOverflow;
        target.verticalOverflow = source.verticalOverflow;

        // 复制BestFit设置
        target.resizeTextForBestFit = source.resizeTextForBestFit;
        if (source.resizeTextForBestFit)
        {
            target.resizeTextMinSize = source.resizeTextMinSize;
            target.resizeTextMaxSize = source.resizeTextMaxSize;
        }

        // Copy all Outlines if exist
        var sourceOutlines = source.GetComponents<Outline>();
        foreach (var srcOutline in sourceOutlines)
        {
            var targetOutline = target.gameObject.AddComponent<Outline>();
            targetOutline.effectColor = srcOutline.effectColor;
            targetOutline.effectDistance = srcOutline.effectDistance;
            targetOutline.useGraphicAlpha = srcOutline.useGraphicAlpha;
        }

        // Copy all Shadows if exist
        var sourceShadows = source.GetComponents<Shadow>();
        foreach (var srcShadow in sourceShadows)
        {
            var targetShadow = target.gameObject.AddComponent<Shadow>();
            targetShadow.effectColor = srcShadow.effectColor;
            targetShadow.effectDistance = srcShadow.effectDistance;
            targetShadow.useGraphicAlpha = srcShadow.useGraphicAlpha;
        }
    }

    private Rect GetTextBounds(TextGenerator textGen)
    {
        if (textGen == null || textGen.characterCount == 0 || textGen.verts.Count == 0)
        {
            Debug.LogWarning("TextGenerator is empty or null, using default bounds");
            return new Rect(0, 0, 200, 100); // 默认大小
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        var verts = textGen.verts;
        int vertCount = Mathf.Min(verts.Count, textGen.characterCountVisible * 4);

        for (int i = 0; i < vertCount; i++)
        {
            var vert = verts[i];
            minX = Mathf.Min(minX, vert.position.x);
            maxX = Mathf.Max(maxX, vert.position.x);
            minY = Mathf.Min(minY, vert.position.y);
            maxY = Mathf.Max(maxY, vert.position.y);
        }

        // 确保有有效的边界
        if (minX == float.MaxValue || maxX == float.MinValue)
        {
            Debug.LogWarning("Invalid text bounds calculated, using default");
            return new Rect(0, 0, 200, 100);
        }

        float width = maxX - minX;
        float height = maxY - minY;

        // 添加一些padding以确保文本不会被裁剪
        return new Rect(minX - 2, minY - 2, width + 4, height + 4);
    }

    private string SanitizeFileName(string fileName)
    {
        // 移除非法字符
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }

    private bool IsTextureMostlyTransparent(Texture2D texture)
    {
        if (texture == null) return false;

        // 采样纹理的几个关键点来检查透明度
        int width = texture.width;
        int height = texture.height;

        // 检查角点
        Color[] corners = {
            texture.GetPixel(0, 0),
            texture.GetPixel(width - 1, 0),
            texture.GetPixel(0, height - 1),
            texture.GetPixel(width - 1, height - 1)
        };

        // 检查中心点
        Color center = texture.GetPixel(width / 2, height / 2);

        // 计算平均透明度
        float totalAlpha = 0;
        foreach (var color in corners)
        {
            totalAlpha += color.a;
        }
        totalAlpha += center.a;

        float averageAlpha = totalAlpha / 5;

        // 如果平均透明度低于阈值，认为图片大部分是透明的
        return averageAlpha < 0.1f;
    }

    /// <summary>
    /// 处理RTL（从右到左）语言的文本，对需要反转的语言进行文本反转
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="language">语言</param>
    /// <returns>处理后的文本</returns>
    private string ProcessRTLText(string text, SystemLanguage language)
    {
        // 定义需要RTL处理的语言
        HashSet<SystemLanguage> rtlLanguages = new HashSet<SystemLanguage>
        {
            SystemLanguage.Arabic,
            SystemLanguage.Hebrew
            // 可以根据需要添加更多RTL语言
        };

        // 如果是RTL语言，则反转文本
        if (rtlLanguages.Contains(language))
        {
            return ReverseTextForRTL(text);
        }

        // 对于非RTL语言，返回原始文本
        return text;
    }

    /// <summary>
    /// 反转文本以适应RTL语言显示
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <returns>反转后的文本</returns>
    private string ReverseTextForRTL(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // 匹配完整的富文本标签对，包括嵌套
        var tagPattern = @"<(b|i|size|color)(?:=([^>]+))?>(.*?)</\1>";
        var result = new System.Text.StringBuilder();

        // 递归处理嵌套标签
        var processedText = ProcessNestedTags(text, tagPattern);
        return processedText;
    }

    private string ProcessNestedTags(string text, string pattern)
    {
        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
        var lastEnd = 0;
        var segments = new System.Collections.Generic.List<string>();

        var matches = regex.Matches(text);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            // 添加标签前的纯文本（反转）
            if (match.Index > lastEnd)
            {
                var textBefore = text.Substring(lastEnd, match.Index - lastEnd);
                segments.Add(SmartReverseText(textBefore));
            }

            // 处理标签内容
            var tagName = match.Groups[1].Value;
            var tagAttr = match.Groups[2].Value;
            var innerContent = match.Groups[3].Value;

            // 递归处理内部内容
            var processedInner = ProcessNestedTags(innerContent, pattern);

            // 重建标签（保持标签结构，内容已处理）
            var openTag = string.IsNullOrEmpty(tagAttr) ? $"<{tagName}>" : $"<{tagName}={tagAttr}>";
            var closeTag = $"</{tagName}>";
            segments.Add(openTag + processedInner + closeTag);

            lastEnd = match.Index + match.Length;
        }

        // 添加最后的纯文本（反转）
        if (lastEnd < text.Length)
        {
            var textAfter = text.Substring(lastEnd);
            segments.Add(SmartReverseText(textAfter));
        }

        // 如果没有匹配到标签，直接反转整个文本
        if (matches.Count == 0)
        {
            return SmartReverseText(text);
        }

        // 反转段落顺序
        segments.Reverse();
        return string.Join("", segments);
    }

    /// <summary>
    /// 智能反转文本，保留数字和拉丁字符的顺序
    /// </summary>
    private string SmartReverseText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var segments = new System.Collections.Generic.List<string>();
        var currentSegment = new System.Text.StringBuilder();
        bool inLatinOrDigit = false;

        foreach (char c in text)
        {
            bool isLatinOrDigit = char.IsDigit(c) || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                                   c == '.' || c == ',' || c == ' ' || c == '-' || c == '+' || c == '%';

            if (isLatinOrDigit)
            {
                if (!inLatinOrDigit && currentSegment.Length > 0)
                {
                    // 反转并添加之前的RTL段
                    segments.Add(ReverseString(currentSegment.ToString()));
                    currentSegment.Clear();
                }
                currentSegment.Append(c);
                inLatinOrDigit = true;
            }
            else
            {
                if (inLatinOrDigit && currentSegment.Length > 0)
                {
                    // 保持LTR段不变
                    segments.Add(currentSegment.ToString());
                    currentSegment.Clear();
                }
                currentSegment.Append(c);
                inLatinOrDigit = false;
            }
        }

        // 添加最后一段
        if (currentSegment.Length > 0)
        {
            if (inLatinOrDigit)
                segments.Add(currentSegment.ToString());
            else
                segments.Add(ReverseString(currentSegment.ToString()));
        }

        // 反转段的顺序
        segments.Reverse();
        return string.Join("", segments);
    }

    /// <summary>
    /// 简单字符串反转
    /// </summary>
    private string ReverseString(string str)
    {
        char[] charArray = str.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }

    private void SetupImageSwitch(TextToImageItem item, List<MLT_ImageSwitch.LanguageImageSettings> settings)
    {
        try
        {
            // 检查输入参数
            if (item == null)
            {
                Debug.LogError("TextToImageItem 为 null");
                return;
            }

            if (item.gameObject == null)
            {
                Debug.LogError("TextToImageItem 的 GameObject 为 null");
                return;
            }

            if (settings == null)
            {
                Debug.LogError($"传递给 {item.gameObject.name} 的设置为 null");
                return;
            }

            Debug.Log($"开始为 {item.gameObject.name} 设置ImageSwitch，设置数量: {settings.Count}");

            // 检查是否已有MLT_ImageSwitch组件
            if (item.imageSwitch == null)
            {
                // 创建一个新的GameObject来放置MLT_ImageSwitch组件，而不是替换Text组件
                GameObject imageSwitchGO = new GameObject($"{item.gameObject.name}_ImageSwitch");
                if (item.gameObject.transform.parent != null)
                {
                    imageSwitchGO.transform.SetParent(item.gameObject.transform.parent, false);
                }
                imageSwitchGO.transform.localPosition = item.gameObject.transform.localPosition;
                imageSwitchGO.transform.localRotation = item.gameObject.transform.localRotation;
                imageSwitchGO.transform.localScale = item.gameObject.transform.localScale;

                // 添加Image组件
                var image = imageSwitchGO.AddComponent<UnityEngine.UI.Image>();
                if (image == null)
                {
                    Debug.LogError($"无法为 {item.gameObject.name} 添加Image组件");
                    GameObject.DestroyImmediate(imageSwitchGO);
                    return;
                }

                // 设置Image组件的尺寸与原始Text组件一致
                if (item.textComponent != null)
                {
                    var textRectTransform = item.textComponent.GetComponent<RectTransform>();
                    var imageRectTransform = image.GetComponent<RectTransform>();
                    if (textRectTransform != null && imageRectTransform != null)
                    {
                        imageRectTransform.sizeDelta = textRectTransform.sizeDelta;
                        imageRectTransform.anchorMin = textRectTransform.anchorMin;
                        imageRectTransform.anchorMax = textRectTransform.anchorMax;
                        imageRectTransform.pivot = textRectTransform.pivot;
                        imageRectTransform.anchoredPosition = textRectTransform.anchoredPosition;
                    }
                }

                // 添加MLT_ImageSwitch组件
                item.imageSwitch = imageSwitchGO.AddComponent<MLT_ImageSwitch>();
                if (item.imageSwitch == null)
                {
                    Debug.LogError($"无法为 {item.gameObject.name} 添加MLT_ImageSwitch组件");
                    GameObject.DestroyImmediate(imageSwitchGO);
                    return;
                }

                Debug.Log($"为 {item.gameObject.name} 创建了新的ImageSwitch GameObject");
            }

            // 检查item.imageSwitch是否为null
            if (item.imageSwitch == null)
            {
                Debug.LogError($"无法获取或创建 {item.gameObject.name} 的 MLT_ImageSwitch 组件");
                return;
            }

            // 清空现有设置并添加新的
            item.imageSwitch.languageImages.Clear();
            item.imageSwitch.languageImages.AddRange(settings);

            // 禁用Text组件和MLT_TextSwitch组件
            if (item.textComponent != null)
                item.textComponent.enabled = false;
            if (item.textSwitch != null)
                item.textSwitch.enabled = false;

            // 创建提示说明
            Debug.Log($"已为 {item.gameObject.name} 设置 {settings.Count} 个语言的图片");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SetupImageSwitch 失败: {item?.gameObject?.name}\n{e.Message}\n{e.StackTrace}");
        }
    }
}
