# Contributing

Thanks for helping improve Random Library Cover. Bug reports, feature ideas and pull requests are all welcome.

## Reporting bugs

Open an [issue](https://github.com/oppahansi/random-library-cover/issues/new/choose) and include:

- your Jellyfin version and how it runs (Docker, Linux package, Windows, …)
- the plugin version, shown under **Dashboard → Plugins**
- what you expected and what happened instead
- related lines from the Jellyfin log, if any (**Dashboard → Logs**)

Also say whether the File Transformation plugin is installed, and include errors from the browser console (F12) if the menu item doesn't show up. Please remove file paths or library names you don't want to share.

## Development setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Clone the repository and build:

   ```sh
   dotnet build Jellyfin.Plugin.RandomLibraryCover -c Release
   ```

3. To try your build, copy `Jellyfin.Plugin.RandomLibraryCover/bin/Release/<framework>/Jellyfin.Plugin.RandomLibraryCover.dll` into a folder under your test server's `plugins/` directory and restart Jellyfin, then reload the web client. A throwaway Docker container works well for this:

   ```sh
   docker run --rm -p 8096:8096 \
     -v "$PWD/test-config:/config" \
     -v /path/to/some/media:/media:ro \
     jellyfin/jellyfin:12.3
   ```

   Build with `-p:JellyfinVersion=<version>` that matches the server image. See [Building](README.md#building).

## Project layout

| Path | Contents |
|---|---|
| `Jellyfin.Plugin.RandomLibraryCover/Plugin.cs` | Plugin entry point and settings page, removes the `index.html` tag on uninstall |
| `Jellyfin.Plugin.RandomLibraryCover/Api/` | The `/RandomLibraryCover` controller: shuffle endpoints and client script |
| `Jellyfin.Plugin.RandomLibraryCover/Covers/` | Picks and saves the covers, and the hourly task behind automatic shuffling |
| `Jellyfin.Plugin.RandomLibraryCover/Configuration/` | Settings and the settings page |
| `Jellyfin.Plugin.RandomLibraryCover/Injection/` | Startup task that adds the script tag, via File Transformation or `index.html` on disk |
| `Jellyfin.Plugin.RandomLibraryCover/Web/randomLibraryCover.js` | The web client script that adds the menu item to library cards |
| `targets.json` | Jellyfin versions that get a release build |
| `scripts/package.sh` | Builds and zips one Jellyfin version |
| `scripts/update-manifest.py` | Updates `manifest.json`, the Jellyfin plugin repository file |

The client script relies on jellyfin-web's markup: library cards in `.homeSectionsContainer` with `data-type="CollectionFolder"`, the card's `[data-action="menu"]` button, and the action sheet (`.actionSheet`, `.actionSheetMenuItem[data-id="editimages"]`). If a new jellyfin-web version changes it, that's the place to look.

## Pull requests

- Keep changes focused. Open an issue first for larger features so we can agree on the approach.
- Make sure every supported Jellyfin version still builds. CI checks this on every pull request, and locally you can run:

  ```sh
  for v in $(jq -r '.[]' targets.json); do scripts/package.sh "$v"; done
  ```

- If the Jellyfin API differs between versions, use the built-in target framework symbols (`#if NET9_0_OR_GREATER`, `#if NET10_0_OR_GREATER`) instead of dropping support for older versions.
- Warnings are treated as errors. Match the existing code style; `.editorconfig` covers the basics.
- Add an entry under `## [Unreleased]` in `CHANGELOG.md` for user-facing changes.
- Don't edit `manifest.json` by hand. The release workflow maintains it.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
