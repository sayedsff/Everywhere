using System.ComponentModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Everywhere.Chat.Plugins;
using Everywhere.I18N;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell;
using Microsoft.SemanticKernel;

namespace Everywhere.Windows.Chat.Plugins;

public class PowerShellPlugin : BuiltInChatPlugin
{
    public override DynamicResourceKeyBase HeaderKey { get; } = new DynamicResourceKey(LocaleKey.NativeChatPlugin_Shell_Header);

    public override DynamicResourceKeyBase DescriptionKey { get; } = new DynamicResourceKey(LocaleKey.NativeChatPlugin_Shell_Description);

    public override LucideIconKind? Icon => LucideIconKind.SquareTerminal;

    public override string BeautifulIcon => "avares://Everywhere.Windows/Assets/Icons/PowerShell.svg";

    private readonly ILogger<PowerShellPlugin> _logger;

    public PowerShellPlugin(ILogger<PowerShellPlugin> logger) : base("powershell")
    {
        _logger = logger;

        // Load powershell module
        // from: https://github.com/PowerShell/PowerShell/issues/25793
        var path = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
#if NET9_0
        var modulesPath = Path.Combine(path ?? ".", "runtimes", "win", "lib", "net9.0", "Modules");
#else
        #error Target framework not supported
#endif
        Environment.SetEnvironmentVariable(
            "PSModulePath",
            $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\Modules")};" +
            $"{modulesPath};" + // Import application auto-contained modules
            Environment.GetEnvironmentVariable("PSModulePath"));

        _functions.Add(
            new NativeChatFunction(
                ExecutePowerShellScriptAsync,
                ChatFunctionPermissions.ShellExecute));
    }

    [KernelFunction("execute_powershell_script")]
    [Description("Execute PowerShell script and obtain its output.")]
    private async Task<string> ExecutePowerShellScriptAsync(
        [FromKernelServices] IChatPluginUserInterface userInterface,
        [Description("A concise description for user, explaining what you are doing")] string description,
        [Description("Signle or multi-line")] string script)
    {
        _logger.LogDebug("Executing PowerShell script with description: {Description}", description);

        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("Script cannot be null or empty.", nameof(script));
        }

        // Use PowerShell to execute the script and return the output
        var iss = InitialSessionState.CreateDefault2();
        iss.ExecutionPolicy = ExecutionPolicy.Bypass;
        // Set to ConstrainedLanguage to enhance security
        iss.LanguageMode = PSLanguageMode.ConstrainedLanguage;
        using var powerShell = PowerShell.Create(iss);
        powerShell.AddScript(script);

        var results = await powerShell.InvokeAsync();
        if (powerShell.HadErrors)
        {
            var errorMessages = powerShell.Streams.Error.Select(e => e.ToString());
            throw new InvalidOperationException($"PowerShell script execution failed: {string.Join(Environment.NewLine, errorMessages)}");
        }

        return string.Join(Environment.NewLine, results.Select(r => r.ToString()));
    }
}