using Everywhere.Chat;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.SemanticKernel;

namespace Everywhere.Serialization;

public class FunctionResultContentMessagePackFormatter : IMessagePackFormatter<FunctionResultContent?>
{
    public void Serialize(ref MessagePackWriter writer, FunctionResultContent? value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        writer.Write(value.CallId);
        writer.Write(value.PluginName);
        writer.Write(value.FunctionName);

        writer.WriteArrayHeader(2);
        switch (value.Result)
        {
            case ChatAttachment chatAttachment:
            {
                var formatter = options.Resolver.GetFormatterWithVerify<ChatAttachment>();
                writer.Write(1);
                formatter.Serialize(ref writer, chatAttachment, options);
                break;
            }
            default:
            {
                writer.Write(0);
                writer.Write(value.Result?.ToString());
                break;
            }
        }
    }

    public FunctionResultContent Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var callId = reader.ReadString();
        var pluginName = reader.ReadString();
        var functionName = reader.ReadString();

        if (reader.ReadArrayHeader() != 2)
        {
            throw new MessagePackSerializationException("FunctionResultContent array header must be 2.");
        }

        var valueType = reader.ReadInt32();
        object? value = null;
        switch (valueType)
        {
            case 0:
            {
                value = reader.ReadString();
                break;
            }
            case 1:
            {
                var formatter = options.Resolver.GetFormatterWithVerify<ChatAttachment>();
                value = formatter.Deserialize(ref reader, options);
                break;
            }
        }

        return new FunctionResultContent(functionName, pluginName, callId, value);
    }
}