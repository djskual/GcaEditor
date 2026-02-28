using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using GcaEditor.UI.Interop;
using GcaEditor.Models;

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

    private int? _placingAmbientIndex = null;

    private readonly HashSet<int> _ambientIdsInitiallyInDoc = new();

    private static readonly Regex FeatureNameRx = new(
        @"^Feature_(LHD|RHD)_(\d{1,2})\.png$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private void InitAmbientUiOnLoaded()
    {
        _side = (SideRhd?.IsChecked == true) ? DriveSide.RHD : DriveSide.LHD;
        ApplyAmbientSideToViewer();
        RefreshAmbientUi();
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

}
