using Everywhere.Common;

namespace Everywhere.Chat;

public record ChatContextHistory(
    HumanizedDate Date,
    IReadOnlyList<ChatContext> Contexts
);