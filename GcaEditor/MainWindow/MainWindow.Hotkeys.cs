using GcaEditor.Models;
using System.Windows.Input;

namespace GcaEditor;

public partial class MainWindow
{
    private bool _ambientNudgeActive;
    private int _ambientNudgeIndex;
    private ushort _ambientNudgeStartX;
    private ushort _ambientNudgeStartY;
    private bool _ambientNudgeUndoPushed;

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Escape cancels Place or Move
        if (e.Key == Key.Escape && (_placingAmbientIndex != null || _movingAmbientIndex != null))
        {
            if (_placingAmbientIndex != null) ExitAmbientPlacementMode();
            if (_movingAmbientIndex != null) ExitAmbientMoveMode();
            e.Handled = true;
            return;
        }

        // Arrow keys nudge selected ambient image (when not in Place/Move)
        if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
        {
            if (_placingAmbientIndex != null || _movingAmbientIndex != null)
                return;

            if (!_uiReady || Viewer == null || _doc == null)
                return;

            if (AmbientList.SelectedItem is not AmbientSlotItem it)
                return;

            int idx = it.Index;

            var img = _doc.Images.FirstOrDefault(x => x.Id == (ushort)idx);
            if (img == null)
                return;

            // Start a nudge sequence only once (ignore repeats for undo)
            if (!_ambientNudgeActive)
            {
                _ambientNudgeActive = true;
                _ambientNudgeIndex = idx;
                _ambientNudgeStartX = img.X;
                _ambientNudgeStartY = img.Y;
                _ambientNudgeUndoPushed = false;
            }

            // If selection changed while holding keys, cancel sequence and start new
            if (_ambientNudgeIndex != idx)
            {
                _ambientNudgeActive = true;
                _ambientNudgeIndex = idx;
                _ambientNudgeStartX = img.X;
                _ambientNudgeStartY = img.Y;
                _ambientNudgeUndoPushed = false;
            }

            // Push undo snapshot once per sequence, on first effective move
            if (!_ambientNudgeUndoPushed)
            {
                _history.PushUndoSnapshot(CaptureState());
                _ambientNudgeUndoPushed = true;
            }

            int step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;

            double dx = 0;
            double dy = 0;

            switch (e.Key)
            {
                case Key.Left: dx = -step; break;
                case Key.Right: dx = step; break;
                case Key.Up: dy = -step; break;
                case Key.Down: dy = step; break;
            }

            double newX = img.X + dx;
            double newY = img.Y + dy;

            var clamped = Viewer.ClampAmbientTopLeft(idx, newX, newY);

            img.X = (ushort)clamped.X;
            img.Y = (ushort)clamped.Y;

            Viewer.LoadDocument(_doc);
            ApplyAmbientSideToViewer();
            RefreshAmbientUi();

            e.Handled = true;
        }
    }

    private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Up && e.Key != Key.Down)
            return;

        // End nudge sequence
        _ambientNudgeActive = false;
        _ambientNudgeUndoPushed = false;
    }
}
