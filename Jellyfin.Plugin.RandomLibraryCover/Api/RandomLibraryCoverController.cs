using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RandomLibraryCover.Covers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RandomLibraryCover.Api;

/// <summary>
/// Serves the web client script and shuffles library covers.
/// </summary>
[ApiController]
[Route("RandomLibraryCover")]
public class RandomLibraryCoverController : ControllerBase
{
    // Same policy as Jellyfin's own image upload endpoint: library covers are shared by all users, so admins only.
    private const string RequiresElevation = "RequiresElevation";

    private readonly CoverShuffler _shuffler;

    /// <summary>
    /// Initializes a new instance of the <see cref="RandomLibraryCoverController"/> class.
    /// </summary>
    /// <param name="shuffler">The cover shuffler.</param>
    public RandomLibraryCoverController(CoverShuffler shuffler)
    {
        _shuffler = shuffler;
    }

    /// <summary>
    /// Gets the script that adds the "Random cover" menu item to the web client.
    /// </summary>
    /// <returns>The JavaScript file.</returns>
    [HttpGet("ClientScript")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetClientScript()
    {
        var stream = GetType().Assembly.GetManifestResourceStream(typeof(Plugin).Namespace + ".Web.randomLibraryCover.js");
        if (stream is null)
        {
            return NotFound();
        }

        return File(stream, "application/javascript");
    }

    /// <summary>
    /// Gives a library a new random cover from its own items' backdrops or thumbs.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The item the new cover comes from.</returns>
    [HttpPost("Libraries/{libraryId}/Shuffle")]
    [Authorize(Policy = RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ShuffleResult>> ShuffleLibrary([FromRoute] Guid libraryId, CancellationToken cancellationToken)
    {
        var library = _shuffler.GetLibrary(libraryId);
        if (library is null)
        {
            return NotFound("Not a library.");
        }

        var result = await _shuffler.ShuffleAsync(library, cancellationToken).ConfigureAwait(false);
        return result.Success ? result : UnprocessableEntity(result);
    }

    /// <summary>
    /// Gives every library that isn't excluded from automatic shuffling a new random cover.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many libraries got a new cover.</returns>
    [HttpPost("Libraries/Shuffle")]
    [Authorize(Policy = RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ShuffleAllResult>> ShuffleAll(CancellationToken cancellationToken)
    {
        var shuffled = await AutoShuffleTask.ShuffleIncludedAsync(_shuffler, null, cancellationToken).ConfigureAwait(false);
        return new ShuffleAllResult { Shuffled = shuffled };
    }

    /// <summary>
    /// Result of <see cref="ShuffleAll"/>.
    /// </summary>
    public class ShuffleAllResult
    {
        /// <summary>
        /// Gets or sets how many libraries got a new cover.
        /// </summary>
        public int Shuffled { get; set; }
    }
}
