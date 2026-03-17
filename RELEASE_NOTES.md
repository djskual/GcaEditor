# Release Notes
## Added
- Added a reusable dark themed message box dialog that matches the app UI.
- Added copy-to-clipboard support in message dialogs for easier debugging.

## Improved
- Improved themed message box with better keyboard handling (Escape to close) and centered positioning.

## Fixed
- Fixed themed message box closing logic to avoid a WPF dialog result exception when closing with Escape.

## Cleanup
- Replaced native Windows message boxes with themed in-app dialogs for better visual consistency.
