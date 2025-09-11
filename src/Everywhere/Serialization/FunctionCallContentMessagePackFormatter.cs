using MessagePack;
using MessagePack.Formatters;
using Microsoft.SemanticKernel;

namespace Everywhere.Serialization;

public class FunctionCallContentMessagePackFormatter : IMessagePackFormatter<FunctionCallContent?>
{
    public void Serialize(ref MessagePackWriter writer, FunctionCallContent? value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        writer.Write(value.Id);
        writer.Write(value.PluginName);
        writer.Write(value.FunctionName);
        writer.WriteMapHeader(value.Arguments?.Count ?? 0);
        if (value.Arguments != null)
        {
            foreach (var (key, val) in value.Arguments)
            {
                writer.Write(key);
                writer.Write(val?.ToString());
            }
        }
    }

    public FunctionCallContent Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var id = reader.ReadString();
        var pluginName = reader.ReadString();
        var functionName = reader.ReadString() ?? throw new MessagePackSerializationException("FunctionCallContent functionName cannot be null.");
        var argCount = reader.ReadMapHeader();
        var arguments = new KernelArguments();
        for (var j = 0; j < argCount; j++)
        {
            var key = reader.ReadString() ?? throw new MessagePackSerializationException("FunctionCallContent argument key cannot be null.");
            arguments[key] = reader.ReadString();
        }

        return new FunctionCallContent(functionName, pluginName, id, arguments);
    }
}