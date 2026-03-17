# Release Notes
## Added
- Added a reusable dark themed message box dialog that matches the app UI.
- Added copy-to-clipboard support in message dialogs for easier debugging.
- Added routed keyboard shortcuts for the main setup and file actions (`Ctrl+O`, `Ctrl+S`, `Ctrl+B`, `Ctrl+Shift+C`).

## Improved
- Improved themed message box with better keyboard handling (Escape to close) and centered positioning.
- Improved the main window layout by moving setup and file actions to the menu bar while keeping state-based enable/disable behavior.
- Improved command handling so menu items and keyboard shortcuts now share the same state-based enable/disable logic.

## Fixed
- Fixed themed message box closing logic to avoid a WPF dialog result exception when closing with Escape.
- Fixed dark menu items to display shortcut text such as `Ctrl+O` and `Ctrl+S`.

## Cleanup
- Replaced native Windows message boxes with themed in-app dialogs for better visual consistency.
- Removed the large top action buttons to simplify the left panel and reduce visual clutter.
