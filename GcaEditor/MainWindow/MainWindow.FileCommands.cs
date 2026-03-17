using GcaEditor.Data;
using GcaEditor.IO;
using GcaEditor.Views;
using GcaEditor.UI.Dialogs;
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
        catch (System.Exception ex)
        {
            AppMessageBox.Show("Failed to load car.json: " + ex.Message);
            return;
        }

        var dlg = new ChooseCarWindow(cat) { Owner = this };
        bool? ok = dlg.ShowDialog();
        if (ok != true) return;

        if (dlg.IsCustom)
        {
            CurrentCarLabel.Text = "Car: Custom";
            MibLabel.Text = "MIB: -";
            SetStartupLocked(false);
            CommandManager.InvalidateRequerySuggested();
            return;
        }

        if (dlg.SelectedCar == null)
            return;

        string carsRoot = CarCatalogLoader.GetCarsRoot();
        string carFolder = Path.Combine(carsRoot, dlg.SelectedMib, dlg.SelectedCar.id);

        // Side determines which background and which GCA to use:
        // LHD -> 0.gca
        // RHD -> 1.gca
        bool isRhd = dlg.SelectedSide == "RHD";
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

        // Load background
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new System.Uri(bgPath);
        bi.EndInit();
        bi.Freeze();

        Viewer.SetBackground(bi);
        UpdateMibLabelFromBackground(bi);
        var fitH = ViewerHost.ActualHeight;

        Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, fitH);

        // Load GCA
        LoadGcaFromPath(gcaPath);

        // Load features for both sides
        LoadAmbientFeaturesFromCarFolder(carFolder);

        // Apply side selection
        if (isRhd)
        {
            SideRhd.IsChecked = true;
            _side = DriveSide.RHD;
        }
        else
        {
            SideLhd.IsChecked = true;
            _side = DriveSide.LHD;
        }

        ApplyAmbientSideToViewer();
        RefreshAmbientUi();

        CurrentCarLabel.Text = "Car: " + dlg.SelectedCar.id + " - " + dlg.SelectedCar.name + " - " + dlg.SelectedMib + " - " + dlg.SelectedSide;

        SetStartupLocked(false);
        CommandManager.InvalidateRequerySuggested();
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
            _gcaPath = path;
            _doc = GcaCodec.Load(_gcaPath);

            _history.Clear();
            Viewer.LoadDocument(_doc);

            CaptureInitialAmbientIds();

            RefreshZonesUi();
            RefreshAmbientUi();
            UpdateAmbientAvailability();
            CommandManager.InvalidateRequerySuggested();
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

    private void LoadAmbientFeaturesFromCarFolder(string carFolder)
    {
        // Reset runtime slots
        for (int i = 0; i <= 22; i++)
        {
            ClearAmbientSlot(DriveSide.LHD, i);
            ClearAmbientSlot(DriveSide.RHD, i);
        }

        var files = Directory.GetFiles(carFolder, "Feature_*.png", SearchOption.TopDirectoryOnly);
        foreach (var f in files)
        {
            var name = Path.GetFileName(f);
            if (!TryParseFeatureName(name, out var side, out var index))
                continue;

            try
            {
                var bmp = Viewer.LoadAndConvertAmbientMask(f);
                StoreAmbientSlot(side, index, bmp, name);
            }
            catch
            {
                // ignore
            }
        }
    }

    private void ImportBackground_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            Title = "Fond menu lumieres (MIB2.5 1280x556 or MIB2 800x417)"
        };
        if (ofd.ShowDialog() != true) return;

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new Uri(ofd.FileName);
        bi.EndInit();
        bi.Freeze();

        Viewer.SetBackground(bi);
        UpdateMibLabelFromBackground(bi);
        Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, ViewerHost.ActualHeight);

        UpdateAmbientAvailability();
        CommandManager.InvalidateRequerySuggested();
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
            AppMessageBox.Show("Tu dois d'abord importer un background (PNG 1280x556 or 800x417) avant de charger un .gca.");
            return;
        }

        var ofd = new OpenFileDialog
        {
            Filter = "GCA (*.gca)|*.gca|All files|*.*",
            Title = "Ouvrir un fichier GCA"
        };
        if (ofd.ShowDialog() != true) return;

        LoadGcaFromPath(ofd.FileName);
    }

    private void SaveGca_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
            return;

        var sfd = new SaveFileDialog
        {
            Filter = "GCA (*.gca)|*.gca",
            Title = "Enregistrer le GCA",
            FileName = _gcaPath != null ? System.IO.Path.GetFileName(_gcaPath) : "menu.gca"
        };

        if (sfd.ShowDialog() != true) return;

        GcaCodec.Save(sfd.FileName, _doc);
        AppMessageBox.Show("GCA sauvegarde.");
        CommandManager.InvalidateRequerySuggested();
    }

    private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SaveGca_Click(sender, new RoutedEventArgs());
    }

    private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _doc != null;
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
