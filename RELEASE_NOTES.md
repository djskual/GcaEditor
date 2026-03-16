# Release Notes
## Added
- Added a maximum undo/redo history limit of 100 states to prevent unbounded memory usage during long editing sessions.

## Improved
- Improved update checking by parsing and comparing GitHub tags instead of relying on the first tag returned by the API.
- Improved version comparison so prerelease tags such as `beta` are handled more reliably.
- Improved viewer zoom behavior by keeping the current viewport centered when zooming in or out.
- Improved ambient image move behavior by adding a small drag threshold to prevent accidental moves.
- Improved ambient overlay updates by refreshing only the affected slot instead of rerendering all ambient images.

## Fixed
- Fixed a typo in the project asset declarations for `bright.png`.
- Fixed a false XAML designer resource error by moving the viewer brightness icon mask loading out of XAML.

## Cleanup
- Cleaned up embedded asset handling to make resource loading more explicit and easier to maintain.
