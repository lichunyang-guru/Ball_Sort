using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public sealed class FontSubsetExporterWindow : EditorWindow
{
    private const float MaxWindowWidth = 1280f;
    private const string PythonPathKey = "FontSubsetExporter.PythonPath";
    private const string FontsKey = "FontSubsetExporter.FontSources";
    private const string OutputDirectoryKey = "FontSubsetExporter.OutputDirectory";
    private const string OutputPrefixKey = "FontSubsetExporter.OutputPrefix";
    private const string TextKey = "FontSubsetExporter.Text";
    private const string IncludeRecommendedGlyphsKey = "FontSubsetExporter.IncludeRecommendedGlyphs";
    private const string KeepLayoutFeaturesKey = "FontSubsetExporter.KeepLayoutFeatures";
    private const string FonttoolsImportCheckScript =
        "import sys\n"
        + "print(sys.executable)\n"
        + "try:\n"
        + "    import fontTools\n"
        + "    print(fontTools.__version__)\n"
        + "except Exception as exc:\n"
        + "    print(f'{type(exc).__name__}: {exc}', file=sys.stderr)\n"
        + "    sys.exit(1)\n";

    private readonly List<FontSource> fontSources = new List<FontSource>();
    private string inputText;
    private string outputDirectory;
    private string outputPrefix;
    private string pythonPath;
    private bool includeRecommendedGlyphs;
    private bool keepLayoutFeatures;
    private Vector2 scrollPosition;
    private UnityEngine.Object fontAssetToAdd;
    private List<MLT_TextToImageConverter.TextToImageItem> conversionItems = new List<MLT_TextToImageConverter.TextToImageItem>();
    private Vector2 scrollPos;

    [MenuItem("Tools/MLT Translation/Text to TTF Converter")]
    public static void ShowWindow()
    {
        GetWindow<FontSubsetExporterWindow>("Font Subset Exporter");
    }

    private void OnEnable()
    {
        maxSize = new Vector2(MaxWindowWidth, maxSize.y);
        inputText = EditorPrefs.GetString(TextKey, "Hello, Unity!");
        outputDirectory = EditorPrefs.GetString(OutputDirectoryKey, Path.Combine(Application.dataPath, "Resources"));
        outputPrefix = EditorPrefs.GetString(OutputPrefixKey, "GameFont");
        pythonPath = EditorPrefs.GetString(PythonPathKey, string.Empty);
        includeRecommendedGlyphs = EditorPrefs.GetBool(IncludeRecommendedGlyphsKey, true);
        keepLayoutFeatures = EditorPrefs.GetBool(KeepLayoutFeaturesKey, true);
        LoadFontSources();
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
            var item = new MLT_TextToImageConverter.TextToImageItem(textSwitch.gameObject);
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
        FreshItemInputText();
    }

    private void FreshItemInputText()
    {
        var allTextSwitches = Resources.FindObjectsOfTypeAll<MLT_TextSwitch>()
            .Where(t => !EditorUtility.IsPersistent(t) && t.gameObject.scene.name != null)
            .ToArray();
        inputText = string.Empty;
        StringBuilder stringBuilder = new StringBuilder();
        foreach (var item in conversionItems)
        {
            if(!item.selected)
                continue;
            foreach(global::MLT_TextSwitch.LanguageText text in item.textSwitch.languageTexts)
            {
                stringBuilder.Append(text.text);
            }
        }
        stringBuilder.Append("1234567890,abcdefghijklmnokqrstuvwxz，！!@#%*()（）{}<>=,ABCDEFGHIJKLMNOPQRSTUVWXYZ/?？。.");
        inputText = stringBuilder.ToString();
    }

    private void OnDisable()
    {
        SavePrefs();
    }

    private void OnGUI()
    {
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
            FreshItemInputText();
        }
        if (GUILayout.Button("全不选"))
        {
            foreach (var item in conversionItems)
                item.selected = false;
            FreshItemInputText();
        }
        EditorGUILayout.EndHorizontal();
        FreshLanguage();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Single TTF Font Exporter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Builds one TTF from a fallback font list. Characters are assigned to the first enabled font that contains the glyph, then merged into a single output font.",
            MessageType.Info);

        DrawTextSection();
        EditorGUILayout.Space(10);
        DrawFontSourcesSection();
        EditorGUILayout.Space(10);
        DrawOutputSection();
        EditorGUILayout.Space(10);
        DrawFonttoolsSection();
        EditorGUILayout.Space(10);
        DrawActions();
        EditorGUILayout.EndScrollView();
    }

    private void FreshLanguage()
    {
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
    }

    private void DrawTextSection()
    {
        EditorGUILayout.LabelField("Text", EditorStyles.boldLabel);
        inputText = EditorGUILayout.TextArea(inputText, GUILayout.MaxWidth(1000),GUILayout.MaxHeight(300));

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Characters: {inputText?.Length ?? 0}", GUILayout.Width(120));
            EditorGUILayout.LabelField($"Unique code points: {CountUniqueCodePoints(inputText)}", GUILayout.Width(170));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Load UTF-8 Text", GUILayout.Width(130)))
            {
                var picked = EditorUtility.OpenFilePanel("Load UTF-8 text", Application.dataPath, "txt");
                if (!string.IsNullOrEmpty(picked))
                {
                    inputText = File.ReadAllText(picked, Encoding.UTF8);
                }
            }
        }
    }

    private void DrawFontSourcesSection()
    {
        EditorGUILayout.LabelField("Fallback Font Sources", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Order matters. Each character is exported from the first enabled font that contains it.", MessageType.None);

        for (var i = 0; i < fontSources.Count; i++)
        {
            var source = fontSources[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    source.Enabled = EditorGUILayout.Toggle(source.Enabled, GUILayout.Width(20));
                    source.Label = EditorGUILayout.TextField(source.Label);

                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button("Up", GUILayout.Width(42)))
                        {
                            SwapFontSources(i, i - 1);
                            GUIUtility.ExitGUI();
                        }
                    }

                    using (new EditorGUI.DisabledScope(i == fontSources.Count - 1))
                    {
                        if (GUILayout.Button("Down", GUILayout.Width(52)))
                        {
                            SwapFontSources(i, i + 1);
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(68)))
                    {
                        fontSources.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    source.Path = EditorGUILayout.TextField("Path", source.Path);
                    if (GUILayout.Button("Browse", GUILayout.Width(80)))
                    {
                        var picked = EditorUtility.OpenFilePanel("Select font", GuessStartDirectory(source.Path), string.Empty);
                        if (!string.IsNullOrEmpty(picked))
                        {
                            source.Path = picked;
                            if (string.IsNullOrWhiteSpace(source.Label))
                            {
                                source.Label = Path.GetFileNameWithoutExtension(picked);
                            }
                        }
                    }
                }

                var extension = Path.GetExtension(source.Path).ToLowerInvariant();
                if (extension == ".ttc" || extension == ".otc")
                {
                    source.FontIndex = Mathf.Max(0, EditorGUILayout.IntField("Collection Font Index", source.FontIndex));
                }

                if (!string.IsNullOrWhiteSpace(source.Path) && !File.Exists(source.Path))
                {
                    EditorGUILayout.HelpBox("Font file does not exist.", MessageType.Warning);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Font File"))
            {
                var picked = EditorUtility.OpenFilePanel("Select font", Application.dataPath, string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    AddFontSource(picked);
                }
            }

            if (GUILayout.Button("Add Font Folder"))
            {
                var picked = EditorUtility.OpenFolderPanel("Select font folder", Application.dataPath, string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    AddFontFolder(picked);
                }
            }

            if (GUILayout.Button("Add System Defaults"))
            {
                AddSystemDefaultFonts();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            fontAssetToAdd = EditorGUILayout.ObjectField("Unity Font Asset", fontAssetToAdd, typeof(Font), false);
            using (new EditorGUI.DisabledScope(fontAssetToAdd == null))
            {
                if (GUILayout.Button("Add Asset", GUILayout.Width(90)))
                {
                    var assetPath = AssetDatabase.GetAssetPath(fontAssetToAdd);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        AddFontSource(Path.GetFullPath(assetPath));
                    }
                }
            }
        }
    }

    private void DrawOutputSection()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        outputPrefix = EditorGUILayout.TextField("File Prefix", outputPrefix);

        using (new EditorGUILayout.HorizontalScope())
        {
            outputDirectory = EditorGUILayout.TextField("Folder", outputDirectory);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                var picked = EditorUtility.OpenFolderPanel("Select output folder", GuessStartDirectory(outputDirectory), string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    outputDirectory = picked;
                }
            }
        }

        if (IsInsideProject(outputDirectory))
        {
            EditorGUILayout.HelpBox("Output is inside this Unity project. Exported fonts will be imported automatically.", MessageType.None);
        }
    }

    private void DrawFonttoolsSection()
    {
        EditorGUILayout.LabelField("Python / fonttools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            pythonPath = EditorGUILayout.TextField("Python Path", pythonPath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                var picked = EditorUtility.OpenFilePanel("Select Python executable", GuessStartDirectory(pythonPath), string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    pythonPath = picked;
                }
            }
        }
        EditorGUILayout.HelpBox("Leave empty to auto-detect a Python that already has fonttools. If none is found, use Install/Update fonttools or choose a Python executable manually.", MessageType.None);

        includeRecommendedGlyphs = EditorGUILayout.ToggleLeft("Include recommended glyphs (.notdef, space, basic fallback glyphs)", includeRecommendedGlyphs);
        keepLayoutFeatures = EditorGUILayout.ToggleLeft("Keep OpenType layout features", keepLayoutFeatures);
    }

    private void DrawActions()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Check fonttools", GUILayout.Height(28)))
            {
                SavePrefs();
                CheckFonttools();
            }

            if (GUILayout.Button("Install/Update fonttools", GUILayout.Height(28)))
            {
                SavePrefs();
                InstallFonttools();
            }
        }

        using (new EditorGUI.DisabledScope(!CanExport()))
        {
            if (GUILayout.Button("Export Single TTF", GUILayout.Height(36)))
            {
                SavePrefs();
                ExportSingleFont();
            }
        }
    }

    private bool CanExport()
    {
        if (string.IsNullOrEmpty(inputText) || string.IsNullOrWhiteSpace(outputDirectory) || string.IsNullOrWhiteSpace(outputPrefix))
        {
            return false;
        }

        foreach (var source in fontSources)
        {
            if (source.Enabled && !string.IsNullOrWhiteSpace(source.Path))
            {
                return true;
            }
        }

        return false;
    }

    private void ExportSingleFont()
    {
        var tempTextFile = string.Empty;
        var tempFontsFile = string.Empty;

        try
        {
            ValidateExportInputs();
            Directory.CreateDirectory(outputDirectory);

            var sanitizedPrefix = SanitizeFileName(outputPrefix);

            tempTextFile = Path.Combine(Path.GetTempPath(), $"unity-font-text-{Guid.NewGuid():N}.txt");
            tempFontsFile = Path.Combine(Path.GetTempPath(), $"unity-font-sources-{Guid.NewGuid():N}.tsv");
            File.WriteAllText(tempTextFile, inputText, new UTF8Encoding(false));
            File.WriteAllText(tempFontsFile, BuildFontSourceManifest(), new UTF8Encoding(false));

            var python = ResolvePythonExecutable();
            EnsureFonttoolsAvailable(python);

            var helperScript = GetHelperScriptPath();
            var result = RunFontExport(
                python,
                helperScript,
                tempFontsFile,
                tempTextFile,
                sanitizedPrefix,
                sanitizedPrefix,
                "Regular");
            var exportedFontPath = Path.Combine(outputDirectory, sanitizedPrefix + ".ttf");

            DeleteCoverageReportFiles(outputDirectory, sanitizedPrefix);
            DeleteGeneratedBoldFontFiles(outputDirectory, sanitizedPrefix);

            if (IsInsideProject(outputDirectory))
            {
                AssetDatabase.Refresh();
            }

            ApplyExportedFontSettings(exportedFontPath);
            UpdatePlayworksDefaultFontSettings();
            AssignExportedFontAndSaveScene(exportedFontPath);

            Debug.Log(result.Output);
            EditorUtility.DisplayDialog("Font Subset Exporter", $"Export completed.\n\n{exportedFontPath}", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogError(exception);
            EditorUtility.DisplayDialog("Font Subset Exporter", exception.Message, "OK");
        }
        finally
        {
            TryDelete(tempTextFile);
            TryDelete(tempFontsFile);
        }
    }

    private ProcessResult RunFontExport(
        PythonExecutable python,
        string helperScript,
        string fontsFile,
        string textFile,
        string outputFilePrefix,
        string fontFamilyName,
        string fontStyleName)
    {
        var args = new List<string>();
        args.AddRange(python.Arguments);
        args.Add(helperScript);
        args.Add("--fonts-file");
        args.Add(fontsFile);
        args.Add("--text-file");
        args.Add(textFile);
        args.Add("--output-dir");
        args.Add(outputDirectory);
        args.Add("--prefix");
        args.Add(outputFilePrefix);
        args.Add("--family-name");
        args.Add(fontFamilyName);
        args.Add("--style-name");
        args.Add(fontStyleName);

        if (includeRecommendedGlyphs)
        {
            args.Add("--include-recommended-glyphs");
        }

        if (keepLayoutFeatures)
        {
            args.Add("--keep-layout-features");
        }

        var result = RunProcess(python.FileName, args.ToArray());
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Export failed with exit code {result.ExitCode}\n\n{result.Output}");
        }

        return result;
    }

    /// <summary>
    /// 清理旧版本导出时生成的覆盖率报告，避免它们留在 Resources 中参与打包。
    /// </summary>
    private static void DeleteCoverageReportFiles(string directory, string prefix)
    {
        var basePath = Path.Combine(directory, prefix + "_coverage_report");
        TryDelete(basePath + ".csv");
        TryDelete(basePath + ".csv.meta");
        TryDelete(basePath + ".json");
        TryDelete(basePath + ".json.meta");
    }

    /// <summary>
    /// 清理已废弃的 Bold 变体导出文件，避免旧资源继续参与打包。
    /// </summary>
    private static void DeleteGeneratedBoldFontFiles(string directory, string prefix)
    {
        var basePath = Path.Combine(directory, prefix + "_Bold");
        TryDelete(basePath + ".ttf");
        TryDelete(basePath + ".ttf.meta");
    }

    /// <summary>
    /// 配置导出字体的 Unity 导入参数。
    /// </summary>
    private static void ApplyExportedFontSettings(string exportedFontPath)
    {
        var assetPath = GetAssetPath(exportedFontPath);
        var importer = AssetImporter.GetAtPath(assetPath) as TrueTypeFontImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Cannot find the exported font importer: {assetPath}");
        }

        importer.fontSize = 64;
        importer.fontTextureCase = FontTextureCase.Dynamic;
        importer.shouldRoundAdvanceValue = false;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// 同步 Playworks 默认字体的字母表、字形大小和纹理尺寸。
    /// </summary>
    private void UpdatePlayworksDefaultFontSettings()
    {   
        var lunaSettingsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "luna.json"));
        if (!File.Exists(lunaSettingsPath))
        {
            throw new FileNotFoundException("Cannot find the Playworks settings file.", lunaSettingsPath);
        }

        var root = JObject.Parse(File.ReadAllText(lunaSettingsPath, Encoding.UTF8));
        var assets = GetOrCreateObject(root, "assets");
        var rules = GetOrCreateObject(assets, "rules");
        var font = GetOrCreateObject(rules, "font");
        var defaultFont = GetOrCreateObject(font, "default");
        var data = GetOrCreateObject(defaultFont, "data");

        data["alphabet"] = inputText;
        data["size"] = 64;
        data["textureWidth"] = 2048;
        data["textureHeight"] = 2048;

        File.WriteAllText(lunaSettingsPath, root.ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    /// <summary>
    /// 将导出字体应用到文本组件，并保存当前活动场景。
    /// </summary>
    private void AssignExportedFontAndSaveScene(string exportedFontPath)
    {
        var assetPath = GetAssetPath(exportedFontPath);
        var exportedFont = AssetDatabase.LoadAssetAtPath<Font>(assetPath);
        if (exportedFont == null)
        {
            throw new InvalidOperationException($"Cannot load the exported font: {assetPath}");
        }

        foreach (var item in conversionItems)
        {
            if (item.textComponent == null)
            {
                continue;
            }

            item.textComponent.font = exportedFont;
            EditorUtility.SetDirty(item.textComponent);
        }

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
        {
            throw new InvalidOperationException("The active scene has not been saved. Save it once before exporting the font.");
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        if (!EditorSceneManager.SaveScene(activeScene))
        {
            throw new InvalidOperationException($"Failed to save the active scene: {activeScene.path}");
        }
    }

    private static JObject GetOrCreateObject(JObject parent, string propertyName)
    {
        var child = parent[propertyName] as JObject;
        if (child != null)
        {
            return child;
        }

        child = new JObject();
        parent[propertyName] = child;
        return child;
    }

    private static string GetAssetPath(string filePath)
    {
        var assetPath = NormalizePath(FileUtil.GetProjectRelativePath(Path.GetFullPath(filePath)));
        if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The exported font must be located under the project's Assets folder.");
        }

        return assetPath;
    }

    private void ValidateExportInputs()
    {
        if (string.IsNullOrEmpty(inputText))
        {
            throw new InvalidOperationException("Input text is empty.");
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException("Output folder is empty.");
        }

        var enabledCount = 0;
        foreach (var source in fontSources)
        {
            if (!source.Enabled)
            {
                continue;
            }

            enabledCount++;
            if (string.IsNullOrWhiteSpace(source.Path) || !File.Exists(source.Path))
            {
                throw new FileNotFoundException("Enabled font file does not exist.", source.Path);
            }
        }

        if (enabledCount == 0)
        {
            throw new InvalidOperationException("Add at least one enabled font source.");
        }

        if (!File.Exists(GetHelperScriptPath()))
        {
            throw new FileNotFoundException("Missing Python helper script.", GetHelperScriptPath());
        }
    }

    private string BuildFontSourceManifest()
    {
        var builder = new StringBuilder();
        foreach (var source in fontSources)
        {
            if (!source.Enabled)
            {
                continue;
            }

            builder.Append(source.Path.Replace('\t', ' '));
            builder.Append('\t');
            builder.Append(Mathf.Max(0, source.FontIndex));
            builder.Append('\t');
            builder.Append(string.IsNullOrWhiteSpace(source.Label) ? Path.GetFileNameWithoutExtension(source.Path) : source.Label.Replace('\t', ' '));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private void CheckFonttools()
    {
        try
        {
            var python = ResolvePythonExecutable();
            var check = GetFonttoolsStatus(python);
            if (check.IsAvailable)
            {
                EditorUtility.DisplayDialog(
                    "Font Subset Exporter",
                    $"fonttools is available.\n\nPython: {check.PythonExecutable}\nVersion: {check.Version}",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Font Subset Exporter", BuildMissingFonttoolsMessage(python, check.Output), "OK");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(exception);
            EditorUtility.DisplayDialog("Font Subset Exporter", exception.Message, "OK");
        }
    }

    private void InstallFonttools()
    {
        try
        {
            var python = ResolvePythonExecutable();
            var args = new List<string>();
            args.AddRange(python.Arguments);
            args.Add("-m");
            args.Add("pip");
            args.Add("install");
            args.Add("--upgrade");
            args.Add("fonttools");
            var result = RunProcess(python.FileName, args.ToArray());
            if (result.ExitCode != 0)
            {
                var ensurePipArgs = new List<string>();
                ensurePipArgs.AddRange(python.Arguments);
                ensurePipArgs.Add("-m");
                ensurePipArgs.Add("ensurepip");
                ensurePipArgs.Add("--upgrade");
                RunProcess(python.FileName, ensurePipArgs.ToArray());

                result = RunProcess(python.FileName, args.ToArray());
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"pip failed with exit code {result.ExitCode}\n\n{result.Output}\n\nManual command:\n{BuildPythonCommand(python)} -m pip install --upgrade fonttools");
                }
            }

            EditorUtility.DisplayDialog("Font Subset Exporter", $"fonttools installed or updated.\n\n{result.Output.Trim()}", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogError(exception);
            EditorUtility.DisplayDialog("Font Subset Exporter", exception.Message, "OK");
        }
    }

    private void AddFontSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        path = Path.GetFullPath(path);
        foreach (var source in fontSources)
        {
            if (string.Equals(Path.GetFullPath(source.Path), path, StringComparison.OrdinalIgnoreCase) && source.FontIndex == 0)
            {
                return;
            }
        }

        fontSources.Add(new FontSource
        {
            Enabled = true,
            Path = path,
            FontIndex = 0,
            Label = Path.GetFileNameWithoutExtension(path)
        });
    }

    private void AddFontFolder(string folder)
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ttf", ".ttc", ".otf", ".otc" };
        foreach (var file in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
        {
            if (extensions.Contains(Path.GetExtension(file)))
            {
                AddFontSource(file);
            }
        }
    }

    private void AddSystemDefaultFonts()
    {
        var candidates = new List<string>();

        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            candidates.Add(Path.Combine(fonts, "arial.ttf"));
            candidates.Add(Path.Combine(fonts, "segoeui.ttf"));
            candidates.Add(Path.Combine(fonts, "msyh.ttc"));
            candidates.Add(Path.Combine(fonts, "simsun.ttc"));
            candidates.Add(Path.Combine(fonts, "meiryo.ttc"));
            candidates.Add(Path.Combine(fonts, "malgun.ttf"));
        }
        else
        {
            candidates.Add("/System/Library/Fonts/Supplemental/Arial.ttf");
            candidates.Add("/System/Library/Fonts/Supplemental/Arial Unicode.ttf");
            candidates.Add("/System/Library/Fonts/PingFang.ttc");
            candidates.Add("/System/Library/Fonts/AppleSDGothicNeo.ttc");
            candidates.Add("/System/Library/Fonts/Apple Color Emoji.ttc");
        }

        var added = 0;
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                AddFontSource(candidate);
                added++;
            }
        }

        if (added == 0)
        {
            EditorUtility.DisplayDialog("Font Subset Exporter", "No known system fonts were found. Add font files or a Noto font folder manually.", "OK");
        }
    }

    private void SwapFontSources(int a, int b)
    {
        var temp = fontSources[a];
        fontSources[a] = fontSources[b];
        fontSources[b] = temp;
    }

    private void LoadFontSources()
    {
        fontSources.Clear();
        var serialized = EditorPrefs.GetString(FontsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return;
        }

        var lines = serialized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.TrimEnd('\r').Split('\t');
            if (parts.Length < 4)
            {
                continue;
            }

            int.TryParse(parts[1], out var fontIndex);
            bool.TryParse(parts[2], out var enabled);
            fontSources.Add(new FontSource
            {
                Path = parts[0],
                FontIndex = Mathf.Max(0, fontIndex),
                Enabled = enabled,
                Label = parts[3]
            });
        }
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(TextKey, inputText ?? string.Empty);
        EditorPrefs.SetString(OutputDirectoryKey, outputDirectory ?? string.Empty);
        EditorPrefs.SetString(OutputPrefixKey, outputPrefix ?? string.Empty);
        EditorPrefs.SetString(PythonPathKey, pythonPath ?? string.Empty);
        EditorPrefs.SetBool(IncludeRecommendedGlyphsKey, includeRecommendedGlyphs);
        EditorPrefs.SetBool(KeepLayoutFeaturesKey, keepLayoutFeatures);

        var builder = new StringBuilder();
        foreach (var source in fontSources)
        {
            builder.Append(source.Path?.Replace('\t', ' ') ?? string.Empty);
            builder.Append('\t');
            builder.Append(Mathf.Max(0, source.FontIndex));
            builder.Append('\t');
            builder.Append(source.Enabled);
            builder.Append('\t');
            builder.Append(source.Label?.Replace('\t', ' ') ?? string.Empty);
            builder.AppendLine();
        }

        EditorPrefs.SetString(FontsKey, builder.ToString());
    }

    private PythonExecutable ResolvePythonExecutable()
    {
        if (!string.IsNullOrWhiteSpace(pythonPath))
        {
            return new PythonExecutable(pythonPath, Array.Empty<string>());
        }

        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            return new PythonExecutable("py", new[] { "-3" });
        }

        if (TryFindPythonWithFonttools(out var detectedPython))
        {
            return detectedPython;
        }

        if (TryFindExistingPython(out detectedPython))
        {
            return detectedPython;
        }

        return new PythonExecutable("python3", Array.Empty<string>());
    }

    private void EnsureFonttoolsAvailable(PythonExecutable python)
    {
        var check = GetFonttoolsStatus(python);
        if (check.IsAvailable)
        {
            return;
        }

        throw new InvalidOperationException(BuildMissingFonttoolsMessage(python, check.Output));
    }

    private static FonttoolsStatus GetFonttoolsStatus(PythonExecutable python)
    {
        var args = new List<string>();
        args.AddRange(python.Arguments);
        args.Add("-c");
        args.Add(FonttoolsImportCheckScript);
        var result = RunProcess(python.FileName, args.ToArray());
        var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        return new FonttoolsStatus(
            result.ExitCode == 0,
            lines.Length > 0 ? lines[0] : BuildPythonCommand(python),
            lines.Length > 1 ? lines[1] : string.Empty,
            result.Output.Trim());
    }

    private static string BuildMissingFonttoolsMessage(PythonExecutable python, string details)
    {
        return "fonttools is not installed for the Python executable Unity is using.\n\n"
            + $"Python command: {BuildPythonCommand(python)}\n\n"
            + "Click Install/Update fonttools, or choose a Python executable that already has fonttools installed.\n\n"
            + $"Manual command:\n{BuildPythonCommand(python)} -m pip install --upgrade fonttools\n\n"
            + $"Details:\n{details}";
    }

    private static string BuildPythonCommand(PythonExecutable python)
    {
        var args = new List<string> { python.FileName };
        args.AddRange(python.Arguments);
        return JoinArguments(args.ToArray());
    }

    private static bool TryFindPythonWithFonttools(out PythonExecutable python)
    {
        foreach (var candidate in GetPythonCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (candidate.IndexOfAny(new[] { '/', '\\' }) >= 0 && !File.Exists(candidate))
            {
                continue;
            }

            var candidatePython = new PythonExecutable(candidate, Array.Empty<string>());
            try
            {
                if (GetFonttoolsStatus(candidatePython).IsAvailable)
                {
                    python = candidatePython;
                    return true;
                }
            }
            catch
            {
                // Keep trying other common Python locations.
            }
        }

        python = default(PythonExecutable);
        return false;
    }

    private static bool TryFindExistingPython(out PythonExecutable python)
    {
        foreach (var candidate in GetPythonCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (candidate.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                python = new PythonExecutable(candidate, Array.Empty<string>());
                return true;
            }

            try
            {
                var candidatePython = new PythonExecutable(candidate, Array.Empty<string>());
                var result = RunPythonOneLiner(candidatePython, "import sys; print(sys.executable)");
                if (result.ExitCode == 0)
                {
                    python = candidatePython;
                    return true;
                }
            }
            catch
            {
                // Keep trying other common Python locations.
            }
        }

        python = default(PythonExecutable);
        return false;
    }

    private static List<string> GetPythonCandidates()
    {
        var candidates = new List<string>
        {
            "python3",
            "/Library/Frameworks/Python.framework/Versions/Current/bin/python3",
            "/opt/homebrew/bin/python3",
            "/usr/local/bin/python3",
            "/usr/bin/python3"
        };

        var frameworkVersions = "/Library/Frameworks/Python.framework/Versions";
        if (!Directory.Exists(frameworkVersions))
        {
            return candidates;
        }

        string[] versionDirectories;
        try
        {
            versionDirectories = Directory.GetDirectories(frameworkVersions);
        }
        catch
        {
            return candidates;
        }

        Array.Sort(versionDirectories, StringComparer.OrdinalIgnoreCase);
        Array.Reverse(versionDirectories);
        foreach (var directory in versionDirectories)
        {
            candidates.Add(Path.Combine(directory, "bin", "python3"));
        }

        return candidates;
    }

    private static ProcessResult RunPythonOneLiner(PythonExecutable python, string script)
    {
        var args = new List<string>();
        args.AddRange(python.Arguments);
        args.Add("-c");
        args.Add(script);
        return RunProcess(python.FileName, args.ToArray());
    }

    private static ProcessResult RunProcess(string fileName, string[] args)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = JoinArguments(args),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using (var process = new Process { StartInfo = processStartInfo })
        {
            var outputBuilder = new StringBuilder();
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data != null)
                {
                    outputBuilder.AppendLine(eventArgs.Data);
                }
            };
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data != null)
                {
                    outputBuilder.AppendLine(eventArgs.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(180000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Ignore cleanup failures after timeout.
                }

                throw new TimeoutException("fonttools timed out after 180 seconds.");
            }

            process.WaitForExit();
            return new ProcessResult(process.ExitCode, outputBuilder.ToString());
        }
    }

    private string GetHelperScriptPath()
    {
        return Path.GetFullPath("Assets/Editor/FontSubsetExporter/font_subset_pipeline.py");
    }

    private static string JoinArguments(string[] args)
    {
        var builder = new StringBuilder();
        foreach (var arg in args)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(QuoteArgument(arg));
        }

        return builder.ToString();
    }

    private static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        var needsQuotes = argument.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) >= 0;
        if (!needsQuotes)
        {
            return argument;
        }

        var builder = new StringBuilder();
        builder.Append('"');
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            backslashes = 0;
            builder.Append(character);
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }

    private static int CountUniqueCodePoints(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var unique = new HashSet<int>();
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                unique.Add(char.ConvertToUtf32(text[i], text[i + 1]));
                i++;
            }
            else
            {
                unique.Add(text[i]);
            }
        }

        return unique.Count;
    }

    private static string GuessStartDirectory(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        return Application.dataPath;
    }

    private static bool IsInsideProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullPath = NormalizePath(Path.GetFullPath(path));
        var projectPath = NormalizePath(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
        return fullPath.StartsWith(projectPath + "/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, projectPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder();
        foreach (var character in fileName)
        {
            builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
        }

        return builder.Length == 0 ? "GameFont" : builder.ToString();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary file cleanup can fail if another process is still holding it.
        }
    }

    private readonly struct PythonExecutable
    {
        public readonly string FileName;
        public readonly string[] Arguments;

        public PythonExecutable(string fileName, string[] arguments)
        {
            FileName = fileName;
            Arguments = arguments ?? Array.Empty<string>();
        }
    }

    private readonly struct FonttoolsStatus
    {
        public readonly bool IsAvailable;
        public readonly string PythonExecutable;
        public readonly string Version;
        public readonly string Output;

        public FonttoolsStatus(bool isAvailable, string pythonExecutable, string version, string output)
        {
            IsAvailable = isAvailable;
            PythonExecutable = pythonExecutable;
            Version = version;
            Output = output;
        }
    }

    private readonly struct ProcessResult
    {
        public readonly int ExitCode;
        public readonly string Output;

        public ProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }
    }

    [Serializable]
    private sealed class FontSource
    {
        public bool Enabled;
        public string Path;
        public int FontIndex;
        public string Label;
    }
}
