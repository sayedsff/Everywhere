using Everywhere.Enums;

namespace Everywhere.Models;

public record ChatContextHistory(
    HumanizedDate Date,
    IReadOnlyList<ChatContext> Contexts
);