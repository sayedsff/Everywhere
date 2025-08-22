using System.ComponentModel;
using System.Management.Automation.Runspaces;
using Everywhere.Enums;
using Everywhere.Models;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell;
using Microsoft.SemanticKernel;

namespace Everywhere.Windows.ChatPlugins;

public class PowerShellPlugin : BuiltInChatPlugin
{
    public override LucideIconKind? Icon => LucideIconKind.SquareTerminal;

    private readonly ILogger<PowerShellPlugin> _logger;

    public PowerShellPlugin(ILogger<PowerShellPlugin> logger) : base("Shell")
    {
        _logger = logger;

        _functions.Add(new AnonymousChatFunction(
            ExecutePowerShellScriptAsync,
            ChatFunctionPermissions.ShellExecute));
    }

    [KernelFunction("execute_powershell_script")]
    [Description(
        "Execute a signle or multi-line PowerShell script and obtain its output. You MUST provide a concise description for user, explaining what you are doing.")]
    private async Task<string> ExecutePowerShellScriptAsync(string description, string script)
    {
        _logger.LogInformation("Executing PowerShell script with description: {Description}\nScript: {Script}", description, script);

        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("Script cannot be null or empty.", nameof(script));
        }

        // Use PowerShell to execute the script and return the output
        var iss = InitialSessionState.CreateDefault2();
        iss.ExecutionPolicy = ExecutionPolicy.Bypass;
        using var powerShell = System.Management.Automation.PowerShell.Create(iss);
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