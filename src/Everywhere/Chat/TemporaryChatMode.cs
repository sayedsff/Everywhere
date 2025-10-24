namespace Everywhere.Chat;

/// <summary>
/// Specifies the mode for temporary chat contexts.
/// </summary>
public enum TemporaryChatMode
{
    [DynamicResourceKey(LocaleKey.TemporaryChatMode_Never)]
    Never,
    [DynamicResourceKey(LocaleKey.TemporaryChatMode_RememberLast)]
    RememberLast,
    [DynamicResourceKey(LocaleKey.TemporaryChatMode_Always)]
    Always
}