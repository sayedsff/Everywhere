namespace Everywhere.Enums;

public enum ChatFloatingWindowPinMode
{
    [DynamicResourceKey(LocaleKey.ChatFloatingWindowPinMode_RemeberLast)]
    RememberLast,

    [DynamicResourceKey(LocaleKey.ChatFloatingWindowPinMode_AlwaysPinned)]
    AlwaysPinned,

    [DynamicResourceKey(LocaleKey.ChatFloatingWindowPinMode_AlwaysUnpinned)]
    AlwaysUnpinned,

    [DynamicResourceKey(LocaleKey.ChatFloatingWindowPinMode_PinOnInput)]
    PinOnInput
}