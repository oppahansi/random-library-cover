using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RandomLibraryCover.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RandomLibraryCover.Covers;

/// <summary>
/// Shuffles the covers of all included libraries when the configured interval has passed. It runs every
/// hour and checks the interval itself, so changing the interval on the settings page takes effect without
/// touching the task's triggers.
/// </summary>
public class AutoShuffleTask : IScheduledTask, IConfigurableScheduledTask
{
    // Hourly runs drift by a few seconds, don't let that push a 6 hour interval to 7 hours.
    private static readonly TimeSpan Slack = TimeSpan.FromMinutes(5);

    private readonly CoverShuffler _shuffler;
    private readonly ILogger<AutoShuffleTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoShuffleTask"/> class.
    /// </summary>
    /// <param name="shuffler">The cover shuffler.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{T}"/> interface.</param>
    public AutoShuffleTask(CoverShuffler shuffler, ILogger<AutoShuffleTask> logger)
    {
        _shuffler = shuffler;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Random Library Cover: automatic shuffle";

    /// <inheritdoc />
    public string Key => "RandomLibraryCoverAutoShuffle";

    /// <inheritdoc />
    public string Description => "Gives the libraries new random covers at the interval set on the plugin's settings page.";

    /// <inheritdoc />
    public string Category => "Random Library Cover";

    /// <inheritdoc />
    public bool IsHidden => true;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => false;

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var config = plugin.Configuration;
        var interval = PluginConfiguration.ToTimeSpan(config.AutoShuffleInterval);
        if (interval is null || DateTime.UtcNow - config.LastAutoShuffle < interval.Value - Slack)
        {
            return;
        }

        var shuffled = await ShuffleIncludedAsync(_shuffler, progress, cancellationToken).ConfigureAwait(false);

        config.LastAutoShuffle = DateTime.UtcNow;
        plugin.SaveConfiguration();
        _logger.LogInformation("Random Library Cover: automatic shuffle gave {Count} libraries a new cover", shuffled);
    }

    /// <summary>
    /// Shuffles every library that isn't excluded on the settings page.
    /// </summary>
    /// <param name="shuffler">The cover shuffler.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many libraries got a new cover.</returns>
    public static async Task<int> ShuffleIncludedAsync(CoverShuffler shuffler, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var excluded = (Plugin.Instance?.Configuration.ExcludedLibraryIds ?? [])
            .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
            .ToHashSet();
        var libraries = shuffler.GetLibraries().Where(library => !excluded.Contains(library.Id)).ToList();

        var shuffled = 0;
        for (var i = 0; i < libraries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await shuffler.ShuffleAsync(libraries[i], cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                shuffled++;
            }

            progress?.Report(100.0 * (i + 1) / libraries.Count);
        }

        return shuffled;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
#if NET9_0_OR_GREATER
                Type = TaskTriggerInfoType.IntervalTrigger,
#else
                Type = TaskTriggerInfo.TriggerInterval,
#endif
                IntervalTicks = TimeSpan.FromHours(1).Ticks
            }
        ];
    }
}
