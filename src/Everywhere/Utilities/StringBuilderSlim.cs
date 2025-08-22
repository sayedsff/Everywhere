namespace Everywhere.Utilities;

public ref struct StringBuilderSlim(Span<char> buffer)
{
    private Span<char> _buffer = buffer;
    private int _length;

    public StringBuilderSlim Append(ReadOnlySpan<char> value)
    {
        if (_length + value.Length > _buffer.Length)
        {
            throw new InvalidOperationException("Not enough space in the buffer to append the value.");
        }

        value.CopyTo(_buffer[_length..]);
        _length += value.Length;
        return this;
    }

    public StringBuilderSlim Append(char value)
    {
        if (_length >= _buffer.Length)
        {
            throw new InvalidOperationException("Not enough space in the buffer to append the character.");
        }

        _buffer[_length] = value;
        _length++;
        return this;
    }

    public StringBuilderSlim AppendLine(ReadOnlySpan<char> value)
    {
        Append(value);
        Append(Environment.NewLine);
        return this;
    }

    public override string ToString() => _buffer.ToString();
}