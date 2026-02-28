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
        }
    }
}
