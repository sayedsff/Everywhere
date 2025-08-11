using Everywhere.Enums;
using Everywhere.Models;

namespace Everywhere.Initialization;

public class SettingsInitializer(Settings settings) : IAsyncInitializer
{
    public int Priority => 50;

    public Task InitializeAsync()
    {
        InitializeModelProviders();

        return Task.CompletedTask;
    }

    private void InitializeModelProviders()
    {
        if (settings.Model.ModelProviders.Count == 0)
        {
            settings.Model.ModelProviders =
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
                            DisplayName = "GPT-5",
                            MaxTokens = 400_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                            IsWebSearchSupported = true,
                            ReleasedAt = new DateOnly(2025, 08, 07),
                            InputPrice = "$1.25/M",
                            OutputPrice = "$10.00/M"
                        },
                        new ModelDefinition
                        {
                            Id = "gpt-5-mini",
                            DisplayName = "GPT-5 mini",
                            MaxTokens = 400_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                            IsWebSearchSupported = true,
                            ReleasedAt = new DateOnly(2025, 08, 07),
                            InputPrice = "$0.25/M",
                            OutputPrice = "$2.00/M"
                        },
                        new ModelDefinition
                        {
                            Id = "gpt-5-nano",
                            DisplayName = "GPT-5 nano",
                            MaxTokens = 400_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                            IsWebSearchSupported = false,
                            ReleasedAt = new DateOnly(2025, 08, 07),
                            InputPrice = "$0.05/M",
                            OutputPrice = "$0.40/M"
                        },
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
                            DisplayName = "GPT 4.1",
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
                            DisplayName = "GPT 4.1 mini",
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
                    DisplayName = "Anthropic (Claude)",
                    Endpoint = "https://api.anthropic.com",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/anthropic.svg",
                    Schema = ModelProviderSchema.Anthropic,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "claude-opus-4-1-20250805",
                            DisplayName = "Claude Opus 4.1",
                            MaxTokens = 200_000,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                            IsWebSearchSupported = true,
                            ReleasedAt = new DateOnly(2025, 08, 05),
                            InputPrice = "$15.00/M",
                            OutputPrice = "$75.00/M"
                        },
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
                    Id = "gemini",
                    DisplayName = "Gemini (Google)",
                    Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai",
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/gemini-color.svg",
                    Schema = ModelProviderSchema.OpenAI,
                    ModelDefinitions =
                    [
                        new ModelDefinition
                        {
                            Id = "gemini-2.5-pro",
                            DisplayName = "Gemini 2.5 Pro",
                            MaxTokens = 1_048_576,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                            IsWebSearchSupported = true,
                            ReleasedAt = new DateOnly(2025, 06, 01),
                            InputPrice = "$1.25/M",
                            OutputPrice = "$10.00/M"
                        },
                        new ModelDefinition
                        {
                            Id = "gemini-2.5-flash",
                            DisplayName = "Gemini 2.5 Flash",
                            MaxTokens = 1_048_576,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                            IsWebSearchSupported = true,
                            ReleasedAt = new DateOnly(2025, 06, 01),
                            InputPrice = "$0.30/M",
                            OutputPrice = "$2.50/M"
                        },
                        new ModelDefinition
                        {
                            Id = "gemini-2.5-flash0lite",
                            DisplayName = "Gemini 2.5 Flash-Lite",
                            MaxTokens = 1_048_576,
                            IsImageInputSupported = true,
                            IsFunctionCallingSupported = true,
                            IsDeepThinkingSupported = true,
                            IsWebSearchSupported = true,
                            ReleasedAt = new DateOnly(2025, 07, 01),
                            InputPrice = "$0.1/M",
                            OutputPrice = "$0.4/M"
                        },
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
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/moonshot.svg",
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
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/grok.svg",
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
                    IconUrl = "https://registry.npmmirror.com/@lobehub/icons-static-svg/latest/files/icons/ollama.svg",
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
        }

        settings.Model.SelectedModelProviderId ??= settings.Model.ModelProviders.FirstOrDefault()?.Id;
        settings.Model.SelectedModelDefinitionId ??= settings.Model.ModelProviders.FirstOrDefault()?.ModelDefinitions.FirstOrDefault()?.Id;
    }
}