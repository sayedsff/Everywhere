using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Plugins.Web;
using ZLinq;
using ZLinq.Linq;

namespace Everywhere.Chat;

public partial class BoChaConnector : IWebSearchEngineConnector
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