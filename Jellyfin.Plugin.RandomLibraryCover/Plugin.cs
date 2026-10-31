using System;
using System.Collections.Generic;
using Jellyfin.Plugin.RandomLibraryCover.Configuration;
using Jellyfin.Plugin.RandomLibraryCover.Injection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.RandomLibraryCover;

/// <summary>
/// The Random Library Cover plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        _applicationPaths = applicationPaths;
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Random Library Cover";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("b94cd594-5ec9-4552-b1b4-455d524fe82e");

    /// <inheritdoc />
    public override string Description => "Adds a \"Random cover\" item to the library card menu that picks a new library cover from the library's own artwork.";

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = "RandomLibraryCover",
            DisplayName = "Random Library Cover",
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
            EnableInMainMenu = true,
            MenuSection = "server",
            MenuIcon = "shuffle"
        };
    }

    /// <inheritdoc />
    public override void OnUninstalling()
    {
        // Undo the index.html fallback so jellyfin-web doesn't keep requesting a script that no longer exists.
        IndexHtml.TryRemoveFromDisk(_applicationPaths.WebPath, out _);
        base.OnUninstalling();
    }
}
