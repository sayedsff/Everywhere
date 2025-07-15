using System.Text;

namespace Everywhere.Markdown;

public readonly record struct ObservableStringBuilderChangedEventArgs(string NewString, int StartIndex, int Length);

public delegate void ObservableStringBuilderChangedEventHandler(in ObservableStringBuilderChangedEventArgs e);

public class ObservableStringBuilder
{
    public int Length => stringBuilder.Length;

    public event ObservableStringBuilderChangedEventHandler? Changed;

    private readonly StringBuilder stringBuilder = new();

    public ObservableStringBuilder Append(string? value)
    {
        if (string.IsNullOrEmpty(value)) return this;
        stringBuilder.Append(value);
        Changed?.Invoke(
            new ObservableStringBuilderChangedEventArgs(
                ToString(),
                stringBuilder.Length - value.Length,
                value.Length));
        return this;
    }

    public ObservableStringBuilder Clear()
    {
        var length = stringBuilder.Length;
        stringBuilder.Clear();
        Changed?.Invoke(
            new ObservableStringBuilderChangedEventArgs(
                string.Empty,
                0,
                length));
        return this;
    }

    public override string ToString()
    {
        return stringBuilder.ToString();
    }
}