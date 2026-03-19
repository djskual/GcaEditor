using GcaEditor.Data;
using GcaEditor.IO;
using GcaEditor.Views;
using GcaEditor.UI.Dialogs;
using GcaEditor.Settings;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GcaEditor;

public partial class MainWindow
{
    private void ChooseCar_Click(object sender, RoutedEventArgs e)
    {
        CarCatalog cat;
        try
        {
            cat = CarCatalogLoader.LoadOrThrow();
        }
        catch (Exception ex)
        {
            AppMessageBox.Show("Failed to load car.json: " + ex.Message);
            return;
        }

        var dlg = new ChooseCarWindow(cat) { Owner = this };
        bool? ok = dlg.ShowDialog();
        if (ok != true)
            return;

        if (!TryConfirmDiscardChanges())
            return;

        var selectedSide = dlg.SelectedSide == "RHD" ? DriveSide.RHD : DriveSide.LHD;

        ResetWorkspaceForCarChange();
        SetCurrentSide(selectedSide);

        _currentSessionIsCustom = dlg.IsCustom;
        _currentCarId = dlg.IsCustom ? null : dlg.SelectedCar?.id;
        _currentCarName = dlg.IsCustom ? null : dlg.SelectedCar?.name;
        _currentMib = dlg.IsCustom ? null : dlg.SelectedMib;

        if (dlg.IsCustom)
        {
            CurrentCarLabel.Text = $"Car: Custom - {dlg.SelectedSide}";
            MibLabel.Text = "MIB: -";

            SetStartupLocked(false);
            RefreshCommandStates();
            UpdateWindowTitle();
            SaveLastProjectSnapshot();
            return;
        }

        if (dlg.SelectedCar == null)
            return;

        string carsRoot = CarCatalogLoader.GetCarsRoot();
        string carFolder = Path.Combine(carsRoot, dlg.SelectedMib, dlg.SelectedCar.id);

        bool isRhd = selectedSide == DriveSide.RHD;
        int gcaIndex = isRhd ? 1 : 0;

        string bgFile = isRhd ? cat.background.rhd : cat.background.lhd;
        string bgPath = Path.Combine(carFolder, bgFile);
        string gcaPath = Path.Combine(carFolder, gcaIndex.ToString() + ".gca");

        if (!File.Exists(bgPath))
        {
            AppMessageBox.Show("Background not found: " + bgPath);
            return;
        }

        if (!File.Exists(gcaPath))
        {
            AppMessageBox.Show("GCA not found: " + gcaPath);
            return;
        }

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new Uri(bgPath);
        bi.EndInit();
        bi.Freeze();

        _backgroundPath = bgPath;
        Viewer.SetBackground(bi);
        UpdateMibLabelFromBackground(bi);

        if (AppSettingsStore.Current.AutoFitViewerAfterBackgroundLoad)
        {
            var fitH = ViewerHost.ActualHeight;
            Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, fitH);
        }

        LoadGcaFromPath(gcaPath);
        LoadAmbientFeaturesFromCarFolder(carFolder, selectedSide);

        ApplyAmbientSideToViewer();
        RefreshAmbientUi();

        CurrentCarLabel.Text = "Car: " + dlg.SelectedCar.id + " - " + dlg.SelectedCar.name + " - " + dlg.SelectedMib + " - " + dlg.SelectedSide;

        SetStartupLocked(false);
        RefreshCommandStates();
        UpdateWindowTitle();
        SaveLastProjectSnapshot();
    }

    private void LoadGcaFromPath(string path)
    {
        if (!Viewer.HasBackground)
        {
            AppMessageBox.Show("Import a background first.");
            return;
        }

        try
        {
            var doc = GcaCodec.Load(path);

            _gcaPath = path;
            _doc = doc;

            _history.Clear();
            Viewer.LoadDocument(_doc);

            CaptureInitialAmbientIds();

            RefreshZonesUi();
            RefreshAmbientUi();
            UpdateAmbientAvailability();
            MarkDocumentClean();
            SaveLastProjectSnapshot();
        }
        catch (InvalidDataException ex)
        {
            AppMessageBox.Show(
                $"Invalid or unsupported GCA file.\n\n{ex.Message}",
                "GCA load error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (EndOfStreamException ex)
        {
            AppMessageBox.Show(
                $"Incomplete or truncated GCA file.\n\n{ex.Message}",
                "GCA load error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            AppMessageBox.Show(
                $"Unable to load GCA file.\n\n{ex.Message}",
                "GCA load error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LoadAmbientFeaturesFromCarFolder(string carFolder, DriveSide targetSide)
    {
        for (int i = 0; i <= 22; i++)
        {
            ClearAmbientSlot(DriveSide.LHD, i);
            ClearAmbientSlot(DriveSide.RHD, i);

            _ambientRgbEnabledLhd[i] = false;
            _ambientRgbEnabledRhd[i] = false;
        }

        var files = Directory.GetFiles(carFolder, "Feature_*.png", SearchOption.TopDirectoryOnly);
        foreach (var f in files)
        {
            var name = Path.GetFileName(f);
            if (!TryParseFeatureName(name, out var side, out var index))
                continue;

            if (side != targetSide)
                continue;

            try
            {
                var bmp = Viewer.LoadAndConvertAmbientMask(f);
                StoreAmbientSlot(side, index, bmp, name);
            }
            catch
            {
                // ignore one broken file
            }
        }
    }

    private void ImportBackground_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            Title = "Import Background (MIB2.5 1280x556 or MIB2 800x417)"
        };
        if (ofd.ShowDialog() != true) return;

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new Uri(ofd.FileName);
        bi.EndInit();
        bi.Freeze();

        _backgroundPath = ofd.FileName;
        Viewer.SetBackground(bi);
        UpdateMibLabelFromBackground(bi);

        if (AppSettingsStore.Current.AutoFitViewerAfterBackgroundLoad)
            Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, ViewerHost.ActualHeight);

        UpdateAmbientAvailability();
        RefreshCommandStates();
        UpdateWindowTitle();
        SaveLastProjectSnapshot();
    }

    private void ViewerHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Viewer.HasBackground)
        {
            var fitH = ViewerHost.ActualHeight;

            Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, fitH);
        }
    }

    private void OpenGca_Click(object sender, RoutedEventArgs e)
    {
        if (!Viewer.HasBackground)
        {
            AppMessageBox.Show("You must import a background first (PNG 1280x556 or 800x417) before opening a .gca file."); 
            return;
        }

        var ofd = new OpenFileDialog
        {
            Filter = "GCA (*.gca)|*.gca|All files|*.*",
            Title = "Open GCA file"
        };
        if (ofd.ShowDialog() != true) return;

        LoadGcaFromPath(ofd.FileName);
    }

    private void SaveGca_Click(object sender, RoutedEventArgs e)
    {
        TrySaveCurrentGca();
    }

    private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SaveGca_Click(sender, new RoutedEventArgs());
    }

    private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _doc != null;
        e.Handled = true;
    }

    private void UpdateMibLabelFromBackground(BitmapSource bg)
    {
        string mib;
        if (bg.PixelWidth == 1280 && bg.PixelHeight == 556)
            mib = "MIB2.5 (1280x556)";
        else if (bg.PixelWidth == 800 && bg.PixelHeight == 417)
            mib = "MIB2 (800x417)";
        else
            mib = "Unknown (" + bg.PixelWidth + "x" + bg.PixelHeight + ")";

        MibLabel.Text = "MIB: " + mib;
    }

    private void CaptureInitialAmbientIds()
    {
        _ambientIdsInitiallyInDoc.Clear();
        if (_doc == null) return;

        foreach (var img in _doc.Images)
            _ambientIdsInitiallyInDoc.Add(img.Id);
    }

}
