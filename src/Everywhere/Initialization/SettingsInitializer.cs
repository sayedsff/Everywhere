using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Serialization;
using Everywhere.AI;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Everywhere.Initialization;

/// <summary>
/// Initializes the settings with dynamic defined list.
/// Also initializes an observer that automatically saves the settings when changed.
/// </summary>
public class SettingsInitializer : IAsyncInitializer
{
    public int Priority => 50;

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

        InitializeModelProviders();
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

    private void InitializeModelProviders()
    {
        // Remove duplicate model providers by Id
        _settings.Model.ModelProviders.Reset(_settings.Model.ModelProviders.DistinctBy(m => m.Id));

        foreach (var modelProvider in _settings.Model.ModelProviders)
        {
            // Remove duplicate model definitions by Id
            modelProvider.ModelDefinitions.Reset(modelProvider.ModelDefinitions.DistinctBy(m => m.Id));
        }

        ApplyModelProviders(
            [
                new ModelProvider
                {
                    Id = "openai",
                    DisplayName = "OpenAI",
                    Endpoint = "https://api.openai.com/v1",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/openai.svg",
                    Schema = ModelProviderSchema.OpenAI,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "gpt-5",
                            ModelId = "gpt-5",
                            DisplayName = "GPT-5",
                            MaxTokens = 400_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "gpt-5-mini",
                            ModelId = "gpt-5-mini",
                            DisplayName = "GPT-5 mini",
                            MaxTokens = 400_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "o4-mini",
                            ModelId = "o4-mini",
                            DisplayName = "o4-mini",
                            MaxTokens = 200_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "gpt-4.1",
                            ModelId = "gpt-4.1",
                            DisplayName = "GPT 4.1",
                            MaxTokens = 1_000_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                            IsDefault = true
                        },
                        new ModelDefinition
                        {
                            Id = "gpt-4.1-mini",
                            ModelId = "gpt-4.1-mini",
                            DisplayName = "GPT 4.1 mini",
                            MaxTokens = 1_000_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                        },
                        new ModelDefinition
                        {
                            Id = "gpt-4o",
                            ModelId = "gpt-4o",
                            DisplayName = "GPT-4o",
                            MaxTokens = 128_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                        },
                        new ModelDefinition
                        {
                            Id = "custom",
                            ModelId = "MODEL_ID_HERE",
                            DisplayName = "Custom Model",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = false,
                        }
                    ]
                },
                new ModelProvider
                {
                    Id = "anthropic",
                    DisplayName = "Anthropic (Claude)",
                    Endpoint = "https://api.anthropic.com",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/anthropic.svg",
                    Schema = ModelProviderSchema.Anthropic,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "claude-opus-4-1-20250805",
                            ModelId = "claude-opus-4-1-20250805",
                            DisplayName = "Claude Opus 4.1",
                            MaxTokens = 200_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "claude-opus-4-20250514",
                            ModelId = "claude-opus-4-20250514",
                            DisplayName = "Claude Opus 4",
                            MaxTokens = 200_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "claude-sonnet-4-20250514",
                            ModelId = "claude-sonnet-4-20250514",
                            DisplayName = "Claude Sonnet 4",
                            MaxTokens = 200_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "claude-3-7-sonnet-20250219",
                            ModelId = "claude-3-7-sonnet-20250219",
                            DisplayName = "Claude 3.7 Sonnet",
                            MaxTokens = 200_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                            IsDefault = true
                        },
                        new ModelDefinition
                        {
                            Id = "claude-3-5-haiku-20241022",
                            ModelId = "claude-3-5-haiku-20241022",
                            DisplayName = "Claude 3.5 Haiku",
                            MaxTokens = 200_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                        },
                        new ModelDefinition
                        {
                            Id = "custom",
                            ModelId = "MODEL_ID_HERE",
                            DisplayName = "Custom Model",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = false,
                        }
                    ]
                },
                new ModelProvider
                {
                    Id = "google",
                    DisplayName = "Google (Gemini)",
                    Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/google-color.svg",
                    Schema = ModelProviderSchema.OpenAI,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "gemini-2.5-pro",
                            ModelId = "gemini-2.5-pro",
                            DisplayName = "Gemini 2.5 Pro",
                            MaxTokens = 1_048_576,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "gemini-2.5-flash",
                            ModelId = "gemini-2.5-flash",
                            DisplayName = "Gemini 2.5 Flash",
                            MaxTokens = 1_048_576,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                            IsDefault = true
                        },
                        new ModelDefinition
                        {
                            Id = "gemini-2.5-flash-lite",
                            ModelId = "gemini-2.5-flash-lite",
                            DisplayName = "Gemini 2.5 Flash-Lite",
                            MaxTokens = 1_048_576,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "custom",
                            ModelId = "MODEL_ID_HERE",
                            DisplayName = "Custom Model",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = false,
                        }
                    ]
                },
                new ModelProvider
                {
                    Id = "deepseek",
                    DisplayName = "DeepSeek",
                    Endpoint = "https://api.deepseek.com",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/deepseek-color.svg",
                    Schema = ModelProviderSchema.OpenAI,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "deepseek-chat",
                            ModelId = "deepseek-chat",
                            DisplayName = "DeepSeek V3",
                            MaxTokens = 64_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                            IsDefault = true
                        },
                        new ModelDefinition
                        {
                            Id = "deepseek-reasoner",
                            ModelId = "deepseek-reasoner",
                            DisplayName = "DeepSeek R1",
                            MaxTokens = 64_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "custom",
                            ModelId = "MODEL_ID_HERE",
                            DisplayName = "Custom Model",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = false,
                        }
                    ]
                },
                new ModelProvider
                {
                    Id = "moonshot",
                    DisplayName = "Moonshot (Kimi)",
                    Endpoint = "https://api.moonshot.cn/v1",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/moonshot.svg",
                    Schema = ModelProviderSchema.OpenAI,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "kimi-k2-0711-preview",
                            ModelId = "kimi-k2-0711-preview",
                            DisplayName = "Kimi K2",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                        },
                        new ModelDefinition
                        {
                            Id = "kimi-latest",
                            ModelId = "kimi-latest",
                            DisplayName = "Kimi Latest",
                            MaxTokens = 128_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                            IsDefault = true
                        },
                        new ModelDefinition
                        {
                            Id = "kimi-thinking-preview",
                            ModelId = "kimi-thinking-preview",
                            DisplayName = "Kimi Thinking Preview",
                            MaxTokens = 128_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "custom",
                            ModelId = "MODEL_ID_HERE",
                            DisplayName = "Custom Model",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = false,
                        }
                    ]
                },
                new ModelProvider
                {
                    Id = "openrouter",
                    DisplayName = "OpenRouter",
                    Endpoint = "https://openrouter.ai/api/v1",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/openrouter.svg",
                    Schema = ModelProviderSchema.OpenAI,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "x-ai/grok-4-fast:free",
                            ModelId = "x-ai/grok-4-fast:free",
                            DisplayName = "xAI: Grok 4 Fast (free)",
                            MaxTokens = 2_000_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                            IsDefault = true
                        },
                        new ModelDefinition
                        {
                            Id = "anthropic/claude-sonnet-4",
                            ModelId = "anthropic/claude-sonnet-4",
                            DisplayName = "Anthropic: Claude Sonnet 4",
                            MaxTokens = 1_000_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                        },
                        new ModelDefinition
                        {
                            Id = "google/gemini-2.5-flash",
                            ModelId = "google/gemini-2.5-flash",
                            DisplayName = "Google: Gemini 2.5 Flash",
                            MaxTokens = 1_048_576,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                        },
                        new ModelDefinition
                        {
                            Id = "custom",
                            ModelId = "MODEL_ID_HERE",
                            DisplayName = "Custom Model",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = false,
                        }
                    ]
                },
                new ModelProvider
                {
                    Id = "siliconflow",
                    DisplayName = "SiliconCloud (SiliconFlow)",
                    Endpoint = "https://api.siliconflow.cn/v1",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/siliconcloud-color.svg",
                    Schema = ModelProviderSchema.OpenAI,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "Qwen/Qwen3-8B",
                            ModelId = "Qwen/Qwen3-8B",
                            DisplayName = "Qwen3-8B (free)",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                            IsDefault = true
                        },
                        new ModelDefinition
                        {
                            Id = "deepseek-ai/DeepSeek-V3.1",
                            ModelId = "deepseek-ai/DeepSeek-V3.1",
                            DisplayName = "DeepSeek-V3.1",
                            MaxTokens = 160_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = false,
                        },
                        new ModelDefinition
                        {
                            Id = "custom",
                            ModelId = "MODEL_ID_HERE",
                            DisplayName = "Custom Model",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = false,
                        }
                    ]
                },
                new ModelProvider
                {
                    Id = "ollama",
                    DisplayName = "Ollama",
                    Endpoint = "http://127.0.0.1:11434",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/ollama.svg",
                    Schema = ModelProviderSchema.Ollama,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "gpt-oss:20b",
                            ModelId = "gpt-oss:20b",
                            DisplayName = "GPT-OSS 20B",
                            MaxTokens = 32_768,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "deepseek-r1:8b",
                            ModelId = "deepseek-r1:8b",
                            DisplayName = "DeepSeek R1 7B",
                            MaxTokens = 65_536,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = true,
                            IsDefault = true
                        },
                        new ModelDefinition
                        {
                            Id = "qwen3:8b",
                            ModelId = "qwen3:8b",
                            DisplayName = "Qwen 3 8B",
                            MaxTokens = 64_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                        },
                        new ModelDefinition
                        {
                            Id = "custom",
                            ModelId = "MODEL_ID_HERE",
                            DisplayName = "Custom Model",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = false,
                        }
                    ]
                },
                new ModelProvider
                {
                    Id = "custom",
                    DisplayName = "Custom Model Provider",
                    Endpoint = "ENDPOINT_HERE",
                    Schema = ModelProviderSchema.OpenAI,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "custom",
                            ModelId = "MODEL_ID_HERE",
                            DisplayName = "Custom Model",
                            MaxTokens = 128_000,
                            IsImageInputSupported = false,
                            IsFunctionCallingSupported = false,
                            IsDeepThinkingSupported = false,
                        }
                    ]
                }
            ],
            _settings.Model.ModelProviders);

        _settings.Model.SelectedModelProviderId ??= _settings.Model.ModelProviders.FirstOrDefault()?.Id;
        _settings.Model.SelectedModelDefinitionId ??= _settings.Model.ModelProviders.FirstOrDefault()?.ModelDefinitions.FirstOrDefault()?.Id;
    }

    /// <summary>
    /// Applies the model providers from the source list to the destination list, without override custom properties.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    private static void ApplyModelProviders(IList<ModelProvider> src, ObservableCollection<ModelProvider> dst)
    {
        var propertyCache = new Dictionary<Type, PropertyInfo[]>();

        foreach (var srcModelProvider in src)
        {
            var dstModelProvider = dst.FirstOrDefault(p => p.Id == srcModelProvider.Id);
            if (dstModelProvider is null)
            {
                dst.Add(srcModelProvider);
            }
            else
            {
                ApplyProperties(srcModelProvider, dstModelProvider, propertyCache);
                ApplyModelDefinitions(srcModelProvider.ModelDefinitions, dstModelProvider.ModelDefinitions, propertyCache);
            }
        }

        for (var i = dst.Count - 1; i >= 0; i--)
        {
            var dstModelProvider = dst[i];
            if (src.All(p => p.Id != dstModelProvider.Id))
            {
                // Remove model provider if it does not exist in the source list
                dst.RemoveAt(i);
            }
        }
    }

    private static void ApplyModelDefinitions(
        IList<ModelDefinition> src,
        ObservableCollection<ModelDefinition> dst,
        Dictionary<Type, PropertyInfo[]> propertyCache)
    {
        foreach (var srcModelDefinition in src)
        {
            var dstModelDefinition = dst.FirstOrDefault(d => d.Id == srcModelDefinition.Id);
            if (dstModelDefinition is null)
            {
                dst.Add(srcModelDefinition);
            }
            else
            {
                ApplyProperties(srcModelDefinition, dstModelDefinition, propertyCache);
            }
        }

        for (var i = dst.Count - 1; i >= 0; i--)
        {
            var dstModelDefinition = dst[i];
            if (src.All(d => d.Id != dstModelDefinition.Id))
            {
                // Remove model definition if it does not exist in the source list
                dst.RemoveAt(i);
            }
        }
    }

    private void InitializeSearchEngineProviders()
    {
        var webSearchEngineSettings = _settings.Plugin.WebSearchEngine;

        // Remove duplicate search engine providers by Id
        webSearchEngineSettings.WebSearchEngineProviders.Reset(webSearchEngineSettings.WebSearchEngineProviders.DistinctBy(p => p.Id));

        ApplySearchEngineProviders(
            [
                new WebSearchEngineProvider
                {
                    Id = "google",
                    EndPoint = "https://customsearch.googleapis.com"
                },
                new WebSearchEngineProvider
                {
                    Id = "bing",
                    EndPoint = "https://api.bing.microsoft.com/v7.0/search?q"
                },
                new WebSearchEngineProvider
                {
                    Id = "brave",
                    EndPoint = "https://api.search.brave.com/res/v1/web/search?q"
                },
                new WebSearchEngineProvider
                {
                    Id = "bocha",
                    EndPoint = "https://api.bochaai.com/v1/web-search"
                },
            ],
            webSearchEngineSettings.WebSearchEngineProviders);

        webSearchEngineSettings.SelectedWebSearchEngineProviderId ??= webSearchEngineSettings.WebSearchEngineProviders.FirstOrDefault()?.Id;
    }

    private static void ApplySearchEngineProviders(IList<WebSearchEngineProvider> src, ObservableCollection<WebSearchEngineProvider> dst)
    {
        var propertyCache = new Dictionary<Type, PropertyInfo[]>();

        foreach (var srcSearchEngineProvider in src)
        {
            var dstSearchEngineProvider = dst.FirstOrDefault(p => p.Id == srcSearchEngineProvider.Id);
            if (dstSearchEngineProvider is null)
            {
                dst.Add(srcSearchEngineProvider);
            }
            else
            {
                ApplyProperties(srcSearchEngineProvider, dstSearchEngineProvider, propertyCache);
            }
        }

        for (var i = dst.Count - 1; i >= 0; i--)
        {
            var dstSearchEngineProvider = dst[i];
            if (src.All(p => p.Id != dstSearchEngineProvider.Id))
            {
                // Remove search engine provider if it does not exist in the source list
                dst.RemoveAt(i);
            }
        }
    }

    private static void ApplyProperties(object src, object dst, Dictionary<Type, PropertyInfo[]> propertyCache)
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
                .ToArray();
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