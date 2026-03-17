# v0.1.0

First stable release of GcaEditor.

This version provides a complete workflow for editing VW ambient lighting GCA files for MIB2/MIB2.5 systems, including zone management, ambient image placement, and a refined dark-themed UI.

## Highlights
- Full editing workflow from background import to GCA export
- Zone creation and positioning
- Ambient image import, placement, and management
- Undo/redo system
- Dirty state tracking
- Custom dark-themed dialog system
- Simplified drive side handling (LHD/RHD per session)

# Release Notes
## Improved
- Improved the choose car dialog layout by moving drive side selection next to custom mode and preserving the selected side in custom mode.
- Improved ambient workflow by locking editing to the drive side selected in the car chooser instead of allowing side switching during editing.
- Improved feature loading so only the selected drive side is loaded into the current editing session.
- Improved custom mode by keeping a fixed selected side while leaving background and GCA loading fully manual.
- Improved zone management by using a fixed available range (`0x00` to `0x0B`), ensuring deleted zones are always available again.
- Improved main window layout with better default sizing and minimum size constraints to prevent layout breakage.
- Improved viewer initial size to better utilize available space and reduce empty areas.
- Refined overall UI proportions for a more balanced and consistent editing experience.
- Improved viewer horizontal scrolling by reversing the direction to make it feel more intuitive.
- Improved UI consistency by translating the remaining French dialog text to English.

## Fixed
- Fixed car changes to prompt for saving unsaved work before resetting the current editor state.

## Cleanup
- Removed the runtime LHD/RHD switch from the ambient panel to simplify the editing flow and reduce confusion.
- Removed custom zone ID input to simplify the UI and rely on the known editable zone range.
