using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Agents;
using Everywhere.Enums;
using Everywhere.Models;
using Everywhere.Views;
using IconPacks.Avalonia.Material;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ObservableCollections;
using ChatMessage = Everywhere.Models.ChatMessage;

namespace Everywhere.ViewModels;

public partial class AssistantFloatingWindowViewModel : BusyViewModelBase
{
    public Settings Settings { get; }

    [ObservableProperty]
    public partial IVisualElement? TargetElement { get; private set; }

    [ObservableProperty]
    public partial PixelRect TargetBoundingRect { get; private set; }

    [ObservableProperty]
    public partial string? Title { get; private set; }

    [ObservableProperty]
    public partial List<DynamicKeyMenuItem> Actions { get; private set; } = [];

    [ObservableProperty]
    public partial List<AssistantCommand> AssistantCommands { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<ChatMessage> ChatMessages =>
        field ??= chatMessages.CreateView(x => x).With(v => v.AttachFilter(m => m.Role != ChatRole.System))
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly IChatCompletionService chatCompletionService;
    private readonly List<DynamicKeyMenuItem> textEditActions;
    private readonly List<AssistantCommand> textEditCommands;
    private readonly ObservableList<ChatMessage> chatMessages = [];

    private CancellationTokenSource? cancellationTokenSource;

    public AssistantFloatingWindowViewModel(Settings settings, IChatCompletionService chatCompletionService)
    {
        Settings = settings;
        this.chatCompletionService = chatCompletionService;

        textEditActions =
        [
            // new DynamicKeyMenuItem
            // {
            //     Icon = MakeIcon(PackIconMaterialKind.Translate),
            //     Header = "AssistantFloatingWindowViewModel_Translate",
            //     Command = GenerateAndReplaceCommand,
            //     CommandParameter =
            //         "Translate the content of XML node with id=\"{ElementId}\" between **{SystemLanguage}** and **English**" // todo
            // },
            // new DynamicKeyMenuItem
            // {
            //     Icon = MakeIcon(PackIconMaterialKind.FastForward),
            //     Header = "AssistantFloatingWindowViewModel_ContinueWriting",
            //     Command = GenerateAndAppendCommand,
            //     CommandParameter =
            //         "The user has already written a beginning as the content of XML node with id=\"{ElementId}\". " +
            //         "You should try to imitate the user's writing style and tone, and continue writing in the user's perspective"
            // },
            // new DynamicKeyMenuItem
            // {
            //     Header = "Change Tone to",
            //     Items =
            //     {
            //         new DynamicKeyMenuItem
            //         {
            //             Header = "Formal",
            //             Command = GenerateAndReplaceCommand,
            //             CommandParameter = "Change the tone of content of XML node with id=\"{ElementId}\" to **Formal**"
            //         },
            //         new DynamicKeyMenuItem
            //         {
            //             Header = "Casual",
            //             Command = GenerateAndReplaceCommand,
            //             CommandParameter = "Change the tone of content of XML node with id=\"{ElementId}\" to **Casual**"
            //         },
            //         new DynamicKeyMenuItem
            //         {
            //             Header = "Creative",
            //             Command = GenerateAndReplaceCommand,
            //             CommandParameter = "Change the tone of content of XML node with id=\"{ElementId}\" to **Creative**"
            //         },
            //         new DynamicKeyMenuItem
            //         {
            //             Header = "Professional",
            //             Command = GenerateAndReplaceCommand,
            //             CommandParameter = "Change the tone of content of XML node with id=\"{ElementId}\" to **Professional**"
            //         }
            //     }
            // }
        ];

        textEditCommands =
        [
            new AssistantCommand(
                "/translate",
                "AssistantCommand_Translate_Description",
                Prompts.GetDefaultSystemPromptWithMission("Based on context, translate the content of XML node with id=\"{ElementId}\""),
                "Translate it into {0}",
                () => Settings.Common.Language),
            new AssistantCommand(
                "/rewrite",
                "AssistantCommand_Rewrite_Description",
                Prompts.GetDefaultSystemPromptWithMission("Based on context, rewrite the content of XML node with id=\"{ElementId}\""),
                "{0}",
                () => "Refine it"),
        ];
    }

    private CancellationTokenSource? targetElementChangedTokenSource;

    [RelayCommand]
    public async Task SetTargetElementAsync(IVisualElement? targetElement)
    {
        // debouncing
        if (targetElementChangedTokenSource is not null) await targetElementChangedTokenSource.CancelAsync();
        targetElementChangedTokenSource = new CancellationTokenSource();
        var cancellationToken = targetElementChangedTokenSource.Token;
        try
        {
            await Task.Delay(100, cancellationToken);
        }
        catch (OperationCanceledException) { }

        await ExecuteBusyTaskAsync(
            _ =>
            {
                if (Equals(TargetElement, targetElement)) return;

                Reset();

                if (targetElement is not { Type: VisualElementType.TextEdit } ||
                    (targetElement.States & (
                        VisualElementStates.Offscreen |
                        VisualElementStates.Disabled |
                        VisualElementStates.ReadOnly |
                        VisualElementStates.Password)) != 0)
                {
                    TargetElement = null;
                    return;
                }

                using (var process = Process.GetProcessById(targetElement.ProcessId))
                {
                    Title = Path.GetFileNameWithoutExtension(process.ProcessName);
                }

                TargetBoundingRect = targetElement.BoundingRectangle;
                TargetElement = targetElement;
                Actions = textEditActions;
                AssistantCommands = textEditCommands;
            },
            flags: ExecutionFlags.EnqueueIfBusy,
            cancellationToken: cancellationToken);
    }

    private static readonly ChatRole ActionRole = new("Action");

    [RelayCommand]
    private Task ProcessChatMessageSentAsync(string message) => ExecuteBusyTaskAsync(
        async cancellationToken =>
        {
            message = message.Trim();
            if (message.Length == 0) return;

            string? systemPrompt = null;
            ChatMessage? userMessage = null;

            if (message[0] == '/')
            {
                var commandString = message.IndexOf(' ') is var index and > 0 ? message[..index] : message;
                if (AssistantCommands.FirstOrDefault(
                        c => c.Command.Equals(commandString, StringComparison.OrdinalIgnoreCase)) is { } command)
                {
                    systemPrompt = command.SystemPrompt;
                    var commandArgument = message[commandString.Length..].Trim();
                    if (commandArgument.Length == 0)
                    {
                        commandArgument = command.DefaultValueFactory?.Invoke() ?? string.Empty;
                    }
                    var userPrompt = string.Format(command.UserPrompt, commandArgument);
                    userMessage = new ChatMessage(ChatRole.User, userPrompt)
                    {
                        InlineCollection =
                        {
                            new Run(commandString) { TextDecorations = TextDecorations.Underline },
                            new Run(' ' + commandArgument)
                        }
                    };
                }
            }

            systemPrompt ??= Prompts.GetDefaultSystemPromptWithMission(
                "Focused XML node id=\"{ElementId}\". Based on context, answer the user's question");
            userMessage ??= new ChatMessage(ChatRole.User, message) { InlineCollection = { message } };
            chatMessages.Add(userMessage);

            if (chatMessages.Count == 1)
            {
                if (TargetElement is not { } targetElement) return;

                var analysisMessage = new ChatMessage(ActionRole)
                {
                    InlineCollection = { "Analyzing context" }
                };
                chatMessages.Add(analysisMessage);

                analysisMessage.InlineCollection.IsBusy = true;
                var builtSystemPrompt = await Task.Run(() => BuildSystemPrompt(targetElement, systemPrompt, cancellationToken), cancellationToken);
                chatMessages.Remove(analysisMessage);
                chatMessages.Insert(0, new ChatMessage(ChatRole.System, builtSystemPrompt) { InlineCollection = { builtSystemPrompt } });
            }

            await GenerateAsync(cancellationToken);
        });

    // [RelayCommand]
    // private Task AcceptAsync() => ExecuteBusyTaskAsync(
    //     () =>
    //     {
    //         if (TargetElement is not { } element) return;
    //         element.SetText(generatedTextBuilder.ToString(), appendText);
    //         Reset();
    //     });

    [RelayCommand]
    private Task RetryAsync(ChatMessage chatMessage) => ExecuteBusyTaskAsync(
        cancellationToken =>
        {
            var index = chatMessages.IndexOf(chatMessage);
            if (index == -1) return Task.CompletedTask;
            chatMessages.RemoveRange(index, chatMessages.Count - index); // TODO: history tree
            return GenerateAsync(cancellationToken);
        });

    [RelayCommand]
    private static Task CopyAsync(ChatMessage chatMessage) =>
        ServiceLocator.Resolve<AssistantFloatingWindow>().Clipboard?.SetTextAsync(chatMessage.ToString()) ?? Task.CompletedTask;

    [RelayCommand]
    private void Cancel()
    {
        if (IsBusy) return;
        Reset();
    }

    private void Reset()
    {
        cancellationTokenSource?.Cancel();
        IsExpanded = false;
        chatMessages.Clear();
        Actions = [];
        AssistantCommands = [];
    }

    private string BuildSystemPrompt(IVisualElement element, string systemPrompt, CancellationToken cancellationToken)
    {
#if DEBUG
        Debug.WriteLine("BuildXml started...");
        var sw = Stopwatch.StartNew();
#endif

        var rootElement = new OptimizedVisualElement(
            element
                .GetAncestors()
                .CurrentAndNext()
                .Where(p => p.current.ProcessId != p.next.ProcessId)
                .Select(p => p.current)
                .First());

        var visualTreeXmlBuilder = new StringBuilder();
        var idMap = new Dictionary<IVisualElement, int>();
        BuildXml(rootElement, 0);

#if DEBUG
        sw.Stop();
        Debug.WriteLine($"BuildXml finished in {sw.ElapsedMilliseconds}ms");
#endif

        var values = new Dictionary<string, Func<string>>
        {
            { "OS", () => Environment.OSVersion.ToString() },
            { "Time", () => DateTime.Now.ToString("F") },
            { "VisualTree", () => visualTreeXmlBuilder.Remove(visualTreeXmlBuilder.Length - 1, 1).ToString() }, // todo: lazy build xml
            { "SystemLanguage", () => new CultureInfo(Settings.Common.Language).DisplayName },
            { "ElementId", () => idMap[element].ToString() },
        }.ToFrozenDictionary();

        var renderedSystemPrompt = StringTemplateRegex().Replace(
            systemPrompt,
            m => values.TryGetValue(m.Groups[1].Value, out var getter) ? getter() : m.Value);

        return renderedSystemPrompt;

        void BuildXml(IVisualElement currentElement, int indentLevel)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var indent = new string(' ', indentLevel * 2);
            visualTreeXmlBuilder.Append(indent);
            visualTreeXmlBuilder.Append('<').Append(currentElement.Type);

            var id = idMap.Count;
            idMap.Add(currentElement, id);
            visualTreeXmlBuilder.Append(" id=\"").Append(id).Append('"');

            if (currentElement.Name is { } name && !string.IsNullOrWhiteSpace(name))
            {
                visualTreeXmlBuilder.Append(" name=\"").Append(SecurityElement.Escape(name)).Append('"');
            }

            var textLines = currentElement.GetText()?.Split(Environment.NewLine);

            using var childrenEnumerator = currentElement.Children.GetEnumerator();
            if (textLines is [{ } text] && !string.IsNullOrWhiteSpace(text))
            {
                visualTreeXmlBuilder.Append(" text=\"").Append(SecurityElement.Escape(text)).Append('"');
            }

            if (!childrenEnumerator.MoveNext())
            {
                // If the element has no children, we can omit the text node in a single line.
                visualTreeXmlBuilder.AppendLine("/>");
            }
            else
            {
                visualTreeXmlBuilder.AppendLine(">");
                if (textLines is { Length: > 1 })
                {
                    var textLineIndent = new string(' ', indentLevel * 2 + 2);
                    foreach (var textLine in textLines)
                    {
                        if (string.IsNullOrWhiteSpace(textLine)) continue;
                        visualTreeXmlBuilder.Append(textLineIndent).AppendLine(SecurityElement.Escape(textLine));
                    }
                }

                do
                {
                    BuildXml(childrenEnumerator.Current, indentLevel + 1);
                }
                while (childrenEnumerator.MoveNext());
                visualTreeXmlBuilder.Append(indent).Append("</").Append(currentElement.Type).AppendLine(">");
            }
        }
    }

    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        var chatHistory = new ChatHistory(
            chatMessages
                .Where(m => m.Role.Value is "system" or "assistant" or "user" or "tool")
                .Select(m => new ChatMessageContent(new AuthorRole(m.Role.Value), m.ToString())));
        var assistantMessage = new ChatMessage(ChatRole.Assistant)
        {
            InlineCollection =
            {
                IsBusy = true,
            },
        };
        chatMessages.Add(assistantMessage);
        try
        {
            await foreach (var message in chatCompletionService.GetStreamingChatMessageContentsAsync(
                               chatHistory,
                               cancellationToken: cancellationToken))
            {
                if (message.Content is { Length: > 0 } content) assistantMessage.InlineCollection.Add(content);
            }
        }
        finally
        {
            assistantMessage.InlineCollection.IsBusy = false;
        }
    }

    [GeneratedRegex(@"(?<!\{)\{(\w+)\}(?!\})")]
    private static partial Regex StringTemplateRegex();
}