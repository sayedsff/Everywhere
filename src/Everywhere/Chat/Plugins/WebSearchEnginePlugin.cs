using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Utilities;
using Google.Apis.Services;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Brave;
using Microsoft.SemanticKernel.Plugins.Web.Google;
using PuppeteerSharp;
using Tavily;
using ZLinq;

namespace Everywhere.Chat.Plugins;

public partial class WebSearchEnginePlugin : BuiltInChatPlugin
{
    public override LucideIconKind? Icon => LucideIconKind.Globe;

    public override IReadOnlyList<SettingsItem>? SettingsItems { get; }

    private readonly WebSearchEngineSettings _webSearchEngineSettings;
    private readonly IRuntimeConstantProvider _runtimeConstantProvider;
    private readonly IWatchdogManager _watchdogManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<WebSearchEnginePlugin> _logger;
    private readonly DebounceExecutor<WebSearchEnginePlugin> _browserDisposer;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly SemaphoreSlim _browserLock = new(1, 1);

    private IWebSearchEngineConnector? _connector;
    private IBrowser? _browser;
    private Process? _browserProcess;

    public WebSearchEnginePlugin(
        Settings settings,
        IRuntimeConstantProvider runtimeConstantProvider,
        IWatchdogManager watchdogManager,
        ILoggerFactory loggerFactory) : base("WebSearchEngine")
    {
        _webSearchEngineSettings = settings.Plugin.WebSearchEngine;
        _runtimeConstantProvider = runtimeConstantProvider;
        _watchdogManager = watchdogManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<WebSearchEnginePlugin>();
        _browserDisposer = new DebounceExecutor<WebSearchEnginePlugin>(
            () => this,
            static that =>
            {
                that._browserLock.Wait();
                try
                {
                    that._logger.LogDebug("Disposing browser after inactivity.");

                    if (that._browser is null) return;
                    that._browser.CloseAsync();
                    DisposeCollector.DisposeToDefault(ref that._browser);

                    if (that._browserProcess is { HasExited: false })
                    {
                        that._watchdogManager.UnregisterProcessAsync(that._browserProcess.Id);

                        // Kill existing browser process if any
                        that._browserProcess.Kill();
                        that._browserProcess = null;
                    }
                }
                finally
                {
                    that._browserLock.Release();
                }
            },
            TimeSpan.FromMinutes(5)); // Dispose browser after 5 minutes of inactivity

        _functions.Add(
            new AnonymousChatFunction(
                WebSearchAsync,
                ChatFunctionPermissions.NetworkAccess));
        _functions.Add(
            new AnonymousChatFunction(
                WebSnapshotAsync,
                ChatFunctionPermissions.NetworkAccess));

        SettingsItems = Configuration.SettingsItems.CreateForObject(_webSearchEngineSettings, "Plugin_WebSearchEngine");

        new ObjectObserver(HandleSettingsChanged).Observe(_webSearchEngineSettings);
    }

    private void HandleSettingsChanged(in ObjectObserverChangedEventArgs e)
    {
        // Invalidate the connector when settings change
        _connector = null;
    }

    [MemberNotNull(nameof(_connector))]
    private void EnsureConnector()
    {
        if (_connector is not null) return;

        _logger.LogDebug("Ensuring web search engine connector is initialized.");

        if (_webSearchEngineSettings.SelectedWebSearchEngineProvider is not { } provider)
        {
            throw new InvalidOperationException("Web search engine provider is not selected.");
        }

        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new InvalidOperationException("API key is not set.");
        }

        if (!Uri.TryCreate(provider.EndPoint.ActualValue, UriKind.Absolute, out var uri) ||
            uri.Scheme is not "http" and not "https")
        {
            throw new InvalidOperationException("EndPoint is not a valid absolute URI.");
        }

        _connector = provider.Id.ToLower() switch
        {
            "google" => new GoogleConnector(
                new BaseClientService.Initializer
                {
                    ApiKey = provider.ApiKey,
                    BaseUri = uri.AbsoluteUri,
                },
                provider.SearchEngineId ?? throw new InvalidOperationException("Search Engine ID is not set."),
                _loggerFactory),
            "tavily" => new TavilyConnector(provider.ApiKey, uri, _loggerFactory),
            "brave" => new BraveConnector(provider.ApiKey, uri, _loggerFactory),
            "bocha" => new BoChaConnector(provider.ApiKey, uri, _loggerFactory),
            _ => throw new NotSupportedException($"Web search engine provider '{provider.Id}' is not supported.")
        };
    }

    /// <summary>
    /// Performs a web search using the provided query, count, and offset.
    /// </summary>
    /// <param name="query">The text to search for.</param>
    /// <param name="count">The number of results to return. Default is 1.</param>
    /// <param name="offset">The number of results to skip. Default is 0.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The value of the TResult parameter contains the search results as a string.</returns>
    /// <remarks>
    /// This method is marked as "unsafe." The usage of JavaScriptEncoder.UnsafeRelaxedJsonEscaping may introduce security risks.
    /// Only use this method if you are aware of the potential risks and have validated the input to prevent security vulnerabilities.
    /// </remarks>
    [KernelFunction("web_search")]
    [Description("Perform a web search. Invoke this multiple times with different queries to get more results.")]
    private async Task<string> WebSearchAsync(
        [Description("Search query")] string query,
        [Description("Number of results")] int count = 10,
        [Description("Number of results to skip")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Performing web search with query: {Query}, count: {Count}, offset: {Offset}", query, count, offset);

        EnsureConnector();

        var results = await _connector.SearchAsync<WebPage>(query, count, offset, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(results, _jsonSerializerOptions);
    }

    [KernelFunction("web_snapshot")]
    [Description("Snapshot accessibility of a web page via Puppeteer, returning a json of the page content and metadata.")]
    private async Task<string> WebSnapshotAsync([Description("Web page URL to snapshot")] string url, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Taking web snapshot...");

        _browserDisposer.Cancel();
        await _browserLock.WaitAsync(cancellationToken);

        try
        {
            if (_browser is null)
            {
                try
                {
                    if (_browserProcess is { HasExited: false })
                    {
                        // Kill existing browser process if any
                        _browserProcess.Kill();
                        _browserProcess = null;
                    }

                    var cachePath = _runtimeConstantProvider.EnsureWritableDataFolderPath("cache/plugins/puppeteer");
                    var browserFetcher = new BrowserFetcher
                    {
                        CacheDir = cachePath,
                        Browser = SupportedBrowser.Chromium
                    };
                    const string buildId = "1499281";
                    var executablePath = browserFetcher.GetExecutablePath(buildId);
                    if (!File.Exists(executablePath))
                    {
                        _logger.LogDebug("Downloading Puppeteer browser to cache directory: {CachePath}", cachePath);
                        browserFetcher.BaseUrl =
                            await TestUrlConnectionAsync("https://storage.googleapis.com/chromium-browser-snapshots") ??
                            await TestUrlConnectionAsync("https://cdn.npmmirror.com/binaries/chromium-browser-snapshots") ??
                            throw new HttpRequestException("Failed to connect to Puppeteer browser download URL.");
                        await browserFetcher.DownloadAsync(buildId);
                    }

                    _logger.LogDebug("Using Puppeteer browser executable at: {ExecutablePath}", executablePath);
                    var launcher = new Launcher(_loggerFactory);
                    _browser = await launcher.LaunchAsync(
                        new LaunchOptions
                        {
                            ExecutablePath = executablePath,
                            Browser = SupportedBrowser.Chromium,
                            Headless = true
                        });

                    _browserProcess = launcher.Process.Process;
                    await _watchdogManager.RegisterProcessAsync(_browserProcess.Id);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Failed to download or launch Puppeteer browser.", e);
                }
            }

            await using var page = await _browser.NewPageAsync();
            try
            {
                await page.SetUserAgentAsync(
#if IsWindows
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36"
#elif IsOSX
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36"
#else
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36"
#endif
                );
                await page.GoToAsync(url, waitUntil: [WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle2]);

                var node = await page.Accessibility.SnapshotAsync();
                var json = JsonSerializer.Serialize(
                    new
                    {
                        node.Name,
                        Elements = node.Children.Select(n => new
                        {
                            n.Name,
                            n.Description,
                            n.Role
                        })
                    },
                    _jsonSerializerOptions);

                return json;
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        finally
        {
            _browserDisposer.Trigger();
            _browserLock.Release();
        }

        async ValueTask<string?> TestUrlConnectionAsync(string testUrl)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10); // Set a reasonable timeout for the test connection
            try
            {
                using var response = await client.GetAsync(testUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return testUrl;
                }

                _logger.LogWarning("Failed to connect to URL: {Url}, Status Code: {StatusCode}", testUrl, response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to URL: {Url}", testUrl);
                return null;
            }
        }
    }

    private class TavilyConnector(string apiKey, Uri? uri, ILoggerFactory? loggerFactory) : IWebSearchEngineConnector
    {
        private readonly TavilyClient _tavilyClient = new(baseUri: uri);
        private readonly ILogger _logger = loggerFactory?.CreateLogger(typeof(TavilyConnector)) ?? NullLogger.Instance;

        public async Task<IEnumerable<T>> SearchAsync<T>(string query, int count = 1, int offset = 0, CancellationToken cancellationToken = default)
        {
            if (count is <= 0 or >= 50)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, $"{nameof(count)} value must be greater than 0 and less than 50.");
            }

            _logger.LogDebug("Sending request");

            var response = await _tavilyClient.SearchAsync(apiKey: apiKey, query, maxResults: count, cancellationToken: cancellationToken);

            _logger.LogDebug("Response received");

            List<T>? returnValues;
            if (typeof(T) == typeof(string))
            {
                returnValues = response.Results
                    .AsValueEnumerable()
                    .Take(count)
                    .Select(x => x.Content)
                    .ToList() as List<T>;
            }
            else if (typeof(T) == typeof(WebPage))
            {
                returnValues = response.Results
                    .AsValueEnumerable()
                    .Take(count)
                    .Select(x => new WebPage
                    {
                        Name = x.Title,
                        Url = x.Url,
                        Snippet = x.Content
                    })
                    .ToList() as List<T>;
            }
            else
            {
                throw new NotSupportedException($"Type {typeof(T)} is not supported.");
            }

            return returnValues ?? [];
        }
    }

    private partial class BoChaConnector : IWebSearchEngineConnector
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly Uri? _uri;
        private const string DefaultUri = "https://api.bochaai.com/v1/web-search";

        /// <summary>
        /// Initializes a new instance of the <see cref="BoChaConnector"/> class.
        /// </summary>
        /// <param name="apiKey">The API key to authenticate the connector.</param>
        /// <param name="uri">The URI of the Bing Search instance. Defaults to "https://api.bing.microsoft.com/v7.0/search?q".</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
        public BoChaConnector(string apiKey, Uri? uri, ILoggerFactory? loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger(typeof(BoChaConnector)) ?? NullLogger.Instance;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _uri = uri ?? new Uri(DefaultUri);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<T>> SearchAsync<T>(string query, int count = 1, int offset = 0, CancellationToken cancellationToken = default)
        {
            if (count is <= 0 or >= 50)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, $"{nameof(count)} value must be greater than 0 and less than 50.");
            }

            _logger.LogDebug("Sending request: {Uri}", _uri);

            using var responseMessage = await _httpClient.PostAsync(
                _uri,
                JsonContent.Create(
                    new
                    {
                        query,
                        count,
                        summary = true
                    }),
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Response received: {StatusCode}", responseMessage.StatusCode);

            var json = await responseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // Sensitive data, logging as trace, disabled by default
            _logger.LogTrace("Response content received: {Data}", json);

            var response = JsonSerializer.Deserialize(json, ResponseJsonSerializationContext.Default.Response);
            if (response is not { Data: { } data })
            {
                throw new HttpRequestException(response?.Message);
            }

            if (data?.WebPages?.Value is null) return [];

            List<T>? returnValues;
            if (typeof(T) == typeof(string))
            {
                returnValues = data.WebPages.Value
                    .AsValueEnumerable()
                    .Take(count)
                    .Select(x => x.Summary)
                    .ToList() as List<T>;
            }
            else if (typeof(T) == typeof(WebPage))
            {
                returnValues = data.WebPages.Value
                    .AsValueEnumerable()
                    .Take(count)
                    .Select(x => new WebPage
                    {
                        Name = x.Name,
                        Url = x.Url,
                        Snippet = x.Summary ?? x.Snippet
                    })
                    .ToList() as List<T>;
            }
            else
            {
                throw new NotSupportedException($"Type {typeof(T)} is not supported.");
            }

            return returnValues ?? [];
        }

        [JsonSerializable(typeof(Response))]
        private partial class ResponseJsonSerializationContext : JsonSerializerContext;

        private class Response
        {
            [JsonPropertyName("code")]
            public int Code { get; init; }

            [JsonPropertyName("msg")]
            public string? Message { get; init; }

            [JsonPropertyName("data")]
            public BoChaWebSearchResponse? Data { get; init; }
        }

        private sealed class BoChaWebSearchResponse
        {
            [JsonPropertyName("webPages")]
            public BoChaWebPages? WebPages { get; set; }
        }

        private sealed class BoChaWebPages
        {
            /// <summary>
            /// a nullable WebPage array object containing the Web Search API response data.
            /// </summary>
            [JsonPropertyName("value")]
            public BoChaWebPage[]? Value { get; set; }
        }

        private sealed class BoChaWebPage
        {
            /// <summary>
            /// The name of the result.
            /// </summary>
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// The URL of the result.
            /// </summary>
            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;

            /// <summary>
            /// The result snippet.
            /// </summary>
            [JsonPropertyName("snippet")]
            public string Snippet { get; set; } = string.Empty;

            /// <summary>
            /// The result snippet.
            /// </summary>
            [JsonPropertyName("summary")]
            public string? Summary { get; set; }
        }
    }
}