using System.Reflection;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Everywhere.Attributes;
using Everywhere.Enums;
using Everywhere.Models;
using ZLinq;

namespace Everywhere.ViewModels;

public class SettingsPageViewModel : ReactiveViewModelBase
{
    public IReadOnlyList<SettingsItemGroup> Groups { get; }

    public SettingsPageViewModel(Settings settings)
    {
        Groups = typeof(Settings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .AsValueEnumerable()
            .Where(p => p.GetCustomAttribute<HiddenSettingsItemAttribute>() is null)
            .Where(p => p.PropertyType.IsAssignableTo(typeof(SettingsBase)))
            .Select(p => new SettingsItemGroup(p.Name, CreateItems(p.Name, p.Name, p.PropertyType))).ToArray();

        SettingsItem[] CreateItems(string bindingPath, string groupName, Type ownerType)
        {
            return ownerType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .AsValueEnumerable()
                .Where(p => p is { CanRead: true, CanWrite: true })
                .Where(p => p.GetCustomAttribute<HiddenSettingsItemAttribute>() is null)
                .Select(p => CreateItem(bindingPath, groupName, p))
                .OfType<SettingsItem>()
                .ToArray();
        }

        SettingsItem? CreateItem(
            string bindingPath,
            string ownerName,
            PropertyInfo itemPropertyInfo,
            MemberInfo? attributeOwner = null)
        {
            SettingsItem? result = null;

            attributeOwner ??= itemPropertyInfo;
            var name = $"{ownerName}_{itemPropertyInfo.Name}";

            if (attributeOwner.GetCustomAttribute<SettingsSelectionItemAttribute>() is { } selectionAttribute)
            {
                result = new SettingsSelectionItem(name)
                {
                    [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                    [!SettingsSelectionItem.ItemsSourceProperty] = MakeBinding(
                        $"{bindingPath}.{selectionAttribute.ItemsSource}",
                        BindingMode.OneWay,
                        new FuncValueConverter<object?, IEnumerable<SettingsSelectionItem.Item>?>(x =>
                        {
                            if (x is not IEnumerable enumerable) return null;

                            if (!selectionAttribute.I18N)
                            {
                                return enumerable
                                    .AsValueEnumerable()
                                    .Select(k => new SettingsSelectionItem.Item(new DirectResourceKey(k), k))
                                    .ToArray();
                            }

                            var keyPrefix = $"SettingsSelectionItem_{bindingPath.Replace('.', '_')}_{itemPropertyInfo.Name}";
                            return enumerable
                                .AsValueEnumerable()
                                .Select(k => new SettingsSelectionItem.Item(new DynamicResourceKey($"{keyPrefix}_{k}"), k))
                                .ToArray();
                        }))
                };
            }
            else if (itemPropertyInfo.PropertyType == typeof(bool))
            {
                result = new SettingsBooleanItem(name)
                {
                    [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                    IsNullable = false
                };
            }
            else if (itemPropertyInfo.PropertyType == typeof(bool?))
            {
                result = new SettingsBooleanItem(name)
                {
                    [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                    IsNullable = true
                };
            }
            else if (itemPropertyInfo.PropertyType == typeof(string))
            {
                var attribute = attributeOwner.GetCustomAttribute<SettingsStringItemAttribute>();
                result = new SettingsStringItem(name)
                {
                    [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                    Watermark = attribute?.Watermark,
                    MaxLength = attribute?.MaxLength ?? int.MaxValue,
                    IsMultiline = attribute?.IsMultiline ?? false,
                    PasswordChar = (attribute?.IsPassword ?? false) ? '*' : '\0'
                };
            }
            else if (itemPropertyInfo.PropertyType == typeof(int))
            {
                var attribute = attributeOwner.GetCustomAttribute<SettingsIntegerItemAttribute>();
                result = new SettingsIntegerItem(name)
                {
                    [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                    MinValue = attribute?.Min ?? int.MinValue,
                    MaxValue = attribute?.Max ?? int.MaxValue,
                    IsSliderVisible = attribute?.IsSliderVisible ?? true
                };
            }
            else if (itemPropertyInfo.PropertyType == typeof(double))
            {
                var attribute = attributeOwner.GetCustomAttribute<SettingsDoubleItemAttribute>();
                result = new SettingsDoubleItem(name)
                {
                    [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                    MinValue = attribute?.Min ?? double.NegativeInfinity,
                    MaxValue = attribute?.Max ?? double.PositiveInfinity,
                    Step = attribute?.Step ?? 0.1d,
                    IsSliderVisible = attribute?.IsSliderVisible ?? true
                };
            }
            else if (itemPropertyInfo.PropertyType.IsGenericType &&
                     itemPropertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Customizable<>))
            {
                var bindingValueProperty = itemPropertyInfo.PropertyType.GetProperty(nameof(Customizable<>.BindableValue)).NotNull();
                var bindingValueItem = CreateItem(
                    $"{bindingPath}.{itemPropertyInfo.Name}",
                    name,
                    bindingValueProperty,
                    itemPropertyInfo);
                if (bindingValueItem is null)
                {
                    result = null;
                }
                else
                {
                    if (bindingValueItem is SettingsStringItem settingsStringItem)
                    {
                        settingsStringItem[!SettingsStringItem.WatermarkProperty] =
                            MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}.{nameof(Customizable<>.DefaultValue)}", BindingMode.OneWay);
                    }

                    result = new SettingsCustomizableItem(name, bindingValueItem)
                    {
                        [!SettingsCustomizableItem.ResetCommandProperty] =
                            MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}.{nameof(Customizable<>.ResetCommand)}"),
                    };
                }
            }
            else if (itemPropertyInfo.PropertyType == typeof(KeyboardHotkey))
            {
                result = new SettingsKeyboardHotkeyItem(name)
                {
                    [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                };
            }
            else if (itemPropertyInfo.PropertyType.IsEnum)
            {
                result = SettingsSelectionItem.FromEnum(itemPropertyInfo.PropertyType, name, MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"));
            }

            if (result is null) return null;

            if (itemPropertyInfo.GetCustomAttribute<SettingsItemsAttribute>() is { } settingsItemsAttribute)
            {
                result.IsExpanded = settingsItemsAttribute.IsExpanded;
                result[!SettingsItem.IsExpandableProperty] = MakeBinding(
                    $"{bindingPath}.{itemPropertyInfo.Name}",
                    BindingMode.OneWay,
                    ObjectConverters.IsNotNull);
                result.Items.AddRange(CreateItems($"{bindingPath}.{itemPropertyInfo.Name}", name, itemPropertyInfo.PropertyType));
            }

            return result;
        }

        Binding MakeBinding(string path, BindingMode mode = BindingMode.TwoWay, IValueConverter? converter = null) => new(path, mode)
        {
            Source = settings,
            Converter = converter
        };

        InitializeModelProviders(settings);
    }

    private static void InitializeModelProviders(Settings settings)
    {
        if (settings.Model.ModelProviders.Count > 0) return;

        settings.Model.ModelProviders =
        [
            new ModelProvider
            {
                Id = "openai",
                DisplayName = "OpenAI",
                Endpoint = "https://api.openai.com/v1",
                IconUrl = "https://openai.com/favicon.ico",
                Schema = ModelProviderSchema.OpenAI,
                ModelDefinitions =
                [
                    new ModelDefinition
                    {
                        Id = "o4-mini",
                        DisplayName = "o4-mini",
                        MaxTokens = 200_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 04, 17),
                        InputPrice = "$1.10/M",
                        OutputPrice = "$4.40/M"
                    },
                    new ModelDefinition
                    {
                        Id = "o3",
                        DisplayName = "o3",
                        MaxTokens = 200_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 04, 16),
                        InputPrice = "$2.00/M",
                        OutputPrice = "$8.00/M"
                    },
                    new ModelDefinition
                    {
                        Id = "gpt-4.1",
                        DisplayName = "GPT-4.1",
                        MaxTokens = 1_000_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = false,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 04, 14),
                        InputPrice = "$2.00/M",
                        OutputPrice = "$8.00/M"
                    },
                    new ModelDefinition
                    {
                        Id = "gpt-4.1-mini",
                        DisplayName = "GPT-4.1 mini",
                        MaxTokens = 1_000_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = false,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 04, 14),
                        InputPrice = "$0.40/M",
                        OutputPrice = "$1.60/M"
                    },
                    new ModelDefinition
                    {
                        Id = "gpt-4o",
                        DisplayName = "GPT-4o",
                        MaxTokens = 128_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = false,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2024, 05, 13),
                        InputPrice = "$2.50/M",
                        OutputPrice = "$10.00/M"
                    },
                ]
            },
            new ModelProvider
            {
                Id = "anthropic",
                DisplayName = "Anthropic",
                Endpoint = "https://api.anthropic.com",
                IconUrl = "https://www.anthropic.com/favicon.ico",
                Schema = ModelProviderSchema.Anthropic,
                ModelDefinitions =
                [
                    new ModelDefinition
                    {
                        Id = "claude-opus-4-20250514",
                        DisplayName = "Claude Opus 4",
                        MaxTokens = 200_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 05, 23),
                        InputPrice = "$15.00/M",
                        OutputPrice = "$75.00/M"
                    },
                    new ModelDefinition
                    {
                        Id = "claude-sonnet-4-20250514",
                        DisplayName = "Claude Sonnet 4",
                        MaxTokens = 200_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 05, 23),
                        InputPrice = "3.00/M",
                        OutputPrice = "$15.00/M"
                    },
                    new ModelDefinition
                    {
                        Id = "claude-3-7-sonnet-20250219",
                        DisplayName = "Claude 3.7 Sonnet",
                        MaxTokens = 200_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 02, 24),
                        InputPrice = "$3.00/M",
                        OutputPrice = "$15.00/M"
                    },
                    new ModelDefinition
                    {
                        Id = "claude-3-5-haiku-20241022",
                        DisplayName = "Claude 3.5 Haiku",
                        MaxTokens = 200_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = false,
                        IsWebSearchSupported = false,
                        ReleasedAt = new DateOnly(2024, 10, 22),
                        InputPrice = "$1.00/M",
                        OutputPrice = "$5.00/M"
                    },
                ]
            },
            new ModelProvider
            {
                Id = "deepseek",
                DisplayName = "DeepSeek",
                Endpoint = "https://api.deepseek.com",
                IconUrl = "https://www.deepseek.com/favicon.ico",
                Schema = ModelProviderSchema.OpenAI,
                ModelDefinitions =
                [
                    new ModelDefinition
                    {
                        Id = "deepseek-chat",
                        DisplayName = "DeepSeek V3",
                        MaxTokens = 64_000,
                        IsImageInputSupported = false,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = false,
                        IsWebSearchSupported = false,
                        ReleasedAt = new DateOnly(2025, 03, 24),
                        InputPrice = "$0.28/M",
                        OutputPrice = "$1.0/M"
                    },
                    new ModelDefinition
                    {
                        Id = "deepseek-reasoner",
                        DisplayName = "DeepSeek R1",
                        MaxTokens = 64_000,
                        IsImageInputSupported = false,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = false,
                        ReleasedAt = new DateOnly(2025, 05, 28),
                        InputPrice = "0.55/M",
                        OutputPrice = "$2.21/M"
                    },
                ]
            },
            new ModelProvider
            {
                Id = "moonshot",
                DisplayName = "Moonshot",
                Endpoint = "https://api.moonshot.cn/v1",
                IconUrl = "https://statics.moonshot.cn/moonshot-ai/favicon.ico",
                Schema = ModelProviderSchema.OpenAI,
                ModelDefinitions =
                [
                    new ModelDefinition
                    {
                        Id = "kimi-k2-0711-preview",
                        DisplayName = "Kimi K2",
                        MaxTokens = 128_000,
                        IsImageInputSupported = false,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = false,
                        IsWebSearchSupported = false,
                        ReleasedAt = new DateOnly(2025, 07, 11),
                        InputPrice = "$0.55/M",
                        OutputPrice = "$2.21/M"
                    },
                    new ModelDefinition
                    {
                        Id = "kimi-latest",
                        DisplayName = "Kimi Latest",
                        MaxTokens = 128_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = false,
                        IsWebSearchSupported = false,
                        ReleasedAt = new DateOnly(2025, 02, 17),
                        InputPrice = "$1.38/M",
                        OutputPrice = "$4.14/M"
                    },
                    new ModelDefinition
                    {
                        Id = "kimi-thinking-preview",
                        DisplayName = "Kimi Thinking Preview",
                        MaxTokens = 128_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = false,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = false,
                        ReleasedAt = new DateOnly(2025, 05, 06),
                        InputPrice = "$27.62/M",
                        OutputPrice = "$27.62/M"
                    },
                ]
            },
            new ModelProvider
            {
                Id = "xai",
                DisplayName = "xAI (Grok)",
                Endpoint = "https://api.x.ai/v1",
                IconUrl = "https://x.ai/favicon.ico",
                Schema = ModelProviderSchema.OpenAI,
                ModelDefinitions =
                [
                    new ModelDefinition
                    {
                        Id = "grok-4",
                        DisplayName = "Grok 4",
                        MaxTokens = 256_000,
                        IsImageInputSupported = true,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 07, 09),
                        InputPrice = "$3.00/M",
                        OutputPrice = "$15.00/M"
                    },
                    new ModelDefinition
                    {
                        Id = "grok-3-mini",
                        DisplayName = "Grok 3 Mini",
                        MaxTokens = 128_000,
                        IsImageInputSupported = false,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 04, 03),
                        InputPrice = "$0.30/M",
                        OutputPrice = "$0.50/M"
                    },
                    new ModelDefinition
                    {
                        Id = "grok-3",
                        DisplayName = "Grok 3",
                        MaxTokens = 128_000,
                        IsImageInputSupported = false,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = false,
                        IsWebSearchSupported = true,
                        ReleasedAt = new DateOnly(2025, 04, 03),
                        InputPrice = "$3.00/M",
                        OutputPrice = "$15.00/M"
                    },
                ]
            },
            new ModelProvider
            {
                Id = "ollama",
                DisplayName = "Ollama",
                Endpoint = "http://127.0.0.1:11434",
                IconUrl = "https://ollama.com/public/ollama.png",
                Schema = ModelProviderSchema.Ollama,
                ModelDefinitions =
                [
                    new ModelDefinition
                    {
                        Id = "gpt-oss:20b",
                        DisplayName = "GPT-OSS 20B",
                        MaxTokens = 32_768,
                        IsImageInputSupported = false,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = false,
                        ReleasedAt = new DateOnly(2025, 08, 05),
                    },
                    new ModelDefinition
                    {
                        Id = "deepseek-r1:8b",
                        DisplayName = "DeepSeek R1 7B",
                        MaxTokens = 65_536,
                        IsImageInputSupported = false,
                        IsFunctionCallingSupported = false,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = false,
                    },
                    new ModelDefinition
                    {
                        Id = "qwen3:8b",
                        DisplayName = "Qwen 3 8B",
                        MaxTokens = 64_000,
                        IsImageInputSupported = false,
                        IsFunctionCallingSupported = true,
                        IsDeepThinkingSupported = true,
                        IsWebSearchSupported = false
                    }
                ]
            }
        ];
        settings.Model.SelectedModelProviderId = "openai";
        settings.Model.SelectedModelDefinitionId = "gpt-4.1";
    }
}