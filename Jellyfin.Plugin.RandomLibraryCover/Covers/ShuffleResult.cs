using System;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.RandomLibraryCover.Covers;

/// <summary>
/// The outcome of a shuffle.
/// </summary>
public class ShuffleResult
{
    private ShuffleResult()
    {
    }

    /// <summary>
    /// Gets a value indicating whether the library got a new cover.
    /// </summary>
    public bool Success { get; private init; }

    /// <summary>
    /// Gets why it didn't, if it didn't.
    /// </summary>
    public string? Error { get; private init; }

    /// <summary>
    /// Gets the id of the item the new cover comes from.
    /// </summary>
    public Guid? SourceItemId { get; private init; }

    /// <summary>
    /// Gets the name of the item the new cover comes from.
    /// </summary>
    public string? SourceName { get; private init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="source">The item the cover comes from.</param>
    /// <returns>The result.</returns>
    public static ShuffleResult Succeeded(BaseItem source) => new()
    {
        Success = true,
        SourceItemId = source.Id,
        SourceName = source is MediaBrowser.Controller.Entities.TV.Episode episode && !string.IsNullOrEmpty(episode.SeriesName)
            ? episode.SeriesName + " - " + episode.Name
            : source.Name
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">Why it failed.</param>
    /// <returns>The result.</returns>
    public static ShuffleResult Failed(string error) => new() { Success = false, Error = error };
}
