using GcaEditor.Models;
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
            var available = _zoneCatalog.KnownZoneIds.Where(id => !existing.Contains(id)).ToList();

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
            MessageBox.Show("Charge un GCA d'abord.");
            return;
        }

        // 1) Priorite au custom si rempli
        ushort? zoneId = null;

        var customText = CustomZoneIdBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(customText))
        {
            if (!ushort.TryParse(customText, out var customId))
            {
                MessageBox.Show("ID custom invalide. Entrez un nombre entre 0 et 65535.");
                return;
            }
            zoneId = customId;
        }
        else
        {
            // 2) Sinon, prendre depuis la liste connue
            if (AddZoneCombo.SelectedItem is ushort knownId)
                zoneId = knownId;
        }

        if (zoneId == null)
        {
            MessageBox.Show("Choisis une zone dans la liste ou saisis un ID custom.");
            return;
        }

        // Security: forbid duplicate ID in the GCA
        if (_doc.Zones.Any(z => z.Id == zoneId.Value))
        {
            MessageBox.Show($"La zone {zoneId.Value} existe deja dans ce GCA. Supprime-la avant, ou choisis un autre ID.");
            return;
        }

        // Undo snapshot BEFORE mutation
        _history.PushUndoSnapshot(CaptureState());

        // Place at current viewport center in image coords
        var center = Viewer.GetViewportCenterInImageCoords();
        AddZoneInternal(zoneId.Value, center.X, center.Y);

        Viewer.LoadDocument(_doc);
        RefreshZonesUi();

        Viewer.SelectZoneById(zoneId.Value);

        // Reset custom box
        CustomZoneIdBox.Text = "";
    }

    private void DeleteZone_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
        {
            MessageBox.Show("Charge un GCA d'abord.");
            return;
        }

        var sel = Viewer.SelectedZoneId;
        if (sel == null)
        {
            MessageBox.Show("Selectionne une zone a supprimer.");
            return;
        }

        // Undo snapshot BEFORE mutation
        _history.PushUndoSnapshot(CaptureState());

        _doc.Zones.RemoveAll(z => z.Id == sel.Value);

        Viewer.LoadDocument(_doc);
        RefreshZonesUi();
        UpdateZoneOpacitySelection(null);
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
