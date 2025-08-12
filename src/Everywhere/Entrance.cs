#if !DEBUG
using Everywhere.Utilities;
#endif

using Serilog;

namespace Everywhere;

public static class Entrance
{
#if !DEBUG
    private static Mutex? AppMutex;
#endif

    public static void Initialize(string[] args)
    {
        InitializeApplicationMutex(args);
        InitializeErrorHandling();
    }

    private static void InitializeApplicationMutex(string[] args)
    {
#if !DEBUG  // axaml designer may launch this code, so we need to set it to null.
        AppMutex = new Mutex(true, "EverywhereAppMutex", out var createdNew);
        if (!createdNew)
        {
            if (!args.Contains("--autorun"))
            {
                NativeMessageBox.Show(
                    "Info",
                    "Everywhere is already running. Please check your system tray for the application window.",
                    NativeMessageBoxButtons.Ok,
                    NativeMessageBoxIcon.Information);
            }

            Environment.Exit(0);
        }
#endif
    }

    private static void InitializeErrorHandling()
    {
        const string OutputTemplate =
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] " +
            "[{SourceContext}] {Message:lj}{NewLine}{Exception}";
        var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Everywhere");

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: OutputTemplate)
            .WriteTo.File(
                Path.Combine(dataPath, "logs", ".log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: OutputTemplate)
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Logger.Error(e.ExceptionObject as Exception, "Unhandled Exception");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Logger.Error(e.Exception, "Unobserved Task Exception");
            e.SetObserved();
        };
    }
}