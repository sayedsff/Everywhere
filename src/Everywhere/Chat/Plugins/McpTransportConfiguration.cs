using System.Text.Json.Serialization;

namespace Everywhere.Chat.Plugins;

[JsonPolymorphic]
[JsonDerivedType(typeof(StdioMcpTransportConfiguration), "stdio")]
[JsonDerivedType(typeof(SseMcpTransportConfiguration), "sse")]
public abstract record McpTransportConfiguration;

public record StdioMcpTransportConfiguration(
    string Command,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null) : McpTransportConfiguration;

public record SseMcpTransportConfiguration(
    string Url,
    IReadOnlyDictionary<string, string>? Headers = null) : McpTransportConfiguration;