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
    private Color backgroundColor = new Color(1, 1, 1, 0); // 透明背景
    private int padding = 10; // 文本周围的内边距
    private TextAnchor textAnchor = TextAnchor.MiddleCenter;
    private bool antiAliasing = true;
    private TextureFormat textureFormat = TextureFormat.RGBA32;
    private bool compressTexture = true;
    private int maxTextureSize = 2048; // 最大纹理尺寸
    private bool isNativeSize = false;
    private bool isUseTextSize = false;

    [System.Serializable]
    public class TextToImageItem
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
            imageSwitch = MLT_TextToImageConverter.FindImageSwitchForTextObject(obj);
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

            // 使用源Text的实际类型，兼容NewText或其它Text子类，不硬依赖具体组件。
            var tempText = AddCompatibleTextComponent(tempGO, item.textComponent);
            CopyCompatibleTextSettings(item.textComponent, tempText);
            tempText.text = processedText;
            tempText.alignment = textAnchor;

            // 设置RectTransform
            var rect = tempGO.GetComponent<RectTransform>();
            rect.sizeDelta = GetScaledTextRectSize(item.textComponent);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            // 强制更新Canvas和文本，先拿到一个足够安全的渲染区域。
            Canvas.ForceUpdateCanvases();
            tempText.SetAllDirty();

            Vector2 preferredSize = new Vector2(
                tempText.preferredWidth,
                tempText.preferredHeight
            );
            Vector2 renderSize = GetSafeRenderSize(rect.sizeDelta, preferredSize);
            int renderWidth = Mathf.CeilToInt(renderSize.x);
            int renderHeight = Mathf.CeilToInt(renderSize.y);

            Debug.Log($"Text size: {preferredSize}, RenderTexture size: {renderWidth}x{renderHeight}, Component: {tempText.GetType().Name}");

            // 创建RenderTexture
            renderTexture = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32);
            renderTexture.antiAliasing = antiAliasing ? 4 : 1;

            // 创建相机用于渲染
            tempCameraGO = new GameObject("TempCamera");
            tempCameraGO.hideFlags = HideFlags.HideAndDontSave;
            var camera = tempCameraGO.AddComponent<Camera>();
            camera.targetTexture = renderTexture;
            camera.orthographic = true;
            camera.orthographicSize = renderHeight / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.cullingMask = 1 << 5; // UI Layer

            // 设置Canvas为世界空间以确保正确渲染
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(renderWidth, renderHeight);
            canvasRect.anchoredPosition = Vector2.zero;

            // 设置相机参数以正确覆盖Canvas
            tempCameraGO.transform.position = new Vector3(0, 0, -10);
            tempCameraGO.transform.LookAt(Vector3.zero);

            // 调整正交相机大小以紧密覆盖Canvas
            camera.orthographicSize = renderHeight / 2f;
            camera.aspect = (float)renderWidth / renderHeight;
            rect.anchoredPosition = Vector2.zero;

            // 确保文本有内容
            Debug.Log($"Rendering text: '{tempText.text}', Color: {tempText.color}");

            // 强制更新Canvas和文本
            Canvas.ForceUpdateCanvases();
            tempText.SetAllDirty();

            // 再次更新以确保所有更改生效
            Canvas.ForceUpdateCanvases();

            // 添加调试信息
            Debug.Log($"Canvas size: {canvasRect.sizeDelta}, Camera ortho size: {camera.orthographicSize}");
            Debug.Log($"Text rect size: {tempText.rectTransform.sizeDelta}, position: {tempText.rectTransform.position}");
            Debug.Log($"RenderTexture size: {renderTexture.width}x{renderTexture.height}");

            // 渲染
            camera.Render();

            // 从RenderTexture创建Texture2D
            Texture2D texture = new Texture2D(renderWidth, renderHeight, textureFormat, false);
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            Texture2D outputTexture = CropToVisiblePixels(texture);
            if (outputTexture != texture)
                DestroyImmediate(texture);

            texture = LimitTextureSize(outputTexture);
            if (texture != outputTexture)
                DestroyImmediate(outputTexture);

            int width = texture.width;
            int height = texture.height;

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
            if (tempCameraGO != null)
            {
                var tempCamera = tempCameraGO.GetComponent<Camera>();
                if (tempCamera != null)
                    tempCamera.targetTexture = null;
            }

            if (tempGO != null) DestroyImmediate(tempGO);
            if (tempCanvas != null) DestroyImmediate(tempCanvas);
            if (tempCameraGO != null) DestroyImmediate(tempCameraGO);
            if (renderTexture != null) DestroyImmediate(renderTexture);
        }
    }

    private Text AddCompatibleTextComponent(GameObject targetObject, Text source)
    {
        Type sourceType = source.GetType();
        if (sourceType != typeof(Text) && typeof(Text).IsAssignableFrom(sourceType) && !sourceType.IsAbstract)
        {
            try
            {
                Text compatibleText = targetObject.AddComponent(sourceType) as Text;
                if (compatibleText != null)
                    return compatibleText;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"创建临时{sourceType.Name}失败，回退到Text: {ex.Message}");
            }
        }

        return targetObject.AddComponent<Text>();
    }

    private Vector2 GetScaledTextRectSize(Text source)
    {
        RectTransform sourceRect = source.rectTransform;
        Vector2 size = sourceRect.rect.size;
        if (size.x <= 0 || size.y <= 0)
            size = sourceRect.sizeDelta;

        size.x = Mathf.Max(1, Mathf.Abs(size.x) * imageScale);
        size.y = Mathf.Max(1, Mathf.Abs(size.y) * imageScale);
        return size;
    }

    private Vector2 GetSafeRenderSize(Vector2 rectSize, Vector2 preferredSize)
    {
        float width = Mathf.Max(rectSize.x, preferredSize.x, 1);
        float height = Mathf.Max(rectSize.y, preferredSize.y, 1);

        // 给Overflow、Shadow、Outline和子类自定义网格留出余量，最终再按真实像素裁剪。
        int safePadding = Mathf.Max(padding * 4, 32);
        width += safePadding * 2;
        height += safePadding * 2;

        return new Vector2(Mathf.Ceil(width), Mathf.Ceil(height));
    }

    private Texture2D CropToVisiblePixels(Texture2D source)
    {
        if (source == null || backgroundColor.a > 0.001f)
            return source;

        if (!TryGetVisiblePixelBounds(source, out RectInt bounds))
            return source;

        int xMin = Mathf.Max(0, bounds.xMin - padding);
        int yMin = Mathf.Max(0, bounds.yMin - padding);
        int xMax = Mathf.Min(source.width, bounds.xMax + padding);
        int yMax = Mathf.Min(source.height, bounds.yMax + padding);
        int width = Mathf.Max(1, xMax - xMin);
        int height = Mathf.Max(1, yMax - yMin);

        if (width == source.width && height == source.height)
            return source;

        Texture2D cropped = new Texture2D(width, height, textureFormat, false);
        cropped.SetPixels(source.GetPixels(xMin, yMin, width, height));
        cropped.Apply();
        return cropped;
    }

    private bool TryGetVisiblePixelBounds(Texture2D texture, out RectInt bounds)
    {
        const byte alphaThreshold = 2;
        Color32[] pixels = texture.GetPixels32();

        int minX = texture.width;
        int minY = texture.height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < texture.height; y++)
        {
            int rowOffset = y * texture.width;
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[rowOffset + x].a <= alphaThreshold)
                    continue;

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x + 1);
                maxY = Mathf.Max(maxY, y + 1);
            }
        }

        if (maxX < 0 || maxY < 0)
        {
            bounds = new RectInt(0, 0, texture.width, texture.height);
            return false;
        }

        bounds = new RectInt(minX, minY, maxX - minX, maxY - minY);
        return true;
    }

    private Texture2D LimitTextureSize(Texture2D source)
    {
        int sourceMax = Mathf.Max(source.width, source.height);
        if (sourceMax <= maxTextureSize)
            return source;

        float scale = (float)maxTextureSize / sourceMax;
        int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture scaledRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        try
        {
            Graphics.Blit(source, scaledRT);
            Texture2D scaled = new Texture2D(width, height, textureFormat, false);
            RenderTexture.active = scaledRT;
            scaled.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            scaled.Apply();
            return scaled;
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(scaledRT);
        }
    }

    private void CopyCompatibleTextSettings(Text source, Text target)
    {
        if (source.GetType() == target.GetType())
        {
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), target);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"复制{source.GetType().Name}序列化字段失败，仅复制Text基础设置: {ex.Message}");
            }
        }

        CopyTextSettings(source, target);
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

    private string SanitizeFileName(string fileName)
    {
        // 移除非法字符
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
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
                item.imageSwitch = FindImageSwitchForTextObject(item.gameObject);

            if (item.imageSwitch == null)
            {
                // 创建到目标Text子级，方便后续一起调整文本与生成图片的位置。
                GameObject imageSwitchGO = CreateImageSwitchObject(item);

                // 添加Image组件
                var image = imageSwitchGO.GetComponent<UnityEngine.UI.Image>();
                if (image == null)
                {
                    Debug.LogError($"无法为 {item.gameObject.name} 添加Image组件");
                    GameObject.DestroyImmediate(imageSwitchGO);
                    return;
                }

                // 添加MLT_ImageSwitch组件
                item.imageSwitch = imageSwitchGO.AddComponent<MLT_ImageSwitch>();
                if (item.imageSwitch == null)
                {
                    Debug.LogError($"无法为 {item.gameObject.name} 添加MLT_ImageSwitch组件");
                    GameObject.DestroyImmediate(imageSwitchGO);
                    return;
                }

                Debug.Log($"为 {item.gameObject.name} 创建了新的ImageSwitch子对象");
            }
            else
            {
                EnsureImageSwitchUnderText(item);
                if (item.imageSwitch.GetComponent<UnityEngine.UI.Image>() == null)
                    item.imageSwitch.gameObject.AddComponent<UnityEngine.UI.Image>();
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

    private static MLT_ImageSwitch FindImageSwitchForTextObject(GameObject textObject)
    {
        if (textObject == null)
            return null;

        string generatedName = $"{textObject.name}_ImageSwitch";

        MLT_ImageSwitch imageSwitch = FindDirectChildImageSwitch(textObject.transform, generatedName);
        if (imageSwitch != null)
            return imageSwitch;

        Transform parent = textObject.transform.parent;
        if (parent != null)
        {
            imageSwitch = FindNamedImageSwitch(parent, generatedName, textObject.transform);
            if (imageSwitch != null)
                return imageSwitch;
        }

        return null;
    }

    private static MLT_ImageSwitch FindDirectChildImageSwitch(Transform textTransform, string generatedName)
    {
        Transform namedChild = textTransform.Find(generatedName);
        if (namedChild != null && namedChild.TryGetComponent(out MLT_ImageSwitch namedSwitch))
            return namedSwitch;

        foreach (Transform child in textTransform)
        {
            if (child.TryGetComponent(out MLT_ImageSwitch childSwitch))
                return childSwitch;
        }

        return null;
    }

    private static MLT_ImageSwitch FindNamedImageSwitch(Transform parent, string generatedName, Transform excludedTransform)
    {
        foreach (Transform child in parent)
        {
            if (child == excludedTransform || !child.name.StartsWith(generatedName, StringComparison.Ordinal))
                continue;

            if (child.TryGetComponent(out MLT_ImageSwitch imageSwitch))
                return imageSwitch;
        }

        return null;
    }

    private static GameObject CreateImageSwitchObject(TextToImageItem item)
    {
        GameObject imageSwitchGO = new GameObject(
            $"{item.gameObject.name}_ImageSwitch",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image));

        imageSwitchGO.transform.SetParent(item.gameObject.transform, false);
        GameObjectUtility.EnsureUniqueNameForSibling(imageSwitchGO);
        ResetImageSwitchRect(item.textComponent, imageSwitchGO.GetComponent<RectTransform>());
        return imageSwitchGO;
    }

    private static void EnsureImageSwitchUnderText(TextToImageItem item)
    {
        if (item.imageSwitch == null || item.gameObject == null)
            return;

        Transform imageSwitchTransform = item.imageSwitch.transform;
        if (imageSwitchTransform != item.gameObject.transform && imageSwitchTransform.parent != item.gameObject.transform)
            imageSwitchTransform.SetParent(item.gameObject.transform, false);

        ResetImageSwitchRect(item.textComponent, item.imageSwitch.GetComponent<RectTransform>());
    }

    private static void ResetImageSwitchRect(Text textComponent, RectTransform imageRectTransform)
    {
        if (imageRectTransform == null)
            return;

        imageRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        imageRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        imageRectTransform.pivot = new Vector2(0.5f, 0.5f);
        imageRectTransform.anchoredPosition = Vector2.zero;
        imageRectTransform.localRotation = Quaternion.identity;
        imageRectTransform.localScale = Vector3.one;
        imageRectTransform.sizeDelta = GetTextRectSize(textComponent);
    }

    private static Vector2 GetTextRectSize(Text textComponent)
    {
        if (textComponent == null || textComponent.rectTransform == null)
            return Vector2.zero;

        Vector2 size = textComponent.rectTransform.rect.size;
        if (size.x <= 0 || size.y <= 0)
            size = textComponent.rectTransform.sizeDelta;

        return size;
    }
}
