using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.AI;
using Everywhere.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ShadUI;
using ZLinq;

namespace Everywhere.ViewModels;

public partial class WelcomeViewModel : BusyViewModelBase
{
    public enum Step
    {
        Intro = 0,
        SelectProvider = 1,
        /// <summary>
        /// Enter API Key (optional: select model) and validate
        /// </summary>
        EnterApiKey = 2,
        /// <summary>
        /// Teach hotkey
        /// </summary>
        Finale = 3
    }

    public record ModelProviderWrapper(ModelProvider Provider)
    {
        public DynamicResourceKeyBase DescriptionKey => new DynamicResourceKey($"WelcomeView_ModelProviderWrapper_{Provider.Id}_Description");

        public Uri OfficialWebsiteUri => new(Provider.OfficialWebsiteUrl?.DefaultValue ?? "https://everywhere.nekora.dev", UriKind.Absolute);

        public Uri ApiKeyHelpUri => new($"https://everywhere.nekora.dev/model-provider/{Provider.Id}", UriKind.Absolute);
    }

    [ObservableProperty]
    public partial Step CurrentStep { get; private set; }

    public IReadOnlyList<ModelProviderWrapper> ModelProviders { get; }

    public ModelProviderWrapper? SelectedModelProvider
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;

            ModelDefinitions = field?.Provider.ModelDefinitions
                    .AsValueEnumerable()
                    .Where(m => m.Id != "custom")
                    .ToList()
                ?? [];
            SelectedModelDefinition = ModelDefinitions.FirstOrDefault(m => m.IsDefault) ?? ModelDefinitions.First();

            ApiKey = null;
            GoToEnterApiKeyStepCommand.NotifyCanExecuteChanged();
        }
    }

    [ObservableProperty]
    public partial IReadOnlyList<ModelDefinition> ModelDefinitions { get; private set; } = [];

    [ObservableProperty]
    public partial ModelDefinition? SelectedModelDefinition { get; set; }

    public string? ApiKey
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;

            ValidateApiKeyCommand.NotifyCanExecuteChanged();
            IsApiKeyValid = false;
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoToFinaleStepCommand))]
    public partial bool IsApiKeyValid { get; private set; }

    public event Action? ApiKeyValidated;

    public Settings Settings { get; }

    private readonly IKernelMixinFactory _kernelMixinFactory;
    private readonly ILogger<WelcomeViewModel> _logger;

    public WelcomeViewModel(
        IKernelMixinFactory kernelMixinFactory,
        Settings settings,
        ILogger<WelcomeViewModel> logger)
    {
        _kernelMixinFactory = kernelMixinFactory;
        Settings = settings;
        _logger = logger;

        ModelProviders = settings.Model.ModelProviders
            .AsValueEnumerable()
            .Where(m => m.Id is "openai" or "anthropic" or "google" or "deepseek" or "moonshot" or "openrouter" or "siliconflow")
            .Select(m => new ModelProviderWrapper(m))
            .ToList();
    }

    [RelayCommand]
    private void Close() => DialogManager.CloseAll();

    [RelayCommand]
    private void GoToSelectProviderStep() => CurrentStep = Step.SelectProvider;

    [RelayCommand]
    private void PreviousStep() => CurrentStep = (Step)Math.Max(0, (int)CurrentStep - 1);

    public bool CanGoToEnterApiKeyStep => SelectedModelProvider is not null;

    [RelayCommand(CanExecute = nameof(CanGoToEnterApiKeyStep))]
    private void GoToEnterApiKeyStep() => CurrentStep = Step.EnterApiKey;

    [MemberNotNullWhen(true, nameof(ApiKey), nameof(SelectedModelProvider), nameof(SelectedModelDefinition))]
    public bool CanValidateApiKey => !string.IsNullOrWhiteSpace(ApiKey) && SelectedModelProvider is not null && SelectedModelDefinition is not null;

    [RelayCommand(CanExecute = nameof(CanValidateApiKey))]
    private Task ValidateApiKey() => ExecuteBusyTaskAsync(async cancellationToken =>
    {
        if (!CanValidateApiKey) return;

        IsApiKeyValid = false;

        try
        {
            var modelSettings = new ModelSettings
            {
                ModelProviders = { SelectedModelProvider.Provider },
                SelectedModelProvider = SelectedModelProvider.Provider,
                SelectedModelDefinition = SelectedModelDefinition
            };
            var kernelMixin = _kernelMixinFactory.GetOrCreate(modelSettings, ApiKey);
            await kernelMixin.ChatCompletionService.GetChatMessageContentAsync(
                [
                    new ChatMessageContent(AuthorRole.System, "You're a helpful assistant."),
                    new ChatMessageContent(AuthorRole.User, Prompts.TestPrompt)
                ],
                cancellationToken: cancellationToken);

            IsApiKeyValid = true;
            ApiKeyValidated?.Invoke();

            // Apply settings
            Settings.Model.SelectedModelProvider = SelectedModelProvider.Provider;
            Settings.Model.SelectedModelProvider.ApiKey = ApiKey;
            Settings.Model.SelectedModelDefinition = SelectedModelDefinition;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to validate API key for provider {ProviderId} and model {ModelId}",
                SelectedModelProvider.Provider.Id,
                SelectedModelDefinition.Id);
            ToastManager
                .CreateToast(LocaleKey.WelcomeViewModel_ValidateApiKey_FailedToast_Title.I18N())
                .WithContent(ex.GetFriendlyMessage().ToTextBlock())
                .DismissOnClick()
                .ShowError();
        }
    });

    [RelayCommand(CanExecute = nameof(IsApiKeyValid))]
    private void GoToFinaleStep() => CurrentStep = Step.Finale;
}