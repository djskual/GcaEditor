using System;
using System.Windows;
using System.Windows.Input;
using GcaEditor.Models;

namespace GcaEditor;

public partial class MainWindow
{
    private EditorState CaptureState(GcaDocument? docOverride = null)
    {
        var s = new EditorState();
        s.Doc = docOverride ?? _doc;

        s.AmbientLhd = (System.Windows.Media.Imaging.BitmapSource?[])_ambientLhd.Clone();
        s.AmbientRhd = (System.Windows.Media.Imaging.BitmapSource?[])_ambientRhd.Clone();

        s.AmbientLhdName = (string?[])_ambientLhdName.Clone();
        s.AmbientRhdName = (string?[])_ambientRhdName.Clone();

        s.AmbientVisibleLhd = (bool[])_ambientVisibleLhd.Clone();
        s.AmbientVisibleRhd = (bool[])_ambientVisibleRhd.Clone();

        return s;
    }

    private void ApplyState(EditorState s)
    {
        _doc = s.Doc;

        Array.Copy(s.AmbientLhd, _ambientLhd, 23);
        Array.Copy(s.AmbientRhd, _ambientRhd, 23);

        Array.Copy(s.AmbientLhdName, _ambientLhdName, 23);
        Array.Copy(s.AmbientRhdName, _ambientRhdName, 23);

        Array.Copy(s.AmbientVisibleLhd, _ambientVisibleLhd, 23);
        Array.Copy(s.AmbientVisibleRhd, _ambientVisibleRhd, 23);

        if (_doc != null)
            Viewer.LoadDocument(_doc);

        ApplyAmbientSideToViewer();
        RefreshZonesUi();
        RefreshAmbientUi();
    }

    private void Undo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _history.CanUndo;
        e.Handled = true;
    }

    private void Undo_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!_history.CanUndo) return;

        var current = CaptureState();
        var prev = _history.Undo(current);
        ApplyState(prev);
    }

    private void Redo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _history.CanRedo;
        e.Handled = true;
    }

    private void Redo_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!_history.CanRedo) return;

        var current = CaptureState();
        var next = _history.Redo(current);
        ApplyState(next);
    }
}
