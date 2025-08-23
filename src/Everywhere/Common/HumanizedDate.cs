namespace Everywhere.Common;

public enum HumanizedDate
{
    [DynamicResourceKey(LocaleKey.HumanizedDate_Today)]
    Today,
    [DynamicResourceKey(LocaleKey.HumanizedDate_Yesterday)]
    Yesterday,
    [DynamicResourceKey(LocaleKey.HumanizedDate_LastWeek)]
    LastWeek,
    [DynamicResourceKey(LocaleKey.HumanizedDate_LastMonth)]
    LastMonth,
    [DynamicResourceKey(LocaleKey.HumanizedDate_LastYear)]
    LastYear,
    [DynamicResourceKey(LocaleKey.HumanizedDate_Earlier)]
    Earlier
}