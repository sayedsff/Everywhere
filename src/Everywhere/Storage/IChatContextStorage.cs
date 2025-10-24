using Everywhere.Chat;
using Everywhere.Database;

namespace Everywhere.Storage;

/// <summary>
/// Abstraction for chat database operations used by the application.
/// Only exposes use-case level methods instead of leaking IQueryable,
/// which keeps the implementation swappable (SQLite, Dapper, etc.).
/// </summary>
public interface IChatContextStorage
{
    /// <summary>
    /// Soft-deletes a chat context by its ID. Implementations should mark <see cref="ChatContextEntity.IsDeleted"/> = true
    /// and update <see cref="ChatContextEntity.UpdatedAt"/> for LWW resolution. Children rows should also be treated accordingly.
    /// </summary>
    /// <param name="chatContextId">Context Id to delete.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task DeleteChatContextAsync(Guid chatContextId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries chat contexts metadata with simple cursor-based pagination.
    /// </summary>
    /// <param name="take">Maximum number of items to return (page size).</param>
    /// <param name="orderBy">Sort field.</param>
    /// <param name="descending">Whether to sort in descending order.</param>
    /// <param name="startAfterId">
    /// Optional "start after" cursor. When provided, the page starts after this ID using the same sort order.
    /// Implementations should use a deterministic tie-breaker (ID) when multiple rows share the same sort key.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Async sequence of contexts, ordered.</returns>
    IAsyncEnumerable<ChatContextMetadata> QueryChatContextsAsync(
        int take,
        ChatContextOrderBy orderBy,
        bool descending,
        Guid? startAfterId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a chat context with its full message tree.
    /// Throws if not found or is soft-deleted (unless includeDeleted is true).
    /// </summary>
    Task<ChatContext> GetChatContextAsync(Guid chatContextId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly persists the current in-memory ChatContext graph to the database.
    /// Strategy: snapshot-diff by Node.Id (Guid v7) => upsert/update; mark missing nodes as soft-deleted.
    /// - Updates ChatContextEntity (Topic, UpdatedAt).
    /// - Upserts ChatNodeEntity rows (Payload, ParentId, ChoiceChildId, Author, InsertKey).
    /// - Soft-deletes nodes that exist in DB but are absent from memory (unreachable).
    /// This method is idempotent and safe to call frequently.
    /// </summary>
    Task SaveChatContextAsync(ChatContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sort fields for querying chat contexts.
/// </summary>
public enum ChatContextOrderBy
{
    /// <summary>
    /// Sort by <see cref="ChatContextEntity.UpdatedAt"/>.
    /// </summary>
    UpdatedAt = 0,

    /// <summary>
    /// Sort by <see cref="ChatContextEntity.CreatedAt"/>.
    /// </summary>
    CreatedAt = 1,

    /// <summary>
    /// Sort by <see cref="ChatContextEntity.Topic"/> (culture-invariant).
    /// </summary>
    Topic = 2,
}