using Everywhere.Models;

namespace Everywhere.Enums;

public enum HumanizedDate
{
    [DynamicResourceKey("HumanizedDate_Today")]
    Today,
    [DynamicResourceKey("HumanizedDate_Yesterday")]
    Yesterday,
    [DynamicResourceKey("HumanizedDate_LastWeek")]
    LastWeek,
    [DynamicResourceKey("HumanizedDate_LastMonth")]
    LastMonth,
    [DynamicResourceKey("HumanizedDate_LastYear")]
    LastYear,
    [DynamicResourceKey("HumanizedDate_Earlier")]
    Earlier
}