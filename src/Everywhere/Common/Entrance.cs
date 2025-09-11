#if !DEBUG
using Everywhere.Utilities;
#endif

using Serilog;
using Serilog.Formatting.Json;

namespace Everywhere.Common;

public static class Entrance
{
#if !DEBUG
    private static Mutex? AppMutex;
#endif

    /// <summary>
    /// Releases the application mutex. Only call this method when the application is exiting.
    /// </summary>
    public static void ReleaseMutex()
    {
#if !DEBUG
        if (AppMutex == null) return;

        AppMutex.ReleaseMutex();
        AppMutex.Dispose();
        AppMutex = null;
#endif
    }

    public static void Initialize(string[] args)
    {
        InitializeApplicationMutex(args);
        InitializeLogger();
        InitializeErrorHandling();
    }

    private static void InitializeApplicationMutex(string[] args)
    {
#if DEBUG
        // axaml designer may launch this code, so we need to set it to null.
        _ = args;
#else
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

    private static void InitializeLogger()
    {
        var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Everywhere");

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                new JsonFormatter(),
                Path.Combine(dataPath, "logs", ".jsonl"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    private static void InitializeErrorHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Logger.Error(e.ExceptionObject as Exception, "Unhandled Exception");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            if (e.Observed) return;

            Log.Logger.Error(e.Exception, "Unobserved Task Exception");
            e.SetObserved();
        };
    }
}