using GcaEditor.Models;
using GcaEditor.UI.Dialogs;
using System.Windows;
using System.Windows.Controls;

namespace GcaEditor;

public partial class MainWindow
{
    // Zones UI

    private void RefreshZonesUi()
    {
        // Preserve current selection (important: drag triggers RefreshZonesUi)
        var selectedId = Viewer.SelectedZoneId;

        _suppressListSelection = true;
        try
        {
            ZonesList.Items.Clear();

            if (_doc == null)
            {
                AddZoneCombo.ItemsSource = null;
                ZonesList.SelectedIndex = -1;
                return;
            }

            foreach (var z in _doc.Zones.OrderBy(z => z.Id))
            {
                ZonesList.Items.Add(new ZoneListItem
                {
                    Id = z.Id,
                    Display = $"{z.Id} - {_zoneCatalog.GetName(z.Id)}"
                });
            }

            // Populate available IDs: Known IDs not already in doc
            var existing = _doc.Zones.Select(z => z.Id).ToHashSet();
            var available = Enumerable
                           .Range(0x00, 0x0C) // 0 ? 11
                           .Select(i => (ushort)i)
                           .Where(id => !existing.Contains(id))
                           .ToList();

            AddZoneCombo.ItemsSource = available;
            if (available.Count > 0)
                AddZoneCombo.SelectedIndex = 0;

            // Restore selection in list without triggering Viewer.ClearSelection()
            if (selectedId != null)
            {
                for (int i = 0; i < ZonesList.Items.Count; i++)
                {
                    if (ZonesList.Items[i] is ZoneListItem it && it.Id == selectedId.Value)
                    {
                        ZonesList.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                ZonesList.SelectedIndex = -1;
            }
        }
        finally
        {
            _suppressListSelection = false;
        }
    }

    private void ZonesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressListSelection) return;
        if (_doc == null) return;

        if (ZonesList.SelectedItem is not ZoneListItem it)
        {
            Viewer.ClearSelection();
            UpdateZoneOpacitySelection(null);
            return;
        }

        Viewer.SelectZoneById(it.Id);
        UpdateZoneOpacitySelection(it.Id);
    }

    private void AddZone_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
        {
            AppMessageBox.Show("Load first a GCA file.");
            return;
        }

        if (AddZoneCombo.SelectedItem is not ushort zoneId)
        {
            AppMessageBox.Show("Chose a zone in the available list.");
            return;
        }

        // Undo snapshot BEFORE mutation
        _history.PushUndoSnapshot(CaptureState());

        // Place at current viewport center in image coords
        var center = Viewer.GetViewportCenterInImageCoords();
        AddZoneInternal(zoneId, center.X, center.Y);

        Viewer.LoadDocument(_doc);
        RefreshZonesUi();

        Viewer.SelectZoneById(zoneId);

        RefreshDirtyState();
    }

    private void DeleteZone_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
        {
            AppMessageBox.Show("First load a GCA file.");
            return;
        }

        var sel = Viewer.SelectedZoneId;
        if (sel == null)
        {
            AppMessageBox.Show("Select a zone to delete.");
            return;
        }

        // Undo snapshot BEFORE mutation
        _history.PushUndoSnapshot(CaptureState());

        _doc.Zones.RemoveAll(z => z.Id == sel.Value);

        Viewer.LoadDocument(_doc);
        RefreshZonesUi();
        UpdateZoneOpacitySelection(null);
        RefreshDirtyState();
    }

    private void AddZoneInternal(ushort id, double centerX, double centerY)
    {
        // Zone = 104x104, center = sun center
        const double half = 52.0;

        var z = new GcaZone
        {
            Id = id,

            // Constants
            A = 0x0010,
            B = 0x0010,
            C = 0x0004,

            X1 = (ushort)Math.Round(centerX - half),
            Y1 = (ushort)Math.Round(centerY - half),

            X2 = (ushort)Math.Round(centerX + half),
            Y2 = (ushort)Math.Round(centerY - half),

            X3 = (ushort)Math.Round(centerX + half),
            Y3 = (ushort)Math.Round(centerY + half),

            X4 = (ushort)Math.Round(centerX - half),
            Y4 = (ushort)Math.Round(centerY + half),
        };

        _doc!.Zones.Add(z);
    }
}
