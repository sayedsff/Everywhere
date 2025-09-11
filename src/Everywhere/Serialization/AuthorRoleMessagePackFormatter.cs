using MessagePack;
using MessagePack.Formatters;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Everywhere.Serialization;

public class AuthorRoleMessagePackFormatter : IMessagePackFormatter<AuthorRole>
{
    public AuthorRole Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return new AuthorRole(reader.ReadString() ?? throw new MessagePackSerializationException("AuthorRole label cannot be null."));
    }

    public void Serialize(ref MessagePackWriter writer, AuthorRole value, MessagePackSerializerOptions options)
    {
        writer.Write(value.Label);
    }
}