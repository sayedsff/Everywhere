namespace Everywhere.Chat.Permissions;

/// <summary>
/// Represents the user's consent decision for a permission request.
/// </summary>
public enum ConsentDecision
{
    [DynamicResourceKey(LocaleKey.ConsentDecision_Deny)]
    Deny = 0,
    [DynamicResourceKey(LocaleKey.ConsentDecision_AllowOnce)]
    AllowOnce = 1,
    [DynamicResourceKey(LocaleKey.ConsentDecision_AllowSession)]
    AllowSession = 2,
    [DynamicResourceKey(LocaleKey.ConsentDecision_AlwaysAllow)]
    AlwaysAllow = 3
}