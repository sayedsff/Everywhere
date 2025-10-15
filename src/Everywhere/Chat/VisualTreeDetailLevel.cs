namespace Everywhere.Chat;

public enum VisualTreeDetailLevel
{
	[DynamicResourceKey(LocaleKey.VisualTreeDetailLevel_Detailed)]
	Detailed,

	[DynamicResourceKey(LocaleKey.VisualTreeDetailLevel_Compact)]
	Compact,

	[DynamicResourceKey(LocaleKey.VisualTreeDetailLevel_Minimal)]
	Minimal
}

public static class VisualTreeDetailLevels
{
	public static VisualTreeDetailLevel[] All { get; } = Enum.GetValues<VisualTreeDetailLevel>();
}
