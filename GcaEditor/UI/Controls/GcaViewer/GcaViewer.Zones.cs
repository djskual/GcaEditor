using System.Windows;

namespace GcaEditor.UI.Controls;

public partial class GcaViewer
{
    public bool SelectZoneById(ushort id) => _suns.SelectZoneById(id);
    public void ClearSelection() => _suns.ClearSelectionPublic();
}
