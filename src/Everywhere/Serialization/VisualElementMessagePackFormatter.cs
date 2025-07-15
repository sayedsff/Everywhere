using MessagePack;

namespace Everywhere.Serialization;

public class VisualElementMessagePackFormatter : MessagePack.Formatters.IMessagePackFormatter<IVisualElement?>
{
    private static readonly IVisualElementContext VisualElementContext = ServiceLocator.Resolve<IVisualElementContext>();

    public IVisualElement? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var id = reader.ReadString() ?? throw new MessagePackSerializationException("VisualElement ID cannot be null.");
        return VisualElementContext.ElementFromId(id);
    }

    public void Serialize(ref MessagePackWriter writer, IVisualElement? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        var id = value.Id;
        if (string.IsNullOrEmpty(id))
        {
            throw new MessagePackSerializationException("VisualElement ID cannot be null or empty.");
        }

        writer.Write(id);
    }
}