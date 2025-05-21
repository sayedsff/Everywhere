using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Plugins.Web;

namespace Everywhere.Assistant;

public class BoChaConnector : IWebSearchEngineConnector
{
    private readonly ILogger logger;
    private readonly HttpClient httpClient;
    private readonly Uri? uri;
    private const string DefaultUri = "https://api.bochaai.com/v1/web-search";

    /// <summary>
    /// Initializes a new instance of the <see cref="BoChaConnector"/> class.
    /// </summary>
    /// <param name="apiKey">The API key to authenticate the connector.</param>
    /// <param name="uri">The URI of the Bing Search instance. Defaults to "https://api.bing.microsoft.com/v7.0/search?q".</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public BoChaConnector(string apiKey, Uri? uri = null, ILoggerFactory? loggerFactory = null) :
        this(apiKey, new HttpClient(), uri, loggerFactory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BoChaConnector"/> class.
    /// </summary>
    /// <param name="apiKey">The API key to authenticate the connector.</param>
    /// <param name="httpClient">The HTTP client to use for making requests.</param>
    /// <param name="uri">The URI of the BoCha Search instance. Defaults to "https://api.bochaai.com/v1/web-search".</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public BoChaConnector(string apiKey, HttpClient httpClient, Uri? uri = null, ILoggerFactory? loggerFactory = null)
    {
        logger = loggerFactory?.CreateLogger(typeof(BoChaConnector)) ?? NullLogger.Instance;
        this.httpClient = httpClient;
        this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        this.uri = uri ?? new Uri(DefaultUri);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> SearchAsync<T>(string query, int count = 1, int offset = 0, CancellationToken cancellationToken = default)
    {
        if (count is <= 0 or >= 50)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, $"{nameof(count)} value must be greater than 0 and less than 50.");
        }

        logger.LogDebug("Sending request: {Uri}", uri);

        using var responseMessage = await httpClient.PostAsync(
            uri,
            JsonContent.Create(new
            {
                query,
                count,
                summary = true
            }),
            cancellationToken).ConfigureAwait(false);

        logger.LogDebug("Response received: {StatusCode}", responseMessage.StatusCode);

        var json = await responseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // Sensitive data, logging as trace, disabled by default
        logger.LogTrace("Response content received: {Data}", json);

        var response = JsonSerializer.Deserialize<Response>(json);
        if (response is not { Data: { } data })
        {
            throw new HttpRequestException(response?.Message);
        }

        List<T>? returnValues = null;
        if (data?.WebPages?.Value is not null)
        {
            if (typeof(T) == typeof(string))
            {
                var results = data.WebPages?.Value;
                returnValues = results?.Select(x => x.Snippet).ToList() as List<T>;
            }
            else if (typeof(T) == typeof(WebPage))
            {
                List<WebPage> webPages = [.. data.WebPages.Value];
                returnValues = webPages.Take(count).ToList() as List<T>;
            }
            else
            {
                throw new NotSupportedException($"Type {typeof(T)} is not supported.");
            }
        }

        return
            returnValues is null ? [] :
            returnValues.Count <= count ? returnValues :
            returnValues.Take(count);
    }

    private class Response
    {
        [JsonPropertyName("code")]
        public int Code { get; init; }

        [JsonPropertyName("msg")]
        public string? Message { get; init; }

        [JsonPropertyName("data")]
        public WebSearchResponse? Data { get; init; }
    }
}