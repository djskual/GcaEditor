using System.Windows.Input;

namespace GcaEditor;

public partial class MainWindow
{
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && (_placingAmbientIndex != null || _movingAmbientIndex != null))
        {
            if (_placingAmbientIndex != null) ExitAmbientPlacementMode();
            if (_movingAmbientIndex != null) ExitAmbientMoveMode();
            e.Handled = true;
        }
    }
}
