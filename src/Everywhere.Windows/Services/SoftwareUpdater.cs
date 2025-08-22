using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Enums;
using Everywhere.Interfaces;
using Everywhere.Utilities;
using Microsoft.Extensions.Logging;

namespace Everywhere.Windows.Services;

public sealed partial class SoftwareUpdater(
    INativeHelper nativeHelper,
    IRuntimeConstantProvider runtimeConstantProvider,
    ILogger<SoftwareUpdater> logger
) : ObservableObject, ISoftwareUpdater
{
    // GitHub API and download URLs
    private const string GitHubApiUrl = "https://api.github.com/repos/DearVa/Everywhere/releases/latest";

    // Proxies for robustness
    private static readonly string[] GitHubProxies = ["https://gh-proxy.com/"];

    private readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "libcurl/7.64.1 r-curl/4.3.2 httr/1.4.2 EverywhereUpdater" }
        }
    };

    private PeriodicTimer? _timer;
    private Task? _updateTask;
    private Asset? _latestAsset;

    public Version CurrentVersion { get; } = typeof(SoftwareUpdater).Assembly.GetName().Version ?? new Version(0, 0, 0);

    [ObservableProperty] public partial DateTimeOffset? LastCheckTime { get; private set; }

    [ObservableProperty] public partial Version? LatestVersion { get; private set; }

    public void RunAutomaticCheckInBackground(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        _timer = new PeriodicTimer(interval);
        cancellationToken.Register(Stop);

        Task.Run(
            async () =>
            {
                await CheckForUpdatesAsync(cancellationToken); // check immediately on start

                while (await _timer.WaitForNextTickAsync(cancellationToken))
                {
                    await CheckForUpdatesAsync(cancellationToken);
                }
            },
            cancellationToken);

        void Stop()
        {
            DisposeCollector.DisposeToDefault(ref _timer);
        }
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_updateTask is not null) return;

            var response = await GetResponseAsync(GitHubApiUrl);
            var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = jsonDoc.RootElement;

            var latestTag = root.GetProperty("tag_name").GetString();
            if (latestTag is null) return;

            var versionString = latestTag.StartsWith('v') ? latestTag[1..] : latestTag;
            if (!Version.TryParse(versionString, out var latestVersion))
            {
                logger.LogWarning("Could not parse version from tag: {Tag}", latestTag);
                return;
            }

            var assets = root.GetProperty("assets").Deserialize<List<Asset>>();
            var isInstalled = nativeHelper.IsInstalled;
            _latestAsset = assets?.FirstOrDefault(
                a => isInstalled ?
                    a.Name.EndsWith($"-Windows-x64-Setup-v{versionString}.exe", StringComparison.OrdinalIgnoreCase) :
                    a.Name.EndsWith($"-Windows-x64-v{versionString}.zip", StringComparison.OrdinalIgnoreCase));

            LatestVersion = latestVersion > CurrentVersion ? latestVersion : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check for updates.");
            LatestVersion = null;
        }

        LastCheckTime = DateTimeOffset.UtcNow;
    }

    public async Task PerformUpdateAsync(IProgress<double> progress)
    {
        if (_updateTask is not null)
        {
            await _updateTask;
            return;
        }

        if (LatestVersion is null || LatestVersion <= CurrentVersion || _latestAsset is not { } asset)
        {
            logger.LogInformation("No new version available to update.");
            return;
        }

        _updateTask = Task.Run(async () =>
        {
            try
            {
                var assetPath = await DownloadAssetAsync(asset, progress);

                if (assetPath.EndsWith(".exe"))
                {
                    UpdateViaInstaller(assetPath);
                }
                else
                {
                    await UpdateViaPortableAsync(assetPath);
                }
            }
            finally
            {
                _updateTask = null;
            }
        });

        await _updateTask;
    }

    private async Task<string> DownloadAssetAsync(Asset asset, IProgress<double> progress)
    {
        var installPath = runtimeConstantProvider.EnsureWritableDataFolderPath("updates");
        var assetDownloadPath = Path.Combine(installPath, asset.Name);

        var fileInfo = new FileInfo(assetDownloadPath);
        if (fileInfo.Exists)
        {
            if (fileInfo.Length == asset.Size && string.Equals(await HashFileAsync(), asset.Digest, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Asset {AssetName} already exists and is valid, skipping download.", asset.Name);
                progress.Report(1.0);
                return assetDownloadPath;
            }

            logger.LogInformation("Asset {AssetName} exists but is invalid, redownloading.", asset.Name);
        }

        var response = await GetResponseAsync(asset.DownloadUrl);
        await using var fs = new FileStream(assetDownloadPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        var totalBytes = response.Content.Headers.ContentLength ?? asset.Size;
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        var totalBytesRead = 0L;
        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalBytesRead += bytesRead;
            progress.Report((double)totalBytesRead / totalBytes);
        }

        fs.Position = 0;
        if (!string.Equals("sha256:" + Convert.ToHexString(await SHA256.HashDataAsync(fs)), asset.Digest, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Downloaded asset {asset.Name} hash does not match expected digest.");
        }

        return assetDownloadPath;

        async Task<string> HashFileAsync()
        {
            await using var fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sha256 = await SHA256.HashDataAsync(fileStream);
            return "sha256:" + Convert.ToHexString(sha256);
        }
    }

    private static void UpdateViaInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
        Environment.Exit(0);
    }

    private async static Task UpdateViaPortableAsync(string zipPath)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "update.bat");
        var exeLocation = Assembly.GetExecutingAssembly().Location;
        var currentDir = Path.GetDirectoryName(exeLocation)!;

        var scriptContent =
            $"""
             @echo off
             ECHO Waiting for the application to close...
             TASKKILL /IM "{Path.GetFileName(exeLocation)}" /F >nul 2>nul
             timeout /t 2 /nobreak >nul
             ECHO Backing up old version...
             ren "{currentDir}" "{Path.GetFileName(currentDir)}_old"
             ECHO Unpacking new version...
             powershell -Command "Expand-Archive -LiteralPath '{zipPath}' -DestinationPath '{currentDir}' -Force"
             IF %ERRORLEVEL% NEQ 0 (
                 ECHO Unpacking failed, restoring old version...
                 ren "{Path.Combine(Path.GetDirectoryName(currentDir)!, Path.GetFileName(currentDir) + "_old")}" "{Path.GetFileName(currentDir)}"
                 GOTO END
             )
             ECHO Cleaning up old files...
             rd /s /q "{Path.Combine(Path.GetDirectoryName(currentDir)!, Path.GetFileName(currentDir) + "_old")}"
             ECHO Starting new version...
             start "" "{exeLocation}"
             :END
             del "{scriptPath}"
             """;

        await File.WriteAllTextAsync(scriptPath, scriptContent);

        Process.Start(new ProcessStartInfo(scriptPath) { UseShellExecute = true, Verb = "runas" });
        Environment.Exit(0);
    }

    private async Task<HttpResponseMessage> GetResponseAsync(string url)
    {
        ObjectDisposedException.ThrowIf(_httpClient is null, this);

        try
        {
            return await GetResponseImplAsync(url);
        }
        catch (Exception ex1)
        {
            logger.LogWarning(ex1, "Direct request failed, trying GitHub proxies.");
            foreach (var proxy in GitHubProxies)
            {
                try
                {
                    return await GetResponseImplAsync(proxy + url);
                }
                catch (Exception ex2)
                {
                    logger.LogWarning(ex2, "Request via proxy {Proxy} failed.", proxy);
                }
            }
        }

        throw new Exception("All attempts to get a valid response failed.");

        async Task<HttpResponseMessage> GetResponseImplAsync(string actualUrl)
        {
            var response = await _httpClient.GetAsync(actualUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return response;
        }
    }

    [Serializable]
    private record Asset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("digest")] string Digest,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("browser_download_url")] string DownloadUrl);
}