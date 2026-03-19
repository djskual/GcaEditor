# Release Notes

## Added
- Added a complete Settings window with categorized sections (General, Editor, Viewer, Updates).
- Added persistent application settings stored in a local configuration file.
- Added option to automatically check for updates at startup.
- Added option to include prerelease versions in update checks.
- Added option to remember window size and position between sessions.
- Added option to confirm before resetting the current workspace.
- Added options to confirm before deleting zones and ambient images.
- Added configurable default save folder for GCA export.
- Added automatic fallback to the currently loaded GCA directory when no default save folder is defined.
- Added option to auto-load the last project at startup.
- Added full restoration of last project context:
  - drive side (LHD/RHD)
  - background image (if still available)
  - GCA file (if still available)
  - OEM ambient features (for car-based projects)
- Added restoration of manually imported ambient files for custom projects (if files still exist).

## Improved
- Improved save dialog behavior with smarter default directory selection.
- Improved startup workflow by restoring the previous working context.
- Improved robustness when loading missing or moved files (background, GCA, ambient).
- Improved overall UX with a more structured and scalable settings interface.
- Improved settings feedback by applying changes immediately after saving.

## Fixed
- Fixed potential crashes when loading invalid or unsupported GCA files.
- Fixed settings not being consistently applied across sessions.
- Fixed edge cases when restoring incomplete or partially missing projects.

## Cleanup
- Removed unused and non-functional settings to keep the configuration interface clean.
- Refactored settings persistence logic for better maintainability.
- Simplified project state management and snapshot handling.
