# Security

The plugin adds three API endpoints:

- `POST /RandomLibraryCover/Libraries/{libraryId}/Shuffle` and `POST /RandomLibraryCover/Libraries/Shuffle` require an administrator, the same policy as Jellyfin's own image upload. They only copy images that already belong to the library's items onto the library itself.
- `GET /RandomLibraryCover/ClientScript` serves the plugin's static web client script without authentication.

It also adds a script tag to jellyfin-web's `index.html`, either in memory through the File Transformation plugin or on disk.

If you find a security problem, please don't open a public issue. Report it privately through [GitHub security advisories](https://github.com/oppahansi/random-library-cover/security/advisories/new). Fixes go into the latest plugin version for every supported Jellyfin line.
