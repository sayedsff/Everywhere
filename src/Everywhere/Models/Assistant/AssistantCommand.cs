namespace Everywhere.Models;

public record AssistantCommand(
    string Command,
    DynamicResourceKey? DescriptionKey,
    string SystemPrompt,
    string UserPrompt,
    Func<string>? DefaultValueFactory = null
)
{
    public virtual bool Equals(AssistantCommand? other) => other is not null && Command == other.Command;

    public override int GetHashCode() => Command.GetHashCode();
}