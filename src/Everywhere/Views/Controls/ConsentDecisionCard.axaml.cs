using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Chat.Permissions;
using ShadUI;

namespace Everywhere.Views;

public class ConsentDecisionEventArgs(ConsentDecision decision) : RoutedEventArgs
{
    public ConsentDecision Decision { get; } = decision;
}

public partial class ConsentDecisionCard : Card
{
    public static readonly RoutedEvent<ConsentDecisionEventArgs> ConsentSelectedEvent =
        RoutedEvent.Register<ConsentDecisionCard, ConsentDecisionEventArgs>(nameof(ConsentSelected), RoutingStrategies.Bubble);

    public event EventHandler<ConsentDecisionEventArgs>? ConsentSelected
    {
        add => AddHandler(ConsentSelectedEvent, value);
        remove => RemoveHandler(ConsentSelectedEvent, value);
    }

    [RelayCommand]
    private void SelectConsent(ConsentDecision decision)
    {
        RaiseEvent(new ConsentDecisionEventArgs(decision) { RoutedEvent = ConsentSelectedEvent });
    }
}