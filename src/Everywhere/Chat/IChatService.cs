namespace Everywhere.Chat;

public interface IChatService
{
    Task SendMessageAsync(UserChatMessage message, CancellationToken cancellationToken);

    Task RetryAsync(ChatMessageNode node, CancellationToken cancellationToken);

    Task EditAsync(ChatMessageNode node, CancellationToken cancellationToken);
}