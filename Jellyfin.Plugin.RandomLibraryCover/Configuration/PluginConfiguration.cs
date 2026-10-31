using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RandomLibraryCover.Configuration;

/// <summary>
/// Which images of the library's items can become the cover. All of them are landscape, like the library cards.
/// </summary>
public enum CoverSource
{
    /// <summary>
    /// Backdrops and thumbs.
    /// </summary>
    BackdropsAndThumbs,

    /// <summary>
    /// Backdrops only (plain artwork, usually without a title).
    /// </summary>
    Backdrops,

    /// <summary>
    /// Thumbs only (landscape artwork, often with the title on it).
    /// </summary>
    Thumbs
}

/// <summary>
/// How often the covers are shuffled automatically.
/// </summary>
public enum ShuffleInterval
{
    /// <summary>
    /// Never, only from the card menu.
    /// </summary>
    Off,

    /// <summary>
    /// Every hour.
    /// </summary>
    Hourly,

    /// <summary>
    /// Every 3 hours.
    /// </summary>
    Every3Hours,

    /// <summary>
    /// Every 6 hours.
    /// </summary>
    Every6Hours,

    /// <summary>
    /// Every 12 hours.
    /// </summary>
    Every12Hours,

    /// <summary>
    /// Once a day.
    /// </summary>
    Daily,

    /// <summary>
    /// Every 3 days.
    /// </summary>
    Every3Days,

    /// <summary>
    /// Once a week.
    /// </summary>
    Weekly
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets which images can become the cover.
    /// </summary>
    public CoverSource Source { get; set; } = CoverSource.BackdropsAndThumbs;

    /// <summary>
    /// Gets or sets a value indicating whether the library name is drawn on the cover, like on Jellyfin's generated covers.
    /// </summary>
    public bool DrawLibraryName { get; set; } = true;

    /// <summary>
    /// Gets or sets how often the covers are shuffled automatically.
    /// </summary>
    public ShuffleInterval AutoShuffleInterval { get; set; } = ShuffleInterval.Off;

    /// <summary>
    /// Gets or sets the libraries left out of automatic shuffling. New libraries are included by default.
    /// </summary>
#pragma warning disable CA1819 // Plugin configuration is XML serialized, which needs an array.
    public string[] ExcludedLibraryIds { get; set; } = [];
#pragma warning restore CA1819

    /// <summary>
    /// Gets or sets when the covers were last shuffled automatically (UTC).
    /// </summary>
    public DateTime LastAutoShuffle { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Gets the length of an interval.
    /// </summary>
    /// <param name="interval">The interval.</param>
    /// <returns>The length, or null for <see cref="ShuffleInterval.Off"/>.</returns>
    public static TimeSpan? ToTimeSpan(ShuffleInterval interval) => interval switch
    {
        ShuffleInterval.Hourly => TimeSpan.FromHours(1),
        ShuffleInterval.Every3Hours => TimeSpan.FromHours(3),
        ShuffleInterval.Every6Hours => TimeSpan.FromHours(6),
        ShuffleInterval.Every12Hours => TimeSpan.FromHours(12),
        ShuffleInterval.Daily => TimeSpan.FromDays(1),
        ShuffleInterval.Every3Days => TimeSpan.FromDays(3),
        ShuffleInterval.Weekly => TimeSpan.FromDays(7),
        _ => null
    };
}
