using System.Windows.Input;

namespace GcaEditor;

public partial class MainWindow
{
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _placingAmbientIndex != null)
        {
            ExitAmbientPlacementMode();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        if (e.Key == Key.Z)
        {
            if (_doc != null && _history.CanUndo)
            {
                _doc = _history.Undo(_doc);
                Viewer.LoadDocument(_doc);
                RefreshZonesUi();
                RefreshAmbientUi();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Y)
        {
            if (_doc != null && _history.CanRedo)
            {
                _doc = _history.Redo(_doc);
                Viewer.LoadDocument(_doc);
                RefreshZonesUi();
                RefreshAmbientUi();
            }
            e.Handled = true;
        }
    }
}
