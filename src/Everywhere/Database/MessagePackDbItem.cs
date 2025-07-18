using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using MessagePack;

namespace Everywhere.Database;

public class MessagePackDbItem<TItem> where TItem : class
{
    [System.ComponentModel.DataAnnotations.Key]
    public int Id { get; init; }

    [Column(TypeName = "BLOB")]
    public required byte[] SerializedData { get; set; }

    [NotMapped]
    public virtual TItem Item
    {
        get => MessagePackSerializer.Deserialize<TItem>(SerializedData);
        set => SerializedData = MessagePackSerializer.Serialize(value);
    }

    /// <summary>
    /// For EF Core
    /// </summary>
    public MessagePackDbItem() { }

    [SetsRequiredMembers]
    public MessagePackDbItem(TItem item)
    {
        SerializedData = MessagePackSerializer.Serialize(item);
    }
}