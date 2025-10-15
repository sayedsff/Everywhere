using System.Text.Json.Serialization;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Everywhere.Configuration;
using Lucide.Avalonia;

namespace Everywhere.AI;

/// <summary>
/// Allowing users to define and manage their own custom AI assistants.
/// </summary>
public partial class CustomAssistant : ObservableObject
{
    [HiddenSettingsItem]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [ObservableProperty]
    public partial ColoredIcon? Icon { get; set; }

    [ObservableProperty]
    [SettingsStringItem(MaxLength = 32)]
    public required partial string Name { get; set; }

    [ObservableProperty]
    [SettingsStringItem(IsMultiline = true, MaxLength = 4096, Height = 80)]
    public partial string? Description { get; set; }

    [ObservableProperty]
    [SettingsStringItem(IsMultiline = true, MaxLength = 40960)]
    public partial Customizable<string> SystemPrompt { get; set; } = Prompts.DefaultSystemPrompt;

    [JsonIgnore]
    [HiddenSettingsItem]
    public ModelProviderTemplate? ModelProviderTemplate
    {
        get => ModelProviderTemplate.SupportedTemplates.FirstOrDefault(t => t.Id == ModelProviderTemplateId);
        set => ModelProviderTemplateId = value?.Id;
    }

    /// <summary>
    /// The ID of the model provider to use for this custom assistant.
    /// This ID should correspond to one of the available model providers in the application.
    /// </summary>
    /// <remarks>
    /// Setting this will set <see cref="Endpoint"/> and <see cref="Schema"/> to the values from the selected model provider.
    /// </remarks>
    public string? ModelProviderTemplateId
    {
        get;
        set
        {
            if (value == field) return;
            field = value;

            if (value is not null &&
                ModelProviderTemplate.SupportedTemplates.FirstOrDefault(t => t.Id == value) is { } template)
            {
                Endpoint.DefaultValue = template.Endpoint;
                Schema.DefaultValue = template.Schema;
                // Note: We do not set ApiKey here, as it may be different for each custom assistant.

                ModelDefinitionTemplateId = template.ModelDefinitions.FirstOrDefault(m => m.IsDefault)?.Id;
            }
            else
            {
                Endpoint.DefaultValue = string.Empty;
                Schema.DefaultValue = ModelProviderSchema.OpenAI;
                ModelDefinitionTemplateId = null;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(ModelProviderTemplate));
        }
    }

    [ObservableProperty]
    public partial Customizable<string> Endpoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Customizable<ModelProviderSchema> Schema { get; set; } = ModelProviderSchema.OpenAI;

    [ObservableProperty]
    [SettingsStringItem(IsPassword = true)]
    public partial string? ApiKey { get; set; }

    [JsonIgnore]
    [HiddenSettingsItem]
    public ModelDefinitionTemplate? ModelDefinitionTemplate
    {
        get => ModelProviderTemplate.SupportedTemplates.FirstOrDefault(t => t.Id == ModelProviderTemplateId)?
            .ModelDefinitions.FirstOrDefault(m => m.Id == ModelDefinitionTemplateId);
        set => ModelDefinitionTemplateId = value?.Id;
    }

    public string? ModelDefinitionTemplateId
    {
        get;
        set
        {
            if (value == field) return;
            field = value;

            if (value is not null &&
                ModelProviderTemplate.SupportedTemplates.FirstOrDefault(t => t.Id == ModelProviderTemplateId) is { } template &&
                template.ModelDefinitions.FirstOrDefault(m => m.Id == value) is { } modelDefinition)
            {
                ModelId.DefaultValue = modelDefinition.Id;
                IsImageInputSupported.DefaultValue = modelDefinition.IsImageInputSupported;
                IsFunctionCallingSupported.DefaultValue = modelDefinition.IsFunctionCallingSupported;
                IsDeepThinkingSupported.DefaultValue = modelDefinition.IsDeepThinkingSupported;
                MaxTokens.DefaultValue = modelDefinition.MaxTokens;
            }
            else
            {
                ModelId.DefaultValue = string.Empty;
                IsImageInputSupported.DefaultValue = false;
                IsFunctionCallingSupported.DefaultValue = false;
                IsDeepThinkingSupported.DefaultValue = false;
                MaxTokens.DefaultValue = 81920;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(ModelDefinitionTemplate));
        }
    }

    [ObservableProperty]
    public partial Customizable<string> ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the model supports image input capabilities.
    /// </summary>
    [ObservableProperty]
    public partial Customizable<bool> IsImageInputSupported { get; set; } = false;

    /// <summary>
    /// Indicates whether the model supports function calling capabilities.
    /// </summary>
    [ObservableProperty]
    public partial Customizable<bool> IsFunctionCallingSupported { get; set; } = false;

    /// <summary>
    /// Indicates whether the model supports tool calls.
    /// </summary>
    [ObservableProperty]
    public partial Customizable<bool> IsDeepThinkingSupported { get; set; } = false;

    /// <summary>
    /// Maximum number of tokens that the model can process in a single request.
    /// aka, the maximum context length.
    /// </summary>
    [ObservableProperty]
    [SettingsIntegerItem(IsSliderVisible = false)]
    public partial Customizable<int> MaxTokens { get; set; } = 81920;

    [ObservableProperty]
    [SettingsDoubleItem(Min = 0.0, Max = 2.0, Step = 0.1)]
    public partial Customizable<double> Temperature { get; set; } = 1.0;

    [ObservableProperty]
    [SettingsDoubleItem(Min = 0.0, Max = 1.0, Step = 0.1)]
    public partial Customizable<double> TopP { get; set; } = 0.9;

    [ObservableProperty]
    [SettingsDoubleItem(Min = -2.0, Max = 2.0, Step = 0.1)]
    public partial Customizable<double> PresencePenalty { get; set; } = 0.0;

    [ObservableProperty]
    [SettingsDoubleItem(Min = -2.0, Max = 2.0, Step = 0.1)]
    public partial Customizable<double> FrequencyPenalty { get; set; } = 0.0;

    [JsonIgnore]
    public SettingsItems SettingsItems
    {
        get
        {
            var results = SettingsItems.CreateForObject(this, nameof(CustomAssistant));

            // add template selector
            // TODO: performance optimization
            var i = results.FindIndexOf(r => Equals(r.HeaderKey.Key, $"Settings_{nameof(CustomAssistant)}_{nameof(ModelProviderTemplateId)}_Header"));
            if (i >= 0)
            {
                object? modelProviderTemplateDataTemplate = null;
                Application.Current?.Resources.TryGetResource(typeof(ModelProviderTemplate), null, out modelProviderTemplateDataTemplate);
                results[i] = new SettingsCustomizableItem(
                    $"{nameof(CustomAssistant)}_{nameof(ModelProviderTemplate)}",
                    new SettingsSelectionItem($"{nameof(CustomAssistant)}_{nameof(ModelProviderTemplate)}")
                    {
                        ItemsSource = ModelProviderTemplate.SupportedTemplates.Select(t =>
                            new SettingsSelectionItem.Item(
                                new DirectResourceKey(t),
                                t,
                                modelProviderTemplateDataTemplate as IDataTemplate)).ToList(),
                        [!SettingsItem.ValueProperty] = new Binding
                        {
                            Path = nameof(ModelProviderTemplate),
                            Source = this,
                            Mode = BindingMode.TwoWay
                        }
                    })
                {
                    ResetCommand = new RelayCommand(() => ModelProviderTemplateId = null)
                };
            }

            i = results.FindIndexOf(r => Equals(r.HeaderKey.Key, $"Settings_{nameof(CustomAssistant)}_{nameof(ModelDefinitionTemplateId)}_Header"));
            if (i >= 0)
            {
                object? modelDefinitionTemplateDataTemplate = null;
                Application.Current?.Resources.TryGetResource(typeof(ModelDefinitionTemplate), null, out modelDefinitionTemplateDataTemplate);
                results[i] = new SettingsCustomizableItem(
                    $"{nameof(CustomAssistant)}_{nameof(ModelDefinitionTemplate)}",
                    new SettingsSelectionItem($"{nameof(CustomAssistant)}_{nameof(ModelDefinitionTemplate)}")
                    {
                        [!SettingsSelectionItem.ItemsSourceProperty] = new Binding
                        {
                            Path = nameof(ModelProviderTemplateId),
                            Source = this,
                            Converter = new FuncValueConverter<string?, IEnumerable<SettingsSelectionItem.Item>>(modelProviderTemplateId =>
                            {
                                if (modelProviderTemplateId is not null &&
                                    ModelProviderTemplate.SupportedTemplates.FirstOrDefault(t => t.Id == modelProviderTemplateId) is { } template)
                                {
                                    return template.ModelDefinitions.Select(m =>
                                        new SettingsSelectionItem.Item(
                                            new DirectResourceKey(m),
                                            m,
                                            modelDefinitionTemplateDataTemplate as IDataTemplate)).ToList();
                                }

                                return [];
                            })
                        },
                        [!SettingsItem.ValueProperty] = new Binding
                        {
                            Path = nameof(ModelDefinitionTemplate),
                            Source = this,
                            Mode = BindingMode.TwoWay
                        }
                    })
                {
                    ResetCommand = new RelayCommand(() => ModelDefinitionTemplateId = null)
                };
            }

            return results;
        }
    }
}