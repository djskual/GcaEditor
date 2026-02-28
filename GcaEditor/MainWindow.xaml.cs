using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GcaEditor.UI.Interop;
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
    private bool _uiReady = false;

    // Ambient images
    private enum DriveSide { LHD, RHD }
    private DriveSide _side = DriveSide.LHD;

    // We keep separate slots for LHD/RHD so you can switch without losing imports.
    private readonly BitmapSource?[] _ambientLhd = new BitmapSource?[23];
    private readonly BitmapSource?[] _ambientRhd = new BitmapSource?[23];
    private readonly string?[] _ambientLhdName = new string?[23];
    private readonly string?[] _ambientRhdName = new string?[23];

    // Visibility per slot (true = rendered in viewer; false = kept but hidden)
    private readonly bool[] _ambientVisibleLhd = Enumerable.Repeat(true, 23).ToArray();
    private readonly bool[] _ambientVisibleRhd = Enumerable.Repeat(true, 23).ToArray();

    // Placement mode
    private int? _placingAmbientIndex = null;

    // IDs that were already present in _doc.Images when the GCA was opened
    private readonly HashSet<int> _ambientIdsInitiallyInDoc = new();

    private static readonly Regex FeatureNameRx = new(
        @"^Feature_(LHD|RHD)_(\d{1,2})\.png$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

        // Inject zone names to viewer
        Viewer.SetZoneNames(_zoneCatalog.Names);

        Viewer.AmbientPlaceRequested += Viewer_AmbientPlaceRequested;

        // Security: must load a background before opening / saving a GCA
        OpenGcaButton.IsEnabled = Viewer.HasBackground;
        SaveGcaButton.IsEnabled = false;

        RefreshZonesUi();

        Loaded += (_, __) =>
        {
            _uiReady = true;

            // Safe initial side state
            _side = (SideRhd?.IsChecked == true) ? DriveSide.RHD : DriveSide.LHD;

            ApplyAmbientSideToViewer();
            RefreshAmbientUi();
        };
    }

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

        CaptureInitialAmbientIds();

        SaveGcaButton.IsEnabled = true;

        RefreshZonesUi();

        // Refresh ambient status (some IDs may already be positioned in the GCA)
        RefreshAmbientUi();
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

    private void CaptureInitialAmbientIds()
    {
        _ambientIdsInitiallyInDoc.Clear();
        if (_doc == null) return;

        foreach (var img in _doc.Images)
            _ambientIdsInitiallyInDoc.Add(img.Id);
    }

    // Ambient images UI

    private void Side_Checked(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || Viewer == null)
            return;

        if (sender is RadioButton rb)
            _side = (rb.Name == "SideRhd") ? DriveSide.RHD : DriveSide.LHD;
        else
            _side = (SideRhd?.IsChecked == true) ? DriveSide.RHD : DriveSide.LHD;

        ApplyAmbientSideToViewer();
        RefreshAmbientUi();
    }

    private void ImportAmbientFile_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            Title = "Import ambient image (Feature_LHD_#.png / Feature_RHD_#.png)"
        };
        if (ofd.ShowDialog() != true) return;

        if (!TryParseFeatureName(System.IO.Path.GetFileName(ofd.FileName), out var side, out var index))
        {
            MessageBox.Show("Nom invalide. Attendu: Feature_LHD_0.png .. Feature_LHD_22.png (ou RHD).", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var bmp = Viewer.LoadAndConvertAmbientMask(ofd.FileName);
        StoreAmbientSlot(side, index, bmp, System.IO.Path.GetFileName(ofd.FileName));

        // If this matches current side, push to viewer immediately
        if (side == _side)
            Viewer.SetAmbientSlot(index, bmp);

        RefreshAmbientUi();
    }

    private void ImportAmbientFolder_Click(object sender, RoutedEventArgs e)
    {
        // WPF-friendly folder picker (no WinForms dependency)
        var folder = FolderPicker.PickFolder("Select folder containing Feature_LHD_#.png / Feature_RHD_#.png");
        if (string.IsNullOrWhiteSpace(folder))
            return;

        var files = System.IO.Directory.GetFiles(folder, "*.png", System.IO.SearchOption.TopDirectoryOnly);

        int imported = 0;
        foreach (var f in files)
        {
            var name = System.IO.Path.GetFileName(f);
            if (!TryParseFeatureName(name, out var side, out var index))
                continue;

            // Filter by selected side (user-friendly)
            if (side != _side)
                continue;

            try
            {
                var bmp = Viewer.LoadAndConvertAmbientMask(f);
                StoreAmbientSlot(side, index, bmp, name);
                Viewer.SetAmbientSlot(index, bmp);
                imported++;
            }
            catch
            {
                // Ignore a single broken file
            }
        }

        if (imported == 0)
            MessageBox.Show($"Aucune image trouvee pour {_side}. Attendu: Feature_{_side}_0.png .. Feature_{_side}_22.png");

        RefreshAmbientUi();
        MessageBox.Show($"Imported {imported} image(s) for {_side}.");
    }

    private void ClearAmbient_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || Viewer == null) return;

        if (AmbientList.SelectedItem is not AmbientSlotItem it)
        {
            MessageBox.Show("Selectionne un slot (0..22) dans la liste.");
            return;
        }

        int idx = it.Index;

        // If deleting the one currently in placement mode, exit first
        if (_placingAmbientIndex == idx)
            ExitAmbientPlacementMode();

        // 1) Always clear runtime slot (file)
        ClearAmbientSlot(_side, idx);
        Viewer.ClearAmbientSlot(idx);

        // 2) If doc has an entry for this ID:
        // - keep it if it existed when the GCA was opened (Missing expected)
        // - remove it if it was created by user in this session (Empty expected)
        if (_doc != null)
        {
            bool positioned = _doc.Images.Any(x => x.Id == idx);
            if (positioned)
            {
                bool wasInitial = _ambientIdsInitiallyInDoc.Contains(idx);
                if (!wasInitial)
                {
                    var img = _doc.Images.FirstOrDefault(x => x.Id == idx);
                    if (img != null)
                    {
                        _history.PushUndoSnapshot(_doc);
                        _doc.Images.Remove(img);

                        Viewer.LoadDocument(_doc);
                    }
                }
            }
        }

        ApplyAmbientSideToViewer();
        RefreshAmbientUi();
    }

    private void ApplyAmbientSideToViewer()
    {
        if (!_uiReady || Viewer == null)
            return;

        Viewer.ClearAllAmbient();

        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                Viewer.SetAmbientSlot(i, slots[i]!);
        }
    }

    private void RefreshAmbientUi()
    {
        int? selIndex = (AmbientList.SelectedItem as AmbientSlotItem)?.Index;

        AmbientList.Items.Clear();

        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        var names = (_side == DriveSide.LHD) ? _ambientLhdName : _ambientRhdName;
        var vis = (_side == DriveSide.LHD) ? _ambientVisibleLhd : _ambientVisibleRhd;

        for (int i = 0; i <= 22; i++)
        {
            bool loaded = slots[i] != null;
            bool positioned = Viewer.IsAmbientIdPositionedInDoc(i);

            string status = loaded
                ? (positioned
                    ? (vis[i] ? "Loaded + positioned" : "Loaded + positioned (hidden)")
                    : "Loaded (pending)")
                : (positioned ? "[GCA] positioned (missing file)" : "Empty");

            string file = loaded && !string.IsNullOrWhiteSpace(names[i]) ? names[i]! : "";

            AmbientList.Items.Add(new AmbientSlotItem
            {
                Index = i,
                Display = $"{i:00} - {status} {file}".TrimEnd()
            });
        }

        if (selIndex != null)
        {
            for (int i = 0; i < AmbientList.Items.Count; i++)
            {
                if (AmbientList.Items[i] is AmbientSlotItem it && it.Index == selIndex.Value)
                {
                    AmbientList.SelectedIndex = i;
                    break;
                }
            }
        }

        UpdateAmbientButtons();
    }

    private static bool TryParseFeatureName(string filename, out DriveSide side, out int index)
    {
        side = DriveSide.LHD;
        index = -1;

        var m = FeatureNameRx.Match(filename);
        if (!m.Success) return false;

        side = m.Groups[1].Value.Equals("RHD", StringComparison.OrdinalIgnoreCase)
            ? DriveSide.RHD
            : DriveSide.LHD;

        if (!int.TryParse(m.Groups[2].Value, out index))
            return false;

        return index >= 0 && index <= 22;
    }

    private void StoreAmbientSlot(DriveSide side, int index, BitmapSource bmp, string filename)
    {
        if (side == DriveSide.LHD)
        {
            _ambientLhd[index] = bmp;
            _ambientLhdName[index] = filename;
            _ambientVisibleLhd[index] = true;
        }
        else
        {
            _ambientRhd[index] = bmp;
            _ambientRhdName[index] = filename;
            _ambientVisibleRhd[index] = true;
        }
    }

    private void ClearAmbientSlot(DriveSide side, int index)
    {
        if (side == DriveSide.LHD)
        {
            _ambientLhd[index] = null;
            _ambientLhdName[index] = null;
            _ambientVisibleLhd[index] = true;
        }
        else
        {
            _ambientRhd[index] = null;
            _ambientRhdName[index] = null;
            _ambientVisibleRhd[index] = true;
        }
    }

    private void Viewer_AmbientPlaceRequested(object? sender, Point imagePt)
    {
        if (!_uiReady || Viewer == null) return;
        if (_doc == null) return;
        if (_placingAmbientIndex == null) return;

        int idxSlot = _placingAmbientIndex.Value;

        // Only place if we actually have a bitmap loaded for current side
        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        if (idxSlot < 0 || idxSlot > 22 || slots[idxSlot] == null)
            return;

        // Undo snapshot BEFORE mutation
        _history.PushUndoSnapshot(_doc);

        // Create or update image entry in doc
        var existing = _doc.Images.FirstOrDefault(i => i.Id == (ushort)idxSlot);
        if (existing == null)
        {
            _doc.Images.Add(new GcaImageRef
            {
                Id = (ushort)idxSlot,
                X = (ushort)Math.Round(imagePt.X),
                Y = (ushort)Math.Round(imagePt.Y),
            });
        }
        else
        {
            existing.X = (ushort)Math.Round(imagePt.X);
            existing.Y = (ushort)Math.Round(imagePt.Y);
        }

        // Ensure it's visible after placement
        var vis = (_side == DriveSide.LHD) ? _ambientVisibleLhd : _ambientVisibleRhd;
        vis[idxSlot] = true;

        Viewer.LoadDocument(_doc);
        ApplyAmbientSideToViewer();
        RefreshAmbientUi();

        ExitAmbientPlacementMode();
    }

    private void EnterAmbientPlacementMode(int index)
    {
        if (!_uiReady || Viewer == null) return;

        _placingAmbientIndex = index;
        Viewer.SetAmbientPlacementMode(index);
        AmbientPlacementHint.Visibility = Visibility.Visible;
        UpdateAmbientButtons();
    }

    private void ExitAmbientPlacementMode()
    {
        if (!_uiReady || Viewer == null) return;

        _placingAmbientIndex = null;
        Viewer.ClearAmbientPlacementMode();
        AmbientPlacementHint.Visibility = Visibility.Collapsed;
        UpdateAmbientButtons();
    }

    private void UpdateAmbientButtons()
    {
        if (!_uiReady || Viewer == null) return;

        bool placing = _placingAmbientIndex != null;

        CancelPlaceAmbientButton.IsEnabled = placing;
        PlaceAmbientButton.Content = placing ? "Placing..." : "Place";

        PlaceAmbientButton.IsEnabled = false;
        ToggleAmbientButton.IsEnabled = false;
        ToggleAmbientButton.Content = "Hide";

        if (AmbientList.SelectedItem is not AmbientSlotItem it) return;

        int idx = it.Index;

        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        var vis = (_side == DriveSide.LHD) ? _ambientVisibleLhd : _ambientVisibleRhd;

        bool loaded = (idx >= 0 && idx <= 22 && slots[idx] != null);
        bool positioned = Viewer.IsAmbientIdPositionedInDoc(idx);
        bool pending = loaded && !positioned;

        // Place only when pending and not already placing
        PlaceAmbientButton.IsEnabled = !placing && pending;

        // Hide/Show only when loaded + positioned and not placing
        bool canToggle = !placing && loaded && positioned;
        ToggleAmbientButton.IsEnabled = canToggle;

        if (loaded && positioned)
            ToggleAmbientButton.Content = vis[idx] ? "Hide" : "Show";
        else
            ToggleAmbientButton.Content = "Hide";
    }

    private void AmbientList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateAmbientButtons();
    }

    private void ToggleAmbientVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || Viewer == null) return;

        if (AmbientList.SelectedItem is not AmbientSlotItem it)
        {
            MessageBox.Show("Selectionne un slot (0..22) dans la liste.");
            return;
        }

        int idxSlot = it.Index;

        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        var vis = (_side == DriveSide.LHD) ? _ambientVisibleLhd : _ambientVisibleRhd;

        if (idxSlot < 0 || idxSlot > 22 || slots[idxSlot] == null)
        {
            MessageBox.Show("Aucune image chargee dans ce slot.");
            return;
        }

        bool positioned = Viewer.IsAmbientIdPositionedInDoc(idxSlot);
        if (!positioned)
        {
            MessageBox.Show("Image pending: place-la d'abord avant Hide/Show.");
            return;
        }

        vis[idxSlot] = !vis[idxSlot];

        if (vis[idxSlot])
            Viewer.SetAmbientSlot(idxSlot, slots[idxSlot]!);
        else
            Viewer.ClearAmbientSlot(idxSlot);

        RefreshAmbientUi();
        UpdateAmbientButtons();
    }

    private void PlaceAmbient_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || Viewer == null) return;

        if (AmbientList.SelectedItem is not AmbientSlotItem it)
        {
            MessageBox.Show("Selectionne une image (slot 0..22) dans la liste.");
            return;
        }

        int idxSlot = it.Index;
        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;

        if (idxSlot < 0 || idxSlot > 22)
            return;

        if (slots[idxSlot] == null)
        {
            MessageBox.Show("Ce slot est vide. Importe d'abord une image.");
            return;
        }

        // Place only if pending
        if (Viewer.IsAmbientIdPositionedInDoc(idxSlot))
        {
            MessageBox.Show("Cette image est deja positionnee. Place est dispo uniquement en pending.");
            return;
        }

        EnterAmbientPlacementMode(idxSlot);
    }

    private void CancelPlaceAmbient_Click(object sender, RoutedEventArgs e)
    {
        ExitAmbientPlacementMode();
    }

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

        // Security: forbid duplicate ID in the GCA
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
        _history.PushUndoSnapshot(_doc);

        _doc.Zones.RemoveAll(z => z.Id == sel.Value);

        Viewer.LoadDocument(_doc);
        RefreshZonesUi();
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

    private sealed class ZoneListItem
    {
        public ushort Id { get; init; }
        public string Display { get; init; } = "";

        public override string ToString() => Display;
    }

    private sealed class AmbientSlotItem
    {
        public int Index { get; init; }
        public string Display { get; init; } = "";

        public override string ToString() => Display;
    }
}
