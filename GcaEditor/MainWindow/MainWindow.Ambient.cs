using GcaEditor.Models;
using GcaEditor.Settings;
using GcaEditor.UI.Dialogs;
using GcaEditor.UI.Interop;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace GcaEditor;

public partial class MainWindow
{
    private enum DriveSide { LHD, RHD }
    private DriveSide _side = DriveSide.LHD;

    private readonly BitmapSource?[] _ambientLhd = new BitmapSource?[23];
    private readonly BitmapSource?[] _ambientRhd = new BitmapSource?[23];
    private readonly string?[] _ambientLhdName = new string?[23];
    private readonly string?[] _ambientRhdName = new string?[23];

    private readonly bool[] _ambientVisibleLhd = Enumerable.Repeat(true, 23).ToArray();
    private readonly bool[] _ambientVisibleRhd = Enumerable.Repeat(true, 23).ToArray();

    // Per-feature selection for RGB colorization.
    // When true, the feature is affected by color changes; otherwise it stays white.
    private readonly bool[] _ambientRgbEnabledLhd = new bool[23];
    private readonly bool[] _ambientRgbEnabledRhd = new bool[23];

    private int? _placingAmbientIndex = null;
    private int? _movingAmbientIndex = null;

    private readonly HashSet<int> _ambientIdsInitiallyInDoc = new();

    private static readonly Regex FeatureNameRx = new(
        @"^Feature_(LHD|RHD)_(\d{1,2})\.png$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private void InitAmbientUiOnLoaded()
    {
        ApplyAmbientSideToViewer();
        RefreshAmbientUi();
        UpdateCurrentSideLabel();
    }

    // Ambient images UI

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
            AppMessageBox.Show("Invalid Name. Expected: Feature_LHD_0.png .. Feature_LHD_22.png (or RHD).", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (side != _side)
        {
            AppMessageBox.Show(
                $"This session is locked to {_side}. Please import a Feature_{_side}_#.png file.",
                "Wrong drive side",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var bmp = Viewer.LoadAndConvertAmbientMask(ofd.FileName);
        StoreAmbientSlot(side, index, bmp, System.IO.Path.GetFileName(ofd.FileName));
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
            AppMessageBox.Show($"None image found for {_side}. Expected: Feature_{_side}_0.png .. Feature_{_side}_22.png");

        RefreshAmbientUi();
        AppMessageBox.Show($"Imported {imported} image(s) for {_side}.");
    }

    private void ClearAmbient_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || Viewer == null) return;

        if (AmbientList.SelectedItem is not AmbientSlotItem it)
        {
            AppMessageBox.Show("Select a slot (0..22) in the list");
            return;
        }

        int idx = it.Index;

        if (_placingAmbientIndex == idx)
            ExitAmbientPlacementMode();

        bool loaded = false;
        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        if (idx >= 0 && idx <= 22)
            loaded = slots[idx] != null;

        bool positioned = _doc != null && _doc.Images.Any(x => x.Id == (ushort)idx);

        // Nothing to delete
        if (!loaded && !positioned)
            return;

        if (AppSettingsStore.Current.ConfirmBeforeDeletingAmbientImage)
        {
            var result = AppMessageBox.Show(
                $"Delete ambient image in slot {idx:00}?",
                "Delete ambient image",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        // Push snapshot BEFORE any changes so delete is undoable for both pending and native images
        _history.PushUndoSnapshot(CaptureState());

        // Always clear runtime slot for current side
        ClearAmbientSlot(_side, idx);
        Viewer.ClearAmbientSlot(idx);

        // If there is a doc entry for this ID:
        // - keep it if it existed when the GCA was opened (Missing expected)
        // - remove it if it was created by user in this session (Empty expected)
        if (_doc != null && positioned)
        {
            bool wasInitial = _ambientIdsInitiallyInDoc.Contains(idx);
            if (!wasInitial)
            {
                var img = _doc.Images.FirstOrDefault(x => x.Id == (ushort)idx);
                if (img != null)
                    _doc.Images.Remove(img);
            }
        }

        RefreshAmbientUi();
        RefreshDirtyState();
    }

    private void ApplyAmbientSideToViewer()
    {
        if (!_uiReady || Viewer == null)
            return;

        Viewer.ClearAllAmbient();

        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        var rgb = (_side == DriveSide.LHD) ? _ambientRgbEnabledLhd : _ambientRgbEnabledRhd;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                Viewer.SetAmbientSlot(i, slots[i]!);

            // Apply per-feature RGB selection for tinting
            Viewer.SetAmbientTintEnabledForSlot(i, rgb[i]);
        }
    }

    private void RefreshAmbientUi()
    {
        int? selIndex = (AmbientList.SelectedItem as AmbientSlotItem)?.Index;

        AmbientList.Items.Clear();

        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        var names = (_side == DriveSide.LHD) ? _ambientLhdName : _ambientRhdName;
        var vis = (_side == DriveSide.LHD) ? _ambientVisibleLhd : _ambientVisibleRhd;

        var rgb = (_side == DriveSide.LHD) ? _ambientRgbEnabledLhd : _ambientRgbEnabledRhd;

        for (int i = 0; i <= 22; i++)
        {
            bool loaded = slots[i] != null;
            bool positioned = Viewer.IsAmbientIdPositionedInDoc(i);

            string status = loaded
                ? (positioned
                    ? (vis[i] ? "Loaded + positioned" : "Loaded + positioned (hidden)")
                    : "Loaded (pending)")
                : (positioned ? "[GCA] positioned (missing file)" : "Empty");

            AmbientList.Items.Add(new AmbientSlotItem
            {
                Index = i,
                Display = status,
                RgbEnabled = rgb[i]
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
        _history.PushUndoSnapshot(CaptureState());

        var clampedPt = Viewer.ClampAmbientTopLeft(idxSlot, imagePt.X, imagePt.Y);

        // Create or update image entry in doc
        var existing = _doc.Images.FirstOrDefault(i => i.Id == (ushort)idxSlot);
        if (existing == null)
        {
            _doc.Images.Add(new GcaImageRef
            {
                Id = (ushort)idxSlot,
                X = (ushort)Math.Round(clampedPt.X),
                Y = (ushort)Math.Round(clampedPt.Y),
            });
        }
        else
        {
            existing.X = (ushort)Math.Round(clampedPt.X);
            existing.Y = (ushort)Math.Round(clampedPt.Y);
        }

        // Ensure it's visible after placement
        var vis = (_side == DriveSide.LHD) ? _ambientVisibleLhd : _ambientVisibleRhd;
        vis[idxSlot] = true;

        Viewer.RefreshAmbientIdFromDoc(idxSlot);
        Viewer.SetAmbientSlotVisible(idxSlot, true);
        RefreshAmbientUi();
        RefreshDirtyState();

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


    private void EnterAmbientMoveMode(int index)
    {
        if (!_uiReady || Viewer == null) return;

        _movingAmbientIndex = index;
        Viewer.SetAmbientMoveMode(index);

        AmbientMoveHint.Visibility = Visibility.Visible;
        UpdateAmbientButtons();
    }

    private void ExitAmbientMoveMode()
    {
        if (!_uiReady || Viewer == null) return;

        _movingAmbientIndex = null;
        Viewer.ClearAmbientMoveMode();

        AmbientMoveHint.Visibility = Visibility.Collapsed;
        UpdateAmbientButtons();
    }

    private void UpdateAmbientAvailability()
    {
        bool hasBg = Viewer != null && Viewer.HasBackground;
        bool hasGca = _doc != null;

        AmbientGroup.IsEnabled = hasBg && hasGca;
    }

    private void UpdateAmbientButtons()
    {
        if (!_uiReady || Viewer == null) return;

        bool placing = _placingAmbientIndex != null;
        bool moving = _movingAmbientIndex != null;

        CancelPlaceAmbientButton.IsEnabled = placing;
        PlaceAmbientButton.Content = placing ? "Placing..." : "Place";

        CancelMoveAmbientButton.IsEnabled = moving;
        MoveAmbientButton.Content = moving ? "Moving..." : "Move";

        PlaceAmbientButton.IsEnabled = false;
        MoveAmbientButton.IsEnabled = false;

        ToggleAmbientButton.IsEnabled = false;
        ToggleAmbientButton.Content = "Hide";

        // Disable actions while moving
        if (AmbientList.SelectedItem is not AmbientSlotItem it) return;

        int idx = it.Index;

        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        var vis = (_side == DriveSide.LHD) ? _ambientVisibleLhd : _ambientVisibleRhd;

        bool loaded = (idx >= 0 && idx <= 22 && slots[idx] != null);
        bool positioned = Viewer.IsAmbientIdPositionedInDoc(idx);
        bool pending = loaded && !positioned;

        // Place only when pending and not already in a mode
        PlaceAmbientButton.IsEnabled = !placing && !moving && pending;

        // Move only when loaded + positioned and not already in a mode
        MoveAmbientButton.IsEnabled = !placing && !moving && loaded && positioned;

        // Hide/Show only when loaded + positioned and not in a mode
        bool canToggle = !placing && !moving && loaded && positioned;
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

    private void AmbientRgbCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        if (cb.DataContext is not AmbientSlotItem it) return;

        bool enabled = cb.IsChecked == true;

        var rgb = (_side == DriveSide.LHD) ? _ambientRgbEnabledLhd : _ambientRgbEnabledRhd;
        if (it.Index < 0 || it.Index > 22) return;

        rgb[it.Index] = enabled;
        it.RgbEnabled = enabled;

        // Apply immediately to viewer: checked slots are affected by the color bar tint.
        if (Viewer != null)
            Viewer.SetAmbientTintEnabledForSlot(it.Index, enabled);
    }

    private void ToggleAmbientVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || Viewer == null) return;

        if (AmbientList.SelectedItem is not AmbientSlotItem it)
        {
            AppMessageBox.Show("Select a slot (0..22) in the list.");
            return;
        }

        int idxSlot = it.Index;

        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        var vis = (_side == DriveSide.LHD) ? _ambientVisibleLhd : _ambientVisibleRhd;

        if (idxSlot < 0 || idxSlot > 22 || slots[idxSlot] == null)
        {
            AppMessageBox.Show("No image loaded into this slot.");
            return;
        }

        bool positioned = Viewer.IsAmbientIdPositionedInDoc(idxSlot);
        if (!positioned)
        {
            AppMessageBox.Show("Image pending: Place it first, before Hide/Show.");
            return;
        }

        vis[idxSlot] = !vis[idxSlot];

        Viewer.SetAmbientSlotVisible(idxSlot, vis[idxSlot]);

        RefreshAmbientUi();
        UpdateAmbientButtons();
    }

    private void PlaceAmbient_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || Viewer == null) return;

        if (AmbientList.SelectedItem is not AmbientSlotItem it)
        {
            AppMessageBox.Show("Select an image (slot 0..22) from the list.");
            return;
        }

        int idxSlot = it.Index;
        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;

        if (idxSlot < 0 || idxSlot > 22)
            return;

        if (slots[idxSlot] == null)
        {
            AppMessageBox.Show("This slot is empty. Import an image first.");
            return;
        }

        // Place only if pending
        if (Viewer.IsAmbientIdPositionedInDoc(idxSlot))
        {
            AppMessageBox.Show("This image is already positioned. Place is only available on a pending basis.");
            return;
        }

        EnterAmbientPlacementMode(idxSlot);
    }

    private void CancelPlaceAmbient_Click(object sender, RoutedEventArgs e)
    {
        ExitAmbientPlacementMode();
    }


    private void MoveAmbient_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || Viewer == null) return;

        if (AmbientList.SelectedItem is not AmbientSlotItem it)
        {
            AppMessageBox.Show("Select an image (slot 0..22) from the list.");
            return;
        }

        int idx = it.Index;

        var slots = (_side == DriveSide.LHD) ? _ambientLhd : _ambientRhd;
        bool loaded = (idx >= 0 && idx <= 22 && slots[idx] != null);
        bool positioned = Viewer.IsAmbientIdPositionedInDoc(idx);

        if (!loaded || !positioned)
        {
            AppMessageBox.Show("Move is only available if the image is loaded and positioned.");
            return;
        }

        // Exit placement if needed
        if (_placingAmbientIndex != null)
            ExitAmbientPlacementMode();

        EnterAmbientMoveMode(idx);
    }

    private void CancelMoveAmbient_Click(object sender, RoutedEventArgs e)
    {
        ExitAmbientMoveMode();
    }

}
