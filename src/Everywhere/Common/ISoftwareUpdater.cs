using System.ComponentModel;

namespace Everywhere.Common;

public interface ISoftwareUpdater : INotifyPropertyChanged
{
    Version CurrentVersion { get; }

    /// <summary>
    /// Gets a value indicating whether an update is available.
    /// </summary>
    DateTimeOffset? LastCheckTime { get; }

    /// <summary>
    /// Gets the latest version for update. Null if no update is available or the check has not been performed yet.
    /// </summary>
    Version? LatestVersion { get; }

    /// <summary>
    /// Runs the automatic update check in the background.
    /// </summary>
    void RunAutomaticCheckInBackground(TimeSpan interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually checks for updates asynchronously.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs the update process.
    /// </summary>
    /// <param name="progress">a 0-1 progress indicator for the update process</param>
    /// <param name="cancellationToken"></param>
    Task PerformUpdateAsync(IProgress<double> progress, CancellationToken cancellationToken = default);
}