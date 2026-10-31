using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.RandomLibraryCover.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
#if NET9_0_OR_GREATER
using Jellyfin.Database.Implementations.Enums;
#endif

namespace Jellyfin.Plugin.RandomLibraryCover.Covers;

/// <summary>
/// Picks a random landscape image from a library's items and saves it as the library's cover (its primary image),
/// the same way an upload under "Edit images" does.
/// </summary>
public class CoverShuffler
{
    // How many random items are looked at before giving up. Most have a backdrop, so this is plenty.
    private const int CandidateCount = 50;

    // Library cards are 16:9. Anything between roughly 3:2 and 21:10 crops well; unknown sizes (0) are allowed.
    private const double MinAspect = 1.45;
    private const double MaxAspect = 2.1;

    // Same size Jellyfin uses for the generated library covers.
    private const int CoverWidth = 960;
    private const int CoverHeight = 540;

    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IImageProcessor _imageProcessor;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<CoverShuffler> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Last image used per library, so a shuffle never picks the cover that's already there.
    private readonly ConcurrentDictionary<Guid, string> _lastSource = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverShuffler"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="imageProcessor">Instance of the <see cref="IImageProcessor"/> interface.</param>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{T}"/> interface.</param>
    public CoverShuffler(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IImageProcessor imageProcessor,
        IApplicationPaths applicationPaths,
        ILogger<CoverShuffler> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _imageProcessor = imageProcessor;
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <summary>
    /// Gets all libraries.
    /// </summary>
    /// <returns>The libraries.</returns>
    public IReadOnlyList<CollectionFolder> GetLibraries()
    {
        return _libraryManager.GetUserRootFolder().Children.OfType<CollectionFolder>().ToList();
    }

    /// <summary>
    /// Gets a library by id.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <returns>The library, or null if the id isn't a library.</returns>
    public CollectionFolder? GetLibrary(Guid libraryId) => _libraryManager.GetItemById(libraryId) as CollectionFolder;

    /// <summary>
    /// Gives a library a new random cover.
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<ShuffleResult> ShuffleAsync(CollectionFolder library, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var source = config.Source;
        ImageType[] imageTypes = source switch
        {
            CoverSource.Backdrops => [ImageType.Backdrop],
            CoverSource.Thumbs => [ImageType.Thumb],
            _ => [ImageType.Backdrop, ImageType.Thumb]
        };

        var pick = PickImage(library, imageTypes);
        if (pick is null)
        {
            return ShuffleResult.Failed($"No usable {DescribeSource(source)} found in \"{library.Name}\".");
        }

        var (item, image) = pick.Value;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (bytes, mimeType) = await RenderCoverAsync(library, image.Path, config.DrawLibraryName, cancellationToken).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            await _providerManager.SaveImage(library, stream, mimeType, ImageType.Primary, null, cancellationToken).ConfigureAwait(false);
            await library.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        _lastSource[library.Id] = image.Path;
        _logger.LogInformation("Random Library Cover: \"{Library}\" now uses an image of \"{Item}\"", library.Name, item.Name);
        return ShuffleResult.Succeeded(item);
    }

    // Returns the image as the cover's bytes: with the library name drawn on it the way Jellyfin's own generated
    // covers look, or the untouched file. Read into memory either way, because the image saver owns the stream
    // and the source file must stay as it is.
    private async Task<(byte[] Bytes, string MimeType)> RenderCoverAsync(CollectionFolder library, string path, bool drawName, CancellationToken cancellationToken)
    {
        if (drawName && _imageProcessor.SupportsImageCollageCreation)
        {
            var output = Path.Combine(_applicationPaths.TempDirectory, "randomlibrarycover-" + Guid.NewGuid().ToString("N") + ".jpg");
            try
            {
                Directory.CreateDirectory(_applicationPaths.TempDirectory);
                _imageProcessor.CreateImageCollage(
                    new ImageCollageOptions { InputPaths = [path], OutputPath = output, Width = CoverWidth, Height = CoverHeight },
                    library.Name);
                if (File.Exists(output))
                {
                    return (await File.ReadAllBytesAsync(output, cancellationToken).ConfigureAwait(false), "image/jpeg");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Random Library Cover: couldn't draw the library name on the cover, using the plain image");
            }
            finally
            {
                File.Delete(output);
            }
        }

        return (await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false), GetMimeType(path));
    }

    private (BaseItem Item, ItemImageInfo Image)? PickImage(CollectionFolder library, ImageType[] imageTypes)
    {
        var candidates = _libraryManager.GetItemList(new InternalItemsQuery
        {
            ParentId = library.Id,
            Recursive = true,
            ImageTypes = imageTypes,
            IsVirtualItem = false,
            OrderBy = [(ItemSortBy.Random, SortOrder.Ascending)],
            Limit = CandidateCount
        });

        _lastSource.TryGetValue(library.Id, out var lastPath);
        foreach (var item in candidates)
        {
            var images = imageTypes
                .SelectMany(item.GetImages)
                .Where(image => IsUsable(image) && !string.Equals(image.Path, lastPath, StringComparison.Ordinal))
                .ToList();
            if (images.Count > 0)
            {
                return (item, images[RandomNumberGenerator.GetInt32(images.Count)]);
            }
        }

        return null;
    }

    private static bool IsUsable(ItemImageInfo image)
    {
        if (!image.IsLocalFile || !File.Exists(image.Path))
        {
            return false;
        }

        if (image.Width <= 0 || image.Height <= 0)
        {
            return true;
        }

        var aspect = (double)image.Width / image.Height;
        return aspect is >= MinAspect and <= MaxAspect;
    }

    private static string DescribeSource(CoverSource source) => source switch
    {
        CoverSource.Backdrops => "backdrops",
        CoverSource.Thumbs => "thumbs",
        _ => "backdrops or thumbs"
    };

    private static string GetMimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };
}
