using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Serialization;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZLinq;

namespace Everywhere.Initialization;

/// <summary>
/// Initializes the settings with dynamic defined list.
/// Also initializes an observer that automatically saves the settings when changed.
/// </summary>
public class SettingsInitializer : IAsyncInitializer
{
    public AsyncInitializerPriority Priority => AsyncInitializerPriority.Settings;

    private readonly Dictionary<string, object?> _saveBuffer = new();
    private readonly DebounceExecutor<Dictionary<string, object?>> _saveDebounceExecutor;
    private readonly Settings _settings;

    public SettingsInitializer(Settings settings, [FromKeyedServices(typeof(Settings))] IConfiguration configuration)
    {
        _settings = settings;

        _saveDebounceExecutor = new DebounceExecutor<Dictionary<string, object?>>(
            () => _saveBuffer,
            saveBuffer =>
            {
                lock (saveBuffer)
                {
                    if (saveBuffer.Count == 0) return;
                    foreach (var (key, value) in saveBuffer) configuration.Set(key, value);
                    saveBuffer.Clear();
                }
            },
            TimeSpan.FromSeconds(0.5));
    }

    public Task InitializeAsync()
    {
        InitializeObserver();
        InitializeSearchEngineProviders();

        return Task.CompletedTask;
    }

    private void InitializeObserver()
    {
        new ObjectObserver(HandleSettingsChanges).Observe(_settings);

        void HandleSettingsChanges(in ObjectObserverChangedEventArgs e)
        {
            lock (_saveBuffer) _saveBuffer[e.Path] = e.Value;
            _saveDebounceExecutor.Trigger();
        }
    }

    private void InitializeSearchEngineProviders()
    {
        var webSearchEngineSettings = _settings.Plugin.WebSearchEngine;

        // Remove duplicate search engine providers by Id
        webSearchEngineSettings.WebSearchEngineProviders.Reset(
            webSearchEngineSettings.WebSearchEngineProviders.AsValueEnumerable().Where(p => p.Id is { Length: > 0 }).DistinctBy(p => p.Id).ToList());

        ApplySearchEngineProviders(
            [
                new WebSearchEngineProvider
                {
                    Id = "google",
                    DisplayName = "Google",
                    EndPoint = "https://customsearch.googleapis.com"
                },
                new WebSearchEngineProvider
                {
                    Id = "tavily",
                    DisplayName = "Tavily",
                    EndPoint = "https://api.tavily.com"
                },
                new WebSearchEngineProvider
                {
                    Id = "brave",
                    DisplayName = "Brave",
                    EndPoint = "https://api.search.brave.com/res/v1/web/search"
                },
                new WebSearchEngineProvider
                {
                    Id = "bocha",
                    DisplayName = "Bocha",
                    EndPoint = "https://api.bochaai.com/v1/web-search"
                },
                new WebSearchEngineProvider
                {
                    Id = "jina",
                    DisplayName = "Jina",
                    EndPoint = "https://s.jina.ai"
                },
                new WebSearchEngineProvider
                {
                    Id = "searxng",
                    DisplayName = "SearXNG",
                    EndPoint = "https://searxng.example.com/search"
                },
            ],
            webSearchEngineSettings.WebSearchEngineProviders);

        webSearchEngineSettings.SelectedWebSearchEngineProviderId ??= webSearchEngineSettings.WebSearchEngineProviders.FirstOrDefault()?.Id;
    }

    private static void ApplySearchEngineProviders(IList<WebSearchEngineProvider> srcList, ObservableCollection<WebSearchEngineProvider> dstList)
    {
        var propertyCache = new Dictionary<Type, IReadOnlyList<PropertyInfo>>();

        foreach (var src in srcList)
        {
            var dst = dstList.FirstOrDefault(p => p.Id == src.Id);
            if (dst is null)
            {
                dstList.Add(src);
            }
            else
            {
                ApplyProperties(src, dst, propertyCache);
            }
        }

        for (var i = dstList.Count - 1; i >= 0; i--)
        {
            var dst = dstList[i];
            if (srcList.All(p => p.Id != dst.Id))
            {
                // Remove search engine provider if it does not exist in the source list
                dstList.RemoveAt(i);
            }
        }
    }

    private static void ApplyProperties(object src, object dst, Dictionary<Type, IReadOnlyList<PropertyInfo>> propertyCache)
    {
        var srcType = src.GetType();
        var dstType = dst.GetType();

        if (srcType != dstType) throw new InvalidOperationException("Source and destination types must be the same.");

        if (!propertyCache.TryGetValue(srcType, out var properties))
        {
            properties = srcType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p is { CanRead: true, CanWrite: true })
                .Where(p => p.GetCustomAttribute<IgnoreDataMemberAttribute>() is null)
                .ToList();
            propertyCache[srcType] = properties;
        }

        foreach (var property in properties)
        {
            var srcValue = property.GetValue(src);
            if (srcValue is null)
            {
                property.SetValue(dst, null);
            }
            else if (IsSimpleType(property.PropertyType))
            {
                property.SetValue(dst, srcValue);
            }
            else
            {
                var dstValue = property.GetValue(dst);
                if (dstValue is null)
                {
                    property.SetValue(dst, srcValue);
                }
                else
                {
                    ApplyProperties(srcValue, dstValue, propertyCache);
                }
            }
        }

        static bool IsSimpleType(Type type) => type.IsPrimitive || type.IsEnum || type.IsValueType || type == typeof(string);
    }
}