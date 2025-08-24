using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using Everywhere.Rpc;
using MessagePack;

namespace Everywhere.Watchdog;

public static class Program
{
    private static readonly ConcurrentDictionary<long, Process> MonitoredProcesses = new();

    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            await Console.Error.WriteLineAsync("No arguments provided. Exiting.");
            Environment.Exit(1);
        }

        var pipeName = args[0];
        Console.WriteLine($"Started. Waiting for main application to connect with pipe name: {pipeName}");

        await using var clientStream = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.In,
            PipeOptions.Asynchronous);

        try
        {
            await clientStream.ConnectAsync(5000).ConfigureAwait(false);
            Console.WriteLine("Main application connected. Listening for commands...");

            var lengthBuffer = new byte[4];
            while (clientStream.IsConnected)
            {
                var bytesRead = await clientStream.ReadAsync(lengthBuffer.AsMemory(0, 4));
                if (bytesRead < 4) break;

                var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                var messageBuffer = new byte[messageLength];
                await clientStream.ReadExactlyAsync(messageBuffer, 0, messageLength);

                var command = MessagePackSerializer.Deserialize<WatchdogCommand>(messageBuffer);
                ProcessCommand(command);
            }
        }
        catch (TimeoutException)
        {
            await Console.Error.WriteLineAsync("Timeout waiting for main application to connect. Exiting...");
        }
        catch (IOException)
        {
            await Console.Error.WriteLineAsync("Connection lost. Main application has likely exited.");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"An unexpected error occurred: {ex.Message}");
        }
        finally
        {
            TerminateAllSubprocesses();
            Console.WriteLine("Job finished. Exiting...");
        }
    }

    private static void ProcessCommand(WatchdogCommand? command)
    {
        switch (command)
        {
            case RegisterSubprocessCommand registerCmd:
                try
                {
                    var process = Process.GetProcessById((int)registerCmd.ProcessId);
                    MonitoredProcesses.TryAdd(process.Id, process);
                    Console.WriteLine($"Registered process '{process.ProcessName}' (ID: {process.Id}).");
                }
                catch (ArgumentException)
                {
                    Console.WriteLine($"Process with ID {registerCmd.ProcessId} not found.");
                }
                break;

            case UnregisterSubprocessCommand unregisterCmd:
                if (MonitoredProcesses.TryRemove(unregisterCmd.ProcessId, out var p))
                {
                    Console.WriteLine($"Unregistered process '{p.ProcessName}' (ID: {p.Id}).");
                }
                break;
        }
    }

    private static void TerminateAllSubprocesses()
    {
        Console.WriteLine($"Terminating {MonitoredProcesses.Count} monitored process(es)...");
        foreach (var pair in MonitoredProcesses)
        {
            try
            {
                if (!pair.Value.HasExited)
                {
                    Console.WriteLine($"Killing process '{pair.Value.ProcessName}' (ID: {pair.Key}).");
                    pair.Value.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to terminate process {pair.Key}: {ex.Message}");
            }
        }

        MonitoredProcesses.Clear();
    }
}