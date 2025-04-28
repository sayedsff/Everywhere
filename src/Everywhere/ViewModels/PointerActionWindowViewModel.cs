using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Enums;
using Everywhere.Models;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Everywhere.ViewModels;

public partial class PointerActionWindowViewModel : BusyViewModelBase
{
    [ObservableProperty]
    public partial List<MenuItem> Actions { get; private set; } = [];

    [ObservableProperty]
    public partial IVisualElement? PointerOverElement { get; private set; }

    [ObservableProperty]
    public partial bool IsGenerating { get; private set; }

    public InlineCollection GeneratedInlineCollection { get; } = new();

    private readonly IVisualElementContext visualElementContext;
    private readonly IChatCompletionService chatCompletionService;
    private readonly List<MenuItem> textEditActions;
    private readonly List<MenuItem> testActions;
    private readonly StringBuilder generatedTextBuilder = new();

    private Task? generateTask;
    private bool appendText;

    public PointerActionWindowViewModel(IVisualElementContext visualElementContext, IChatCompletionService chatCompletionService)
    {
        this.visualElementContext = visualElementContext;
        this.chatCompletionService = chatCompletionService;

        textEditActions =
        [
            new MenuItem
            {
                Header = "Continue Writing",
                Command = ContinueWritingCommand
            },
            new MenuItem
            {
                Header = "Change Tone to",
                Items =
                {
                    new MenuItem
                    {
                        Header = "Formal",
                        Command = ChangeToneToCommand,
                        CommandParameter = "Formal"
                    },
                    new MenuItem
                    {
                        Header = "Casual",
                        Command = ChangeToneToCommand,
                        CommandParameter = "Casual"
                    },
                    new MenuItem
                    {
                        Header = "Creative",
                        Command = ChangeToneToCommand,
                        CommandParameter = "Creative"
                    },
                    new MenuItem
                    {
                        Header = "Professional",
                        Command = ChangeToneToCommand,
                        CommandParameter = "Professional"
                    }
                }
            }
        ];

        testActions =
        [
            new MenuItem
            {
                Header = "Append",
                Command = new RelayCommand(
                    () => PointerOverElement?.SetText("Hello world", true))
            },
            new MenuItem
            {
                Header = "Replace",
                Command = new RelayCommand(
                    () => PointerOverElement?.SetText("Hello world", false))
            },
            new MenuItem
            {
                Header = "Manbo",
            },
            new MenuItem
            {
                Header = "Manbo",
            },
            new MenuItem
            {
                Header = "Manbo",
            },
            new MenuItem
            {
                Header = "Manbo",
            },
            new MenuItem
            {
                Header = "Manbo",
            },
            new MenuItem
            {
                Header = "Manbo",
            },
            new MenuItem
            {
                Header = "Manbo",
            },
        ];
    }

    [RelayCommand]
    private Task ContinueWritingAsync() => generateTask = ExecuteBusyTaskAsync(
        () => Task.Run(
            async () =>
            {
                if (PointerOverElement is not { } pointerOverElement) return;

                appendText = true;
                await GenerateAsync(pointerOverElement, "Continue writing");
            }));

    [RelayCommand]
    private Task ChangeToneTo(string tone) => generateTask = ExecuteBusyTaskAsync(
        () => Task.Run(
            async () =>
            {
                if (PointerOverElement is not { } pointerOverElement) return;

                appendText = false;
                await GenerateAsync(pointerOverElement, $"Change tone to {tone}");
            }));

    [RelayCommand]
    private Task AcceptAsync() => ExecuteBusyTaskAsync(
        () => Task.Run(
            () =>
            {
                if (PointerOverElement is not { } pointerOverElement) return;
                pointerOverElement.SetText(generatedTextBuilder.ToString(), appendText);
            }));

    [RelayCommand]
    private Task RetryAsync() => generateTask ?? Task.CompletedTask;

    [RelayCommand]
    private void Cancel()
    {
        if (IsBusy) return;

        IsGenerating = false;
        PointerOverElement = null;
        Actions = [];
        GeneratedInlineCollection.Clear();
        generatedTextBuilder.Clear();
    }

    private async Task GenerateAsync(IVisualElement element, string mission)
    {
        IsGenerating = true;

        var rootElement = new OptimizedVisualElement(
            element
                .GetAncestors()
                .CurrentAndNext()
                .Where(p => p.current.ProcessId != p.next.ProcessId)
                .Select(p => p.current)
                .First());

        var visualTreeYamlBuilder = new StringBuilder();
        BuildYaml(rootElement, 0);

        var systemPrompt =
            $"""
             # Description
             You are a helpful assistant named "Everywhere".
             In order to better understand user intent, you have access to user's current screen content in YAML format. It is a tree structure, each node is a visual element. The definition of each node is as follows:
             ```yaml
             id: Unique identifier in one Visual Tree.
             type: Type of the visual element. It can be one of the following: {string.Join(", ", Enum.GetNames<VisualElementType>())}
             name: (nullable) Name of the visual element.
             text: (nullable) Text content of the visual element.
             - child1
             - child2...
             ```

             # Visual Tree
             ```yaml
             {visualTreeYamlBuilder.Remove(visualTreeYamlBuilder.Length - 1, 1)}
             ```

             # Mission
             {mission}, **ONLY** for the target visual element with id {element.Id}.

             # Rules
             - You should only respond mission result in plain text, without any explanation or additional information.
             - You should not include any code blocks in your response.
             - You should keep the language of the response the same as the language of the text content of the target visual element.
             - You should not include any text that is not related to the mission.
             """;

        generatedTextBuilder.Clear();
        await foreach (var messageContent in chatCompletionService.GetStreamingChatMessageContentsAsync(new ChatHistory(systemPrompt)))
        {
            if (string.IsNullOrEmpty(messageContent.Content)) continue;
            generatedTextBuilder.Append(messageContent.Content);
            await Dispatcher.UIThread.InvokeAsync(() => GeneratedInlineCollection.Add(messageContent.Content));
        }

        void BuildYaml(IVisualElement currentElement, int indentLevel)
        {
            if (indentLevel > 0) visualTreeYamlBuilder.Append(new string(' ', indentLevel * 2 - 2)).Append("- ");
            visualTreeYamlBuilder.AppendLine($"id: {currentElement.Id}");
            var indent = new string(' ', indentLevel * 2);
            visualTreeYamlBuilder.AppendLine($"{indent}type: {currentElement.Type}");
            if (!string.IsNullOrWhiteSpace(currentElement.Name))
            {
                visualTreeYamlBuilder.Append($"{indent}name: ");
                AppendEscapedText(indent, currentElement.Name);
            }
            if (currentElement.GetText() is { } text && !string.IsNullOrWhiteSpace(text))
            {
                visualTreeYamlBuilder.Append($"{indent}text: ");
                AppendEscapedText(indent, text);
            }
            foreach (var child in currentElement.Children.Where(e => e.Type is not VisualElementType.Button and not VisualElementType.Image))
            {
                BuildYaml(child, indentLevel + 1);
            }
        }

        void AppendEscapedText(string indent, string text)
        {
            if (text.Contains(Environment.NewLine))
            {
                visualTreeYamlBuilder.AppendLine("|");
                foreach (var line in text.Split(Environment.NewLine))
                {
                    visualTreeYamlBuilder.AppendLine($"{indent}  {line}");
                }
            }
            else
            {
                visualTreeYamlBuilder.AppendLine(text);
            }
        }
    }

    protected internal override Task ViewLoaded(CancellationToken cancellationToken) =>
        ExecuteBusyTaskAsync(
            () => Task.Run(
                () =>
                {
                    IsGenerating = false;
                    PointerOverElement = visualElementContext.PointerOverElement;
                    Actions = PointerOverElement switch
                    {
                        { Type: VisualElementType.TextEdit } => testActions,
                        _ => testActions
                    };
                },
                cancellationToken),
            enqueueIfBusy: true,
            cancellationToken: cancellationToken);
}