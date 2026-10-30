#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Security.Cryptography;
using System.IO.Compression;
using UnityEditor.SceneManagement;

// Place this file under Assets/Editor. No additional packages are required.
public sealed class UnityPlayableUploaderWindow : EditorWindow
{
    [Serializable] private class HtmlPayload { public string sourceName; public string content; public string currentDir; }
    [Serializable] private class ZipPayload { public string sourceName; public string archiveContent; public string currentDir; }
    [Serializable] private class UploadResponse { public bool ok; public string error; public string outputName; }

    [Serializable] private class DirectoryItem { public string name; }
    [Serializable] private class DirectoryResponse { public bool ok; public string error; public DirectoryItem[] dirs; }
    [Serializable] private class FileListResponse { public DirectoryItem[] files; }
    private string browserDirectory, browserFile, browserUrl;
    private enum RequestStage { List, Upload, OpenDirectory }
    private RequestStage stage;
    private readonly List<string> directories = new List<string>();
    private bool directoriesLoaded;
    private string pendingDirectory, uploadName, pendingOutputName;
    private string server, directory, filePath, status = "选择 Unity/Luna HTML 或构建 ZIP。";
    private sealed class HttpResult { public string body; public long code; public bool success; public string error; }
    private Task<HttpResult> request;
    private CancellationTokenSource cancellation;
    private bool failed;
    private Task<bool> buildTask;
    private DateTime buildStartedUtc;
    private string buildZipFolder;
    private Dictionary<string, long> previousBuildZips;
    private double buildStartedAt;
    private bool windowActive;
    private bool pluginSupported;
    private string pluginRequirementMessage;
    private bool openingPlugin;
    private double pluginOpenStarted;
    private bool Busy { get { return request != null || buildTask != null || openingPlugin; } }
    private string Pref(string key) { return "UnityPlayableUploader." + Application.dataPath + "." + key; }

    private static UnityPlayableUploaderWindow utilityWindow;

    [MenuItem("Tools/上传 Unity 试玩")]
    public static void Open()
    {
        if (utilityWindow != null)
        {
            utilityWindow.Focus();
            return;
        }
        // GetWindow can reuse a normal window and cannot convert its native window type.
        // Create the instance without showing it, then let ShowUtility create the host.
        foreach (var existing in Resources.FindObjectsOfTypeAll<UnityPlayableUploaderWindow>())
        {
            if (existing.Busy)
            {
                existing.Focus();
                existing.ShowNotification(new GUIContent("请等待当前操作结束后重新打开工具浮窗。"));
                return;
            }
        }
        foreach (var existing in Resources.FindObjectsOfTypeAll<UnityPlayableUploaderWindow>())
            existing.Close();
        var window = CreateInstance<UnityPlayableUploaderWindow>();
        window.titleContent = new GUIContent("上传 Unity 试玩");
        window.ShowUtility();
        utilityWindow = window;
        window.Focus();
    }

    private void OnEnable()
    {
        windowActive = true;
        minSize = new Vector2(480, 420);
        server = EditorPrefs.GetString(Pref("server"), "http://192.168.3.245:8080");
        directory = EditorPrefs.GetString(Pref("directory"), "Unity项目配置");
        filePath = EditorPrefs.GetString(Pref("file"), "");
        uploadName = EditorPrefs.GetString(Pref("uploadName"), new DirectoryInfo(Path.GetDirectoryName(Application.dataPath)).Name);
        try
        {
            Playworks720BuildBridge.GetCompatiblePlugin();
            pluginSupported = true;
            pluginRequirementMessage = null;
        }
        catch (Exception ex)
        {
            pluginSupported = false;
            pluginRequirementMessage = ex.GetBaseException().Message;
        }
        if (pluginSupported) RefreshDirectories();
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString(Pref("server"), server ?? "");
        EditorPrefs.SetString(Pref("directory"), directory ?? "");
        EditorPrefs.SetString(Pref("file"), filePath ?? "");
        EditorPrefs.SetString(Pref("uploadName"), uploadName ?? "");
    }

    private void OnDisable()
    {
        windowActive = false;
        DetachBuild();
        SaveSettings();
        ReleaseRequest(true);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Unity 试玩上传", EditorStyles.boldLabel);
        if (!pluginSupported)
        {
            EditorGUILayout.HelpBox(pluginRequirementMessage ?? "请安装或升级到 Unity Playworks Plugin 7.2.0 后使用。", MessageType.Warning);
            return;
        }
        EditorGUILayout.HelpBox("直接调用管理器现有上传接口。HTML 按文本发送，ZIP 交由服务器转换。请选择已有目标目录；需要新文件夹时，请先在网页创建，再刷新目录。", MessageType.Info);
        using (new EditorGUI.DisabledScope(Busy))
        {
            EditorGUI.BeginChangeCheck();
            server = EditorGUILayout.TextField("服务器地址", server);
            if (EditorGUI.EndChangeCheck()) { directoriesLoaded = false; directories.Clear(); }
            if (GUILayout.Button("刷新目录")) RefreshDirectories();
            using (new EditorGUI.DisabledScope(!directoriesLoaded))
            {
                var labels = new List<string>();
                foreach (string path in directories) labels.Add(path == "." ? "/（根目录）" : path);
                int oldIndex = directories.IndexOf(directory);
                int index = EditorGUILayout.Popup("目标目录", oldIndex, labels.ToArray());
                if (index != oldIndex && index >= 0 && index < directories.Count)
                {
                    directory = directories[index];
                    SaveSettings();
                }
            }
            if (!directoriesLoaded) EditorGUILayout.HelpBox("请先读取服务器目录，失败后可点击刷新重试。", MessageType.Info);
            else if (!directories.Contains(directory)) EditorGUILayout.HelpBox("上次目录已不存在，请重新选择。", MessageType.Warning);
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            uploadName = EditorGUILayout.TextField("上传文件名", uploadName);
            if (EditorGUI.EndChangeCheck()) SaveSettings();
            EditorGUILayout.LabelField("文件名可省略 .html；留空时使用所选文件名。", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(!directoriesLoaded || !directories.Contains(directory)))
            {
                if (GUILayout.Button("构建并上传内网", GUILayout.Height(36))) StartBuildAndUpload();
            }
            EditorGUILayout.LabelField("构建参数在 Playworks 插件中设置；本窗口只负责构建和内网上传。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(16);
            EditorGUILayout.LabelField("手动上传已有文件", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("使用上方的目标目录和上传文件名；自动构建上传无需选择本地文件。", EditorStyles.wordWrappedMiniLabel);
            filePath = EditorGUILayout.TextField("文件路径", filePath);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("选择 HTML / ZIP 文件"))
                {
                    string initial = File.Exists(filePath) ? Path.GetDirectoryName(filePath) : Application.dataPath;
                    string chosen = EditorUtility.OpenFilePanelWithFilters("选择 Unity 试玩", initial,
                        new[] { "Unity playable", "html,htm,zip" });
                    if (!string.IsNullOrEmpty(chosen)) { filePath = chosen; SaveSettings(); }
                }
                if (GUILayout.Button("自动定位构建 ZIP")) LocateBuildZip();
            }
            using (new EditorGUI.DisabledScope(!directoriesLoaded || !directories.Contains(directory)))
            {
                if (GUILayout.Button("上传已有文件", GUILayout.Height(28))) StartManualUpload();
            }
        }
        if (buildTask != null || openingPlugin)
        {
            EditorGUILayout.LabelField(openingPlugin ? "正在打开 Playworks 并等待初始化…" : "Playworks 构建中 · " + (int)(EditorApplication.timeSinceStartup - buildStartedAt) + " 秒");
            if (GUILayout.Button("取消后续自动上传"))
            {
                DetachBuild();
                status = "已取消后续自动上传。Playworks 构建仍会继续，可在插件窗口取消构建。";
            }
        }
        if (request != null)
        {
            Rect rect = GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, Mathf.PingPong((float)EditorApplication.timeSinceStartup * 0.25f, 1f),
                stage == RequestStage.Upload ? "正在上传并等待服务器处理…" : "读取目录中…");
            if (GUILayout.Button("停止等待"))
            {
                ReleaseRequest(true);
                status = "已停止等待。服务器可能已收到文件，请检查结果后再决定是否重试。";
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(status, failed ? MessageType.Error : MessageType.Info);
        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(Busy))
        {
            if (GUILayout.Button("打开内网服务器", GUILayout.Height(28)))
            {
                try
                {
                    browserDirectory = string.IsNullOrWhiteSpace(directory) ? "." : directory;
                    browserUrl = ServerRoot() + "/playable-manager.html?dir=" + Uri.EscapeDataString(browserDirectory);
                    string selectedName = string.IsNullOrEmpty(filePath) ? "_build.zip" : Path.GetFileName(filePath);
                    browserFile = NormalizeUploadName(uploadName, selectedName) + ".html";
                    failed = false;
                    BeginRequest(ServerRoot() + "/api/files?dir=" + Uri.EscapeDataString(browserDirectory) + "&fast=1&refresh=1", null, RequestStage.OpenDirectory);
                    status = "正在查找目标目录中的上传文件…";
                }
                catch (Exception ex) { failed = true; status = ex.GetBaseException().Message; }
            }
        }
    }

    private void LocateBuildZip()
    {
        if (!pluginSupported || Busy) return;
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "LunaTemp", "stage4", "create-hub");
            string[] candidates = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*_build.zip", SearchOption.TopDirectoryOnly)
                : new string[0];
            if (candidates.Length == 0)
                throw new Exception("未找到构建 ZIP，请先使用 Playworks 插件构建。查找目录：" + folder);
            // Creative names may prefix _build.zip; select the most recently generated archive.
            Array.Sort(candidates, (a, b) => {
                int order = File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
                return order != 0 ? order : string.CompareOrdinal(a, b);
            });
            filePath = candidates[0];
            SaveSettings();
            failed = false;
            status = "已定位" + (candidates.Length > 1 ? "最新构建 ZIP：" : "构建 ZIP：") + filePath + "\n点击“上传已有文件”即可上传。";
        }
        catch (Exception ex)
        {
            failed = true;
            status = ex.GetBaseException().Message;
        }
    }

    private string ServerRoot()
    {
        string root = (server ?? "").Trim().TrimEnd('/');
        Uri uri;
        if (!Uri.TryCreate(root, UriKind.Absolute, out uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new Exception("请输入有效的 HTTP/HTTPS 服务器地址。");
        return root;
    }

    private void BeginRequest(string url, string json, RequestStage nextStage)
    {
        stage = nextStage;
        cancellation = new CancellationTokenSource();
        request = SendHttpAsync(url, json, nextStage == RequestStage.Upload ? 600 : 30, cancellation.Token);
        EditorApplication.update += Poll;
    }

    // Editor-only HTTP transport; does not change PlayerSettings or TLS validation.
    private static async Task<HttpResult> SendHttpAsync(string url, string json, int timeoutSeconds, CancellationToken token)
    {
        try
        {
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
            using (var client = new HttpClient(handler))
            using (var message = new HttpRequestMessage(json == null ? HttpMethod.Get : HttpMethod.Post, url))
            {
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                if (json != null) message.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using (var response = await client.SendAsync(message, token).ConfigureAwait(false))
                {
                    return new HttpResult {
                        body = await response.Content.ReadAsStringAsync().ConfigureAwait(false),
                        code = (long)response.StatusCode,
                        success = response.IsSuccessStatusCode,
                        error = response.IsSuccessStatusCode ? null : response.ReasonPhrase
                    };
                }
            }
        }
        catch (OperationCanceledException)
        {
            return new HttpResult { error = token.IsCancellationRequested ? "请求已取消" : "请求超时" };
        }
        catch (Exception ex) { return new HttpResult { error = ex.GetBaseException().Message }; }
    }

    private void RefreshDirectories()
    {
        if (!pluginSupported || Busy) return;
        failed = false;
        directoriesLoaded = false;
        try
        {
            BeginRequest(ServerRoot() + "/api/directories?dir=.", null, RequestStage.List);
            status = "正在读取服务器已有文件夹…";
        }
        catch (Exception ex) { ReleaseRequest(true); failed = true; status = ex.Message; }
    }

    private static string NormalizeUploadName(string value, string selectedFileName)
    {
        string name = (value ?? string.Empty).Trim();
        if (name.Length == 0 || name == "“”" || name == "\"\"")
            name = selectedFileName;
        foreach (string suffix in new[] { ".html", ".htm", ".zip" })
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            { name = name.Substring(0, name.Length - suffix.Length).Trim(); break; }
        if (string.IsNullOrEmpty(name) || name == "." || name == ".." || name.EndsWith(".") ||
            name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
            throw new Exception("请输入有效的文件名，不要包含路径或特殊字符。");
        foreach (char c in name) if (char.IsControl(c)) throw new Exception("文件名不能包含控制字符。");
        return name;
    }

    private void StartBuildAndUpload()
    {
        if (!pluginSupported || Busy) return;
        failed = false;
        try
        {
            ServerRoot();
            if (!directoriesLoaded || !directories.Contains(directory)) throw new Exception("请先选择已有上传目录。");
            NormalizeUploadName(uploadName, "_build.zip");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new Exception("请在非运行模式下等待 Unity 编译、导入结束后再构建。");
            openingPlugin = true;
            pluginOpenStarted = EditorApplication.timeSinceStartup;
            status = "正在打开 Playworks 插件；初始化完成后使用已有配置继续构建上传。";
            if (!EditorApplication.ExecuteMenuItem("Tools/Unity Playworks Plugin"))
                throw new Exception("无法打开 Playworks 插件，请检查插件是否安装完整。");
            EditorApplication.update += PollPluginWindow;
        }
        catch (Exception ex) { DetachBuild(); failed = true; status = ex.GetBaseException().Message; }
    }

    private void PollPluginWindow()
    {
        if (!windowActive || !openingPlugin) { DetachBuild(); return; }
        Repaint();
        try
        {
            double elapsed = EditorApplication.timeSinceStartup - pluginOpenStarted;
            if (elapsed < 0.3) return;
            if (Playworks720BuildBridge.IsWindowReady())
            {
                EditorApplication.update -= PollPluginWindow;
                openingPlugin = false;
                ExecuteBuildAndUpload();
            }
            else if (elapsed > 30)
                throw new Exception("Playworks 窗口初始化超时，请在已打开的插件窗口处理登录或提示后重试。");
        }
        catch (Exception ex) { DetachBuild(); failed = true; status = ex.GetBaseException().Message; }
    }

    private void ExecuteBuildAndUpload()
    {
        if (!pluginSupported || Busy) return;
        failed = false;
        try
        {
            ServerRoot();
            if (!directoriesLoaded || !directories.Contains(directory)) throw new Exception("请先选择已有上传目录。");
            NormalizeUploadName(uploadName, "_build.zip");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new Exception("请在非运行模式下等待 Unity 编译、导入结束后再构建。");
            object manager;
            MethodInfo buildMethod = Playworks720BuildBridge.Resolve(out manager);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssets();
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            buildZipFolder = Path.Combine(projectRoot, "LunaTemp", "stage4", "create-hub");
            previousBuildZips = Playworks720BuildBridge.Snapshot(buildZipFolder);
            buildStartedUtc = DateTime.UtcNow;
            buildStartedAt = EditorApplication.timeSinceStartup;
            SaveSettings();
            status = "正在按 Playworks 当前配置构建 Playground ZIP；成功后自动上传内网。";
            // Invokes the existing project:build path, including plugin authentication and Unity export.
            // Source value 2 is the same source enum value used by the plugin's Build Project button.
            buildTask = (Task<bool>)buildMethod.Invoke(manager,
                new object[] { "playground", Enum.ToObject(buildMethod.GetParameters()[1].ParameterType, 2) });
            if (buildTask == null) throw new Exception("Playworks 未返回构建任务。");
            EditorApplication.update += PollBuild;
        }
        catch (Exception ex) { DetachBuild(); failed = true; status = ex.GetBaseException().Message; }
    }

    private void PollBuild()
    {
        if (buildTask == null) return;
        Repaint();
        if (!buildTask.IsCompleted) return;
        var completed = buildTask;
        EditorApplication.update -= PollBuild;
        buildTask = null;
        try
        {
            if (!completed.GetAwaiter().GetResult())
                throw new Exception("Playworks 构建失败或被取消，未上传。请查看插件诊断和 LunaTemp/luna.log。");
            if (!windowActive) return;
            filePath = Playworks720BuildBridge.FindFreshZip(buildZipFolder, previousBuildZips, buildStartedUtc);
            SaveSettings();
            StartUpload();
        }
        catch (Exception ex) { failed = true; status = "未自动上传：" + ex.GetBaseException().Message; }
        finally { previousBuildZips = null; Repaint(); }
    }

    private void DetachBuild()
    {
        EditorApplication.update -= PollPluginWindow;
        openingPlugin = false;
        EditorApplication.update -= PollBuild;
        // Observe a future failure without retaining this window or uploading after it is closed.
        if (buildTask != null)
            buildTask.ContinueWith(t => { var ignored = t.Exception; },
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        buildTask = null;
        previousBuildZips = null;
    }

    private void StartManualUpload()
    {
        if (!pluginSupported || Busy) return;
        try
        {
            if (Playworks720BuildBridge.IsBuildInProcess())
                throw new Exception("Playworks 正在构建，请等待构建完成后再上传已有文件，避免上传尚未生成完成的 ZIP。");
            StartUpload();
        }
        catch (Exception ex)
        {
            failed = true;
            status = ex.GetBaseException().Message;
        }
    }

    private void StartUpload()
    {
        if (!pluginSupported || Busy) return;
        failed = false;
        try
        {
            string root = ServerRoot();
            if (!directoriesLoaded) throw new Exception("请先刷新目录。");
            if (!File.Exists(filePath)) throw new Exception("请选择存在的 HTML 或 ZIP 文件。");
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".html" && extension != ".htm" && extension != ".zip")
                throw new Exception("仅支持 .html、.htm 或 .zip 文件。");
            string targetDir = directory;
            if (!directories.Contains(targetDir)) throw new Exception("请选择已有目录。");
            string baseName = NormalizeUploadName(uploadName, Path.GetFileName(filePath));
            pendingOutputName = baseName + ".html";
            string name = baseName + (extension == ".zip" ? ".zip" : ".html");
            string json = extension == ".zip"
                ? JsonUtility.ToJson(new ZipPayload { sourceName = name, archiveContent = Convert.ToBase64String(File.ReadAllBytes(filePath)), currentDir = targetDir })
                : JsonUtility.ToJson(new HtmlPayload { sourceName = name, content = File.ReadAllText(filePath, Encoding.UTF8), currentDir = targetDir });
            SaveSettings();
            pendingDirectory = targetDir;
            status = "正在上传 → /" + targetDir + "/" + pendingOutputName;
            BeginRequest(root + "/api/upload_unity_playable", json, RequestStage.Upload);
        }
        catch (Exception ex) { ReleaseRequest(true); failed = true; status = ex.Message; }
    }

    private void Poll()
    {
        if (request == null) return;
        Repaint();
        if (!request.IsCompleted) return;
        RequestStage completedStage = stage;
        HttpResult result = request.GetAwaiter().GetResult();
        string body = result.body;
        long code = result.code;
        bool transportOk = result.success;
        string transportError = result.error;
        ReleaseRequest(false);
        try
        {
            UploadResponse response = null;
            try { response = JsonUtility.FromJson<UploadResponse>(body); }
            catch (Exception) { }
            if (!transportOk || response == null || !response.ok)
            {
                string reason = response != null && !string.IsNullOrEmpty(response.error)
                    ? response.error : (transportError ?? "服务器未返回有效的成功结果");
                throw new Exception("请求未确认成功（HTTP " + code + "）：" + reason +
                    (completedStage != RequestStage.Upload ? "" : "\n如发生超时或断网，请先检查服务器结果，避免重复操作。"));
            }
            if (completedStage == RequestStage.List)
            {
                var listing = JsonUtility.FromJson<DirectoryResponse>(body);
                if (listing.dirs == null) throw new Exception("目录响应缺少 dirs，请检查服务器接口。");
                directories.Clear();
                directories.Add(".");
                foreach (var item in listing.dirs)
                    if (item != null && !string.IsNullOrEmpty(item.name) && !directories.Contains(item.name)) directories.Add(item.name);
                directories.Sort(1, directories.Count - 1, StringComparer.OrdinalIgnoreCase);
                directoriesLoaded = true;
                status = "已读取 " + (directories.Count - 1) + " 个文件夹，请选择上传位置。";
            }
            else if (completedStage == RequestStage.OpenDirectory)
            {
                var listing = JsonUtility.FromJson<FileListResponse>(body);
                if (listing.files == null) throw new Exception("文件列表响应缺少 files。");
                string selected = null;
                string prefix = browserDirectory == "." ? "" : browserDirectory.Trim('/') + "/";
                foreach (var item in listing.files)
                {
                    if (item == null || string.IsNullOrEmpty(item.name)) continue;
                    string name = item.name.Replace('\\', '/');
                    if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    name = name.Substring(prefix.Length);
                    if (name.Contains("/") || !name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) continue;
                    if (selected == null) selected = name;
                    if (string.Equals(name, browserFile, StringComparison.Ordinal)) { selected = name; break; }
                }
                Application.OpenURL(browserUrl + (selected == null ? "" : "&file=" + Uri.EscapeDataString(selected)));
                status = selected == null ? "已打开目标目录，目录中没有 HTML 文件。" : "已打开目标目录并选中：" + selected;
            }
            else
            {
                status = "上传成功 → /" + pendingDirectory + "\n" +
                    (string.IsNullOrEmpty(response.outputName) ? pendingOutputName : response.outputName);
                ClosePlayworksWindowAfterUpload();
            }
        }
        catch (Exception ex) { ReleaseRequest(true); failed = true; status = ex.Message; }
        finally { Repaint(); }
    }

    private void ClosePlayworksWindowAfterUpload()
    {
        try
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "EditorScripts") continue;
                Type mediator = assembly.GetType("Luna.Unity.Tutorial.TutorialMediator");
                PropertyInfo busy = mediator == null ? null : mediator.GetProperty("IsBuildInProcess");
                if (busy != null && (bool)busy.GetValue(null))
                {
                    status += "\n插件正在执行另一项构建，已保留插件窗口。";
                    return;
                }
                Type windowType = assembly.GetType("Luna.Unity.EditorScripts.Interface.LunaExportWindow");
                if (windowType == null) return;
                // Find existing windows only; GetWindow would create a window just to close it.
                foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(windowType))
                {
                    var pluginWindow = obj as EditorWindow;
                    if (pluginWindow != null) pluginWindow.Close();
                }
                return;
            }
        }
        catch (Exception ex)
        {
            // Closing UI is best-effort and must not turn a confirmed upload into a failure.
            status += "\n上传已成功，但插件窗口未能自动关闭：" + ex.GetBaseException().Message;
        }
    }

    private void ReleaseRequest(bool abort)
    {
        EditorApplication.update -= Poll;
        if (cancellation != null)
        {
            if (abort) cancellation.Cancel();
            cancellation.Dispose();
            cancellation = null;
        }
        request = null;
    }
}

// Version-specific adapter for the inspected Playworks 7.2.0 DLL. Never modifies plugin settings.
internal static class Playworks720BuildBridge
{
    private const string SupportedHash = "a8d1a6f5c87dbe81b53bcc310cc13fbf4cb31b62c2e7c7b05a34136f8b38421f";

    internal static bool IsWindowReady()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != "EditorScripts") continue;
            Type type = assembly.GetType("Luna.Unity.Tutorial.TutorialMediator");
            PropertyInfo ready = type == null ? null : type.GetProperty("IsLunaWindowInit");
            return ready != null && (bool)ready.GetValue(null);
        }
        return false;
    }

    internal static Assembly GetCompatiblePlugin()
    {
        Assembly plugin = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == "EditorScripts") { plugin = assembly; break; }
        if (plugin == null)
            throw new Exception("未检测到 Unity Playworks 插件。请安装或升级到 7.2.0 版本后重新打开此窗口。");
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(plugin);
        if (package == null || package.name != "com.unity.playworks.upp")
            throw new Exception("无法确认 Unity Playworks 插件版本。请通过 Package Manager 正确安装 7.2.0 版本后重新打开此窗口。");
        if (!string.Equals(package.version, "7.2.0", StringComparison.Ordinal))
            throw new Exception("当前 Unity Playworks 插件版本：" + package.version + "。\n本工具仅支持 7.2.0，请安装或升级到 7.2.0 版本后重新打开此窗口。");
        using (var file = File.OpenRead(plugin.Location))
        using (var sha = SHA256.Create())
        {
            string hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
            if (hash != SupportedHash)
                throw new Exception("当前 Unity Playworks 7.2.0 插件 DLL 与已适配版本不一致，不能使用构建上传功能。");
        }
        return plugin;
    }

    internal static bool IsBuildInProcess()
    {
        Assembly plugin = GetCompatiblePlugin();
        Type mediator = plugin.GetType("Luna.Unity.Tutorial.TutorialMediator", true);
        PropertyInfo busy = mediator.GetProperty("IsBuildInProcess", BindingFlags.Public | BindingFlags.Static);
        if (busy == null || busy.PropertyType != typeof(bool) || !busy.CanRead)
            throw new Exception("无法确认 Playworks 构建状态，已阻止手动上传。请检查插件后重试。");
        return (bool)busy.GetValue(null);
    }

    internal static MethodInfo Resolve(out object manager)
    {
        manager = null;
        Assembly plugin = GetCompatiblePlugin();
        Type mediator = plugin.GetType("Luna.Unity.Tutorial.TutorialMediator", true);
        if (!(bool)mediator.GetProperty("IsLunaWindowInit").GetValue(null))
            throw new Exception("请先打开 Playworks 插件窗口并完成构建配置，然后再点击构建并上传。");
        if ((bool)mediator.GetProperty("IsBuildInProcess").GetValue(null))
            throw new Exception("Playworks 已在构建，请等待结束后再操作。");
        Type type = plugin.GetType("\u1680.\u1680\u2027", true);
        Type sourceType = plugin.GetType("\u1680.\u1680\u2025", true);
        FieldInfo instance = type.GetField("\u2010\u202e", BindingFlags.Public | BindingFlags.Static);
        MethodInfo method = type.GetMethod("\u00a0", BindingFlags.NonPublic | BindingFlags.Instance,
            null, new[] { typeof(string), sourceType }, null);
        if (instance == null || method == null || method.ReturnType != typeof(Task<bool>))
            throw new Exception("Playworks 构建接口签名不匹配，未启动构建。");
        manager = instance.GetValue(null);
        if (manager == null) throw new Exception("Playworks 构建管理器尚未初始化。");
        return method;
    }

    internal static Dictionary<string, long> Snapshot(string folder)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        if (Directory.Exists(folder))
            foreach (string path in Directory.GetFiles(folder, "*_build.zip", SearchOption.TopDirectoryOnly))
                result[path] = File.GetLastWriteTimeUtc(path).Ticks;
        return result;
    }

    internal static string FindFreshZip(string folder, Dictionary<string, long> before, DateTime started)
    {
        var candidates = new List<string>();
        foreach (var item in Snapshot(folder))
        {
            long oldTime;
            if (item.Value >= started.AddSeconds(-2).Ticks &&
                (!before.TryGetValue(item.Key, out oldTime) || item.Value != oldTime))
                candidates.Add(item.Key);
        }
        if (candidates.Count != 1)
            throw new Exception("没有找到唯一的本次构建 ZIP，已阻止上传旧包。请查看 " + folder);
        string path = candidates[0];
        using (var file = File.OpenRead(path))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
        {
            if (archive.GetEntry("index.html") == null || archive.GetEntry("playground.json") == null)
                throw new Exception("构建 ZIP 缺少 index.html 或 playground.json，未上传。");
            // Decompress every entry to detect incomplete/corrupt output before sending it.
            foreach (var entry in archive.Entries)
                if (!string.IsNullOrEmpty(entry.Name))
                    using (var stream = entry.Open()) stream.CopyTo(Stream.Null);
        }
        return path;
    }
}
#endif
