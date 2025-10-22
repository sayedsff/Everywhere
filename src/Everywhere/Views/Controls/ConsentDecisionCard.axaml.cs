using Everywhere.Chat.Permissions;
using ShadUI;

namespace Everywhere.Views;

public class ConsentDecisionCard : Card
{
    /// <summary>
    /// Defines the <see cref="SelectedConsent"/> property.
    /// </summary>
    public static readonly StyledProperty<ConsentDecision?> SelectedConsentProperty =
        AvaloniaProperty.Register<ConsentDecisionCard, ConsentDecision?>(nameof(SelectedConsent));

    /// <summary>
    /// Gets or sets the selected consent decision. null if no decision has been made.
    /// </summary>
    public ConsentDecision? SelectedConsent
    {
        get => GetValue(SelectedConsentProperty);
        set => SetValue(SelectedConsentProperty, value);
    }
}