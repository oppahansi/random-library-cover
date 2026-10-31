# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [semantic versioning](https://semver.org).

## [Unreleased]

## [1.0.1] - 2026-10-31

### Fixed

- Works next to other plugins that use the File Transformation plugin. Every plugin used the same transformation id, so File Transformation kept only the first one and the menu item could go missing.

## [1.0.0] - 2026-10-31

### Added

- "Random cover" item in the ⋮ menu of the library cards in My Media on the home screen (administrators only). It saves a random backdrop or thumb from the library's items as the library's cover and updates the card right away.
- The library name is drawn on the cover with Jellyfin's own cover renderer, like the generated covers (can be turned off).
- Setting to pick covers from backdrops, thumbs or both. Only landscape images are used, and never the same image twice in a row.
- Optional automatic shuffle every hour, 3, 6 or 12 hours, daily, every 3 days or weekly, for selected libraries, plus a "Shuffle now" button.
- Endpoints `POST /RandomLibraryCover/Libraries/{libraryId}/Shuffle` and `POST /RandomLibraryCover/Libraries/Shuffle`.
- Script injection through the File Transformation plugin when it's installed, otherwise into jellyfin-web's `index.html` (locked while it's changed, left alone if it looks damaged).
- Builds for Jellyfin 10.9, 10.10, 10.11, 12.0, 12.1, 12.2, 12.3 and 12.4.

[Unreleased]: https://github.com/oppahansi/random-library-cover/compare/v1.0.1-jellyfin-12.4...HEAD
[1.0.1]: https://github.com/oppahansi/random-library-cover/releases/tag/v1.0.1-jellyfin-12.4
[1.0.0]: https://github.com/oppahansi/random-library-cover/releases/tag/v1.0.0-jellyfin-12.4
