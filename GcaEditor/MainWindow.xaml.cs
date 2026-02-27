using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GcaEditor.Data;
using GcaEditor.IO;
using GcaEditor.Models;
using GcaEditor.UndoRedo;

namespace GcaEditor;

public partial class MainWindow : Window
{
    private GcaDocument? _doc;
    private string? _gcaPath;

    private readonly UndoRedoStack<GcaDocument> _history;

    private readonly ZoneCatalog _zoneCatalog;

    private bool _suppressListSelection;

    public MainWindow()
    {
        InitializeComponent();

        _zoneCatalog = ZoneCatalog.LoadOrDefault();

        _history = new UndoRedoStack<GcaDocument>(d => d.DeepClone());

        Viewer.ZoneDragCommitted += (_, beforeSnapshot) =>
        {
            if (_doc == null) return;
            _history.PushUndoSnapshot(beforeSnapshot);
            RefreshZonesUi();
        };

        Viewer.SelectedZoneChanged += (_, zoneId) =>
        {
            if (_suppressListSelection) return;

            _suppressListSelection = true;
            try
            {
                if (zoneId == null)
                {
                    ZonesList.SelectedIndex = -1;
                }
                else
                {
                    // Select matching list item
                    for (int i = 0; i < ZonesList.Items.Count; i++)
                    {
                        if (ZonesList.Items[i] is ZoneListItem it && it.Id == zoneId.Value)
                        {
                            ZonesList.SelectedIndex = i;
                            ZonesList.ScrollIntoView(ZonesList.Items[i]);
                            break;
                        }
                    }
                }
            }
            finally
            {
                _suppressListSelection = false;
            }
        };

        PreviewKeyDown += MainWindow_PreviewKeyDown;

        // inject zone names to viewer
        Viewer.SetZoneNames(_zoneCatalog.Names);

        // Security: must load a background before opening / saving a GCA
        OpenGcaButton.IsEnabled = Viewer.HasBackground;
        SaveGcaButton.IsEnabled = false;

        RefreshZonesUi();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        if (e.Key == Key.Z)
        {
            if (_doc != null && _history.CanUndo)
            {
                _doc = _history.Undo(_doc);
                Viewer.LoadDocument(_doc);
                RefreshZonesUi();
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
            }
            e.Handled = true;
        }
    }

    private void ImportBackground_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            Title = "Fond menu lumieres (1280x556)"
        };
        if (ofd.ShowDialog() != true) return;

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new Uri(ofd.FileName);
        bi.EndInit();
        bi.Freeze();

        Viewer.SetBackground(bi);
        Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, ViewerHost.ActualHeight);

        // Now we can open a GCA
        OpenGcaButton.IsEnabled = true;
    }

    private void ViewerHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Viewer.HasBackground)
            Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, ViewerHost.ActualHeight);
    }

    private void OpenGca_Click(object sender, RoutedEventArgs e)
    {
        if (!Viewer.HasBackground)
        {
            MessageBox.Show("Tu dois d'abord importer un background (PNG 1280x556) avant de charger un .gca.");
            return;
        }

        var ofd = new OpenFileDialog
        {
            Filter = "GCA (*.gca)|*.gca|All files|*.*",
            Title = "Ouvrir un fichier GCA"
        };
        if (ofd.ShowDialog() != true) return;

        _gcaPath = ofd.FileName;
        _doc = GcaCodec.Load(_gcaPath);

        _history.Clear();
        Viewer.LoadDocument(_doc);

        SaveGcaButton.IsEnabled = true;

        RefreshZonesUi();
    }

    private void SaveGca_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
        {
            MessageBox.Show("Aucun GCA charge.");
            return;
        }

        var sfd = new SaveFileDialog
        {
            Filter = "GCA (*.gca)|*.gca",
            Title = "Enregistrer le GCA",
            FileName = _gcaPath != null ? System.IO.Path.GetFileName(_gcaPath) : "menu.gca"
        };

        if (sfd.ShowDialog() != true) return;

        GcaCodec.Save(sfd.FileName, _doc);
        MessageBox.Show("GCA sauvegarde.");
    }

    // ===== Zones UI =====

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
            return;
        }

        Viewer.SelectZoneById(it.Id);
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

        // Securite: interdire doublon d'ID dans le GCA (recommandé)
        if (_doc.Zones.Any(z => z.Id == zoneId.Value))
        {
            MessageBox.Show($"La zone {zoneId.Value} existe deja dans ce GCA. Supprime-la avant, ou choisis un autre ID.");
            return;
        }

        // Undo snapshot BEFORE mutation
        _history.PushUndoSnapshot(_doc);

        // Place at current viewport center in image coords
        var center = Viewer.GetViewportCenterInImageCoords();
        AddZoneInternal(zoneId.Value, center.X, center.Y);

        Viewer.LoadDocument(_doc);
        RefreshZonesUi();

        Viewer.SelectZoneById(zoneId.Value);

        // Reset custom box (optionnel)
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
        _history.PushUndoSnapshot(_doc);

        _doc.Zones.RemoveAll(z => z.Id == sel.Value);

        Viewer.LoadDocument(_doc);
        RefreshZonesUi();
    }

    private void AddZoneInternal(ushort id, double centerX, double centerY)
    {
        // zone = 104x104, center = sun center
        const double half = 52.0;

        var z = new GcaZone
        {
            Id = id,

            // constants
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

    private sealed class ZoneListItem
    {
        public ushort Id { get; init; }
        public string Display { get; init; } = "";

        public override string ToString() => Display;
    }
}
