using Everywhere.Models;

namespace Everywhere.Interfaces;

public interface IChatService
{
    Task SendMessageAsync(UserChatMessage userMessage, CancellationToken cancellationToken);

    Task RetryAsync(ChatMessageNode node, CancellationToken cancellationToken);

    Task EditAsync(ChatMessageNode node, CancellationToken cancellationToken);
}