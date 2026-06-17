using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrayonShinchanNotification;

public class AutoUpdater
{
    // TODO: 替换为你的 GitHub 仓库信息
    private const string GitHubOwner = "felixlovejj";
    private const string GitHubRepo = "Crayon-Shin-chan-message-notification";

    private static readonly HttpClient _httpClient = new();
    private readonly string _currentVersion;
    private readonly string _appDirectory;
    private readonly string _tempDirectory;

    public event Action<string>? OnUpdateAvailable;
    public event Action<string>? OnUpdateProgress;
    public event Action<string>? OnUpdateError;
    public event Action? OnUpdateComplete;

    public AutoUpdater()
    {
        _currentVersion = GetCurrentVersion();
        _appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _tempDirectory = Path.Combine(Path.GetTempPath(), "CrayonShinchanUpdater");
    }

    private string GetCurrentVersion()
    {
        var versionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.json");
        if (File.Exists(versionFile))
        {
            try
            {
                var json = File.ReadAllText(versionFile);
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);
                return versionInfo?.Version ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }
        return "1.0.0";
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            OnUpdateProgress?.Invoke("正在检查更新...");

            var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CrayonShinchanNotification");

            var response = await _httpClient.GetStringAsync(url);
            var release = JsonSerializer.Deserialize<GitHubRelease>(response);

            if (release == null || string.IsNullOrEmpty(release.TagName))
            {
                OnUpdateError?.Invoke("无法获取版本信息");
                return;
            }

            var latestVersion = release.TagName.TrimStart('v');
            if (IsNewerVersion(latestVersion, _currentVersion))
            {
                OnUpdateAvailable?.Invoke(latestVersion);
                await DownloadAndInstallAsync(release, latestVersion);
            }
            else
            {
                OnUpdateProgress?.Invoke("已是最新版本");
            }
        }
        catch (HttpRequestException ex)
        {
            OnUpdateError?.Invoke($"网络错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            OnUpdateError?.Invoke($"检查更新失败: {ex.Message}");
        }
    }

    private bool IsNewerVersion(string remoteVersion, string localVersion)
    {
        try
        {
            var remote = Version.Parse(remoteVersion);
            var local = Version.Parse(localVersion);
            return remote > local;
        }
        catch
        {
            return false;
        }
    }

    private async Task DownloadAndInstallAsync(GitHubRelease release, string newVersion)
    {
        try
        {
            // 找到 zip 文件
            var zipAsset = release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".zip"));
            if (zipAsset == null)
            {
                OnUpdateError?.Invoke("未找到更新包");
                return;
            }

            OnUpdateProgress?.Invoke($"正在下载 v{newVersion}...");

            // 下载到临时目录
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, true);
            Directory.CreateDirectory(_tempDirectory);

            var zipPath = Path.Combine(_tempDirectory, "update.zip");
            var extractPath = Path.Combine(_tempDirectory, "extracted");

            await DownloadFileAsync(zipAsset.BrowserDownloadUrl, zipPath);

            OnUpdateProgress?.Invoke("正在解压...");
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            // 创建更新脚本
            CreateUpdateScript(extractPath, newVersion);

            OnUpdateProgress?.Invoke("正在安装更新...");

            // 执行更新脚本（会重启应用）
            StartUpdateProcess();

            OnUpdateComplete?.Invoke();
        }
        catch (Exception ex)
        {
            OnUpdateError?.Invoke($"安装失败: {ex.Message}");
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var totalBytesRead = 0L;

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalBytesRead += bytesRead;

            if (totalBytes > 0)
            {
                var progress = (int)((totalBytesRead * 100) / totalBytes);
                OnUpdateProgress?.Invoke($"下载中: {progress}%");
            }
        }
    }

    private void CreateUpdateScript(string extractPath, string newVersion)
    {
        var scriptPath = Path.Combine(_tempDirectory, "update.bat");

        // 创建批处理脚本
        var script = $@"
@echo off
timeout /t 2 /nobreak > nul
xcopy /E /Y /I ""{extractPath}\*"" ""{_appDirectory}"" 
echo {{""version"": ""{newVersion}""}} > ""{_appDirectory}\version.json""
rmdir /S /Q ""{_tempDirectory}""
start """" ""{_appDirectory}\CrayonShinchanNotification.exe""
del ""%~f0""
";
        File.WriteAllText(scriptPath, script);
    }

    private void StartUpdateProcess()
    {
        var scriptPath = Path.Combine(_tempDirectory, "update.bat");

        var startInfo = new ProcessStartInfo
        {
            FileName = scriptPath,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };

        Process.Start(startInfo);

        // 关闭当前应用
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    public string GetCurrentVersionString() => _currentVersion;
}

public class VersionInfo
{
    public string Version { get; set; } = "1.0.0";
    public string ReleaseDate { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
