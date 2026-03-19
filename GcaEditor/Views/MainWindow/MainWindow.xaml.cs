using GcaEditor.Data;
using GcaEditor.IO;
using GcaEditor.Models;
using GcaEditor.Settings;
using GcaEditor.UI.Dialogs;
using GcaEditor.UndoRedo;
using GcaEditor.Views;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GcaEditor;

public partial class MainWindow : Window
{
    public static readonly RoutedUICommand OpenGcaCommand =
        new("Open GCA", nameof(OpenGcaCommand), typeof(MainWindow));

    public static readonly RoutedUICommand ImportBackgroundCommand =
        new("Import Background", nameof(ImportBackgroundCommand), typeof(MainWindow));

    public static readonly RoutedUICommand ChooseCarCommand =
        new("Choose Car", nameof(ChooseCarCommand), typeof(MainWindow)); 

    private GcaDocument? _doc;
    private string? _gcaPath;
    private string? _lastSavedDocSignature;

    private readonly UndoRedoStack<EditorState> _history;
    private readonly ZoneCatalog _zoneCatalog;

    private bool _suppressListSelection;
    private bool _uiReady = false;
    private bool _startupLocked = true;

    private string? _backgroundPath;
    private bool _currentSessionIsCustom;
    private string? _currentCarId;
    private string? _currentCarName;
    private string? _currentMib;

    public MainWindow()
    {
        InitializeComponent();

        ApplyWindowPlacementFromSettings();
        UpdateWindowTitle();

        _zoneCatalog = ZoneCatalog.LoadOrDefault();
        _history = new UndoRedoStack<EditorState>(
            s => s.DeepClone(),
            maxUndo: AppSettingsStore.Current.MaxUndoHistory);

        WireViewerEvents();
        WireWindowEvents();

        // Startup: lock the UI until Choose car (or Custom) is selected
        SetStartupLocked(true);
        RefreshCommandStates();

        RefreshZonesUi();

        Loaded += (_, __) =>
        {
            _uiReady = true;

            InitAmbientUiOnLoaded();
            UpdateCurrentSideLabel();
            UpdateAmbientAvailability();
            RefreshCommandStates();
            UpdateWindowTitle();

            InitZoneOpacityUi();

            TryAutoLoadLastProject();

            if (AppSettingsStore.Current.AutoCheckUpdatesOnStartup)
                _ = CheckForUpdatesAsync(silentIfUpToDate: true, silentOnError: true);
        };
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(silentIfUpToDate: false, silentOnError: false);
    }

    private async Task CheckForUpdatesAsync(bool silentIfUpToDate, bool silentOnError)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "GcaEditor");
            client.Timeout = TimeSpan.FromSeconds(5);

            var json = await client.GetStringAsync(
                "https://api.github.com/repos/djskual/GcaEditor/tags");

            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                if (!silentIfUpToDate)
                {
                    AppMessageBox.Show(
                        "No tag found on GitHub.",
                        "Update check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            bool includePrerelease = AppSettingsStore.Current.IncludePrereleaseVersionsInUpdateCheck;

            string? latestTag = null;
            TagVersion? latestVersion = null;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("name", out var nameProp))
                    continue;

                var tagName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                if (!TryParseTagVersion(tagName, out var parsed))
                    continue;

                if (!includePrerelease && parsed.IsPrerelease)
                    continue;

                if (latestVersion == null || parsed.CompareTo(latestVersion.Value) > 0)
                {
                    latestVersion = parsed;
                    latestTag = tagName.Trim();
                }
            }

            if (latestVersion == null || string.IsNullOrWhiteSpace(latestTag))
            {
                if (!silentIfUpToDate)
                {
                    AppMessageBox.Show(
                        "No matching version tag found on GitHub.",
                        "Update check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            var currentTag = GetBuildTag();

            if (!TryParseTagVersion(currentTag, out var currentVersion))
            {
                if (!silentOnError)
                {
                    AppMessageBox.Show(
                        $"Current version tag is invalid: {currentTag}",
                        "Update check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return;
            }

            if (latestVersion.Value.CompareTo(currentVersion) > 0)
            {
                var result = AppMessageBox.Show(
                    $"New version available: {latestTag}\n\nOpen download page?",
                    "Update available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/djskual/GcaEditor/releases",
                        UseShellExecute = true
                    });
                }
            }
            else if (!silentIfUpToDate)
            {
                AppMessageBox.Show(
                    "You already have the latest version.",
                    "No update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            if (!silentOnError)
            {
                AppMessageBox.Show(
                    $"Unable to check updates.\n\n{ex.Message}",
                    "Update error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private readonly struct TagVersion : IComparable<TagVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string? PreLabel { get; }
        public int PreNumber { get; }
        public bool IsPrerelease => !string.IsNullOrWhiteSpace(PreLabel);

        public TagVersion(int major, int minor, int patch, string? preLabel, int preNumber)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreLabel = preLabel;
            PreNumber = preNumber;
        }

        public int CompareTo(TagVersion other)
        {
            var c = Major.CompareTo(other.Major);
            if (c != 0) return c;

            c = Minor.CompareTo(other.Minor);
            if (c != 0) return c;

            c = Patch.CompareTo(other.Patch);
            if (c != 0) return c;

            var thisIsStable = string.IsNullOrWhiteSpace(PreLabel);
            var otherIsStable = string.IsNullOrWhiteSpace(other.PreLabel);

            if (thisIsStable && otherIsStable) return 0;
            if (thisIsStable) return 1;
            if (otherIsStable) return -1;

            c = string.Compare(PreLabel, other.PreLabel, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;

            return PreNumber.CompareTo(other.PreNumber);
        }
    }

    private static bool TryParseTagVersion(string? tag, out TagVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var s = tag.Trim();

        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            s = s[1..];

        string corePart = s;
        string? prePart = null;

        var dashIndex = s.IndexOf('-');
        if (dashIndex >= 0)
        {
            corePart = s[..dashIndex];
            prePart = s[(dashIndex + 1)..];
        }

        var core = corePart.Split('.');
        if (core.Length != 3)
            return false;

        if (!int.TryParse(core[0], out var major)) return false;
        if (!int.TryParse(core[1], out var minor)) return false;
        if (!int.TryParse(core[2], out var patch)) return false;

        string? preLabel = null;
        int preNumber = 0;

        if (!string.IsNullOrWhiteSpace(prePart))
        {
            var pre = prePart.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);

            preLabel = pre[0].Trim();

            if (pre.Length > 1 && !int.TryParse(pre[1], out preNumber))
                preNumber = 0;
        }

        version = new TagVersion(major, minor, patch, preLabel, preNumber);
        return true;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AboutWindow
        {
            Owner = this
        };

        dlg.ShowDialog();
    }

    private static string GetBuildTag()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "git-tag.txt");
            if (!File.Exists(path))
                return "unknown";

            var tag = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(tag))
                return "unknown";

            return tag;
        }
        catch
        {
            return "unknown";
        }
    }

    private void RefreshCommandStates()
    {
        CommandManager.InvalidateRequerySuggested();
    }

    private void ApplyWindowPlacementFromSettings()
    {
        var settings = AppSettingsStore.Current;
        if (!settings.RememberWindowSizeAndPosition)
            return;

        if (settings.WindowWidth is double width && width >= MinWidth)
            Width = width;

        if (settings.WindowHeight is double height && height >= MinHeight)
            Height = height;

        if (settings.WindowLeft is double left && settings.WindowTop is double top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
    }

    private void SaveWindowPlacementToSettings()
    {
        var current = AppSettingsStore.Current;
        if (!current.RememberWindowSizeAndPosition)
            return;

        var updated = current.Clone();

        Rect bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        updated.WindowWidth = bounds.Width;
        updated.WindowHeight = bounds.Height;
        updated.WindowLeft = bounds.Left;
        updated.WindowTop = bounds.Top;

        AppSettingsStore.Save(updated);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(AppSettingsStore.Current)
        {
            Owner = this
        };

        if (dlg.ShowDialog() != true)
            return;

        bool rememberPlacement = dlg.ResultSettings.RememberWindowSizeAndPosition;

        AppSettingsStore.Save(dlg.ResultSettings);

        if (rememberPlacement)
            SaveWindowPlacementToSettings();
    }

    private void UpdateWindowTitle()
    {
        var buildTag = GetBuildTag();
        var title = string.IsNullOrWhiteSpace(buildTag) || buildTag == "unknown"
            ? "GcaEditor"
            : $"GcaEditor {buildTag}";

        if (IsDocumentDirty())
            title += " *";

        Title = title;
    }

    private bool IsDocumentDirty()
    {
        if (_doc == null)
            return false;

        return !string.Equals(
            ComputeDocumentSignature(_doc),
            _lastSavedDocSignature,
            StringComparison.Ordinal);
    }

    private void MarkDocumentClean()
    {
        _lastSavedDocSignature = _doc != null
            ? ComputeDocumentSignature(_doc)
            : null;

        UpdateWindowTitle();
        RefreshCommandStates();
    }

    private void RefreshDirtyState()
    {
        UpdateWindowTitle();
        RefreshCommandStates();
    }

    private static string ComputeDocumentSignature(GcaDocument doc)
    {
        var sb = new StringBuilder();

        sb.Append("V:").Append(doc.Version).Append('|');
        sb.Append("H:").Append(doc.HeaderUnk0).Append('|');

        foreach (var z in doc.Zones.OrderBy(z => z.Id))
        {
            sb.Append("Z:")
              .Append(z.Id).Append(',')
              .Append(z.A).Append(',')
              .Append(z.B).Append(',')
              .Append(z.C).Append(',')
              .Append(z.X1).Append(',')
              .Append(z.Y1).Append(',')
              .Append(z.X2).Append(',')
              .Append(z.Y2).Append(',')
              .Append(z.X3).Append(',')
              .Append(z.Y3).Append(',')
              .Append(z.X4).Append(',')
              .Append(z.Y4)
              .Append('|');
        }

        foreach (var img in doc.Images.OrderBy(i => i.Id))
        {
            sb.Append("I:")
              .Append(img.Id).Append(',')
              .Append(img.X).Append(',')
              .Append(img.Y)
              .Append('|');
        }

        return sb.ToString();
    }
    
    void SetStartupLocked(bool locked)
    {
        _startupLocked = locked;

        if (MainLeftPanels != null)
            MainLeftPanels.IsEnabled = !locked;

        RefreshCommandStates();
    }

    private void SetCurrentSide(DriveSide side)
    {
        _side = side;
        UpdateCurrentSideLabel();
        ApplyAmbientSideToViewer();
        RefreshAmbientUi();
    }

    private void UpdateCurrentSideLabel()
    {
        if (CurrentSideLabel == null)
            return;

        CurrentSideLabel.Text = _side == DriveSide.RHD ? "RHD" : "LHD";
    }

    private void ResetWorkspaceForCarChange()
    {
        if (_placingAmbientIndex != null)
            ExitAmbientPlacementMode();

        if (_movingAmbientIndex != null)
            ExitAmbientMoveMode();

        _gcaPath = null;
        _doc = null;
        _lastSavedDocSignature = null;

        _backgroundPath = null;
        _currentSessionIsCustom = false;
        _currentCarId = null;
        _currentCarName = null;
        _currentMib = null;

        _history.Clear();
        _ambientIdsInitiallyInDoc.Clear();

        for (int i = 0; i <= 22; i++)
        {
            ClearAmbientSlot(DriveSide.LHD, i);
            ClearAmbientSlot(DriveSide.RHD, i);

            _ambientRgbEnabledLhd[i] = false;
            _ambientRgbEnabledRhd[i] = false;
        }

        Viewer.LoadDocument(null);
        Viewer.ClearAllAmbient();
        Viewer.SetBackground(null);

        RefreshZonesUi();
        RefreshAmbientUi();
        UpdateAmbientAvailability();
        RefreshDirtyState();
    }

    private string GetEffectiveSaveDirectory()
    {
        var configured = AppSettingsStore.Current.DefaultSaveFolder;
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        if (!string.IsNullOrWhiteSpace(_gcaPath))
        {
            var gcaDir = Path.GetDirectoryName(_gcaPath);
            if (!string.IsNullOrWhiteSpace(gcaDir) && Directory.Exists(gcaDir))
                return gcaDir;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void SaveLastProjectSnapshot()
    {
        if (_startupLocked &&
            string.IsNullOrWhiteSpace(_backgroundPath) &&
            string.IsNullOrWhiteSpace(_gcaPath) &&
            string.IsNullOrWhiteSpace(_currentCarId) &&
            !_currentSessionIsCustom)
        {
            return;
        }

        var updated = AppSettingsStore.Current.Clone();

        updated.LastProjectIsCustom = _currentSessionIsCustom;
        updated.LastProjectCarId = _currentCarId;
        updated.LastProjectCarName = _currentCarName;
        updated.LastProjectMib = _currentMib;
        updated.LastProjectSide = _side == DriveSide.RHD ? "RHD" : "LHD";
        updated.LastProjectBackgroundPath = _backgroundPath;
        updated.LastProjectGcaPath = _gcaPath;

        AppSettingsStore.Save(updated);
    }

    private void TryAutoLoadLastProject()
    {
        var settings = AppSettingsStore.Current;
        if (!settings.AutoLoadLastProject)
            return;

        bool hasProjectContext =
            settings.LastProjectIsCustom ||
            !string.IsNullOrWhiteSpace(settings.LastProjectCarId) ||
            !string.IsNullOrWhiteSpace(settings.LastProjectBackgroundPath) ||
            !string.IsNullOrWhiteSpace(settings.LastProjectGcaPath);

        if (!hasProjectContext)
            return;

        var selectedSide = string.Equals(settings.LastProjectSide, "RHD", StringComparison.OrdinalIgnoreCase)
            ? DriveSide.RHD
            : DriveSide.LHD;

        ResetWorkspaceForCarChange();
        SetCurrentSide(selectedSide);

        _currentSessionIsCustom = settings.LastProjectIsCustom;
        _currentCarId = settings.LastProjectCarId;
        _currentCarName = settings.LastProjectCarName;
        _currentMib = settings.LastProjectMib;

        if (!string.IsNullOrWhiteSpace(settings.LastProjectBackgroundPath) &&
            File.Exists(settings.LastProjectBackgroundPath))
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(settings.LastProjectBackgroundPath);
            bi.EndInit();
            bi.Freeze();

            _backgroundPath = settings.LastProjectBackgroundPath;
            Viewer.SetBackground(bi);
            UpdateMibLabelFromBackground(bi);

            if (AppSettingsStore.Current.AutoFitViewerAfterBackgroundLoad)
                Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, ViewerHost.ActualHeight);
        }

        if (!_currentSessionIsCustom &&
            !string.IsNullOrWhiteSpace(_currentCarId) &&
            !string.IsNullOrWhiteSpace(_currentMib))
        {
            string carsRoot = CarCatalogLoader.GetCarsRoot();
            string carFolder = Path.Combine(carsRoot, _currentMib, _currentCarId);

            if (Directory.Exists(carFolder))
                LoadAmbientFeaturesFromCarFolder(carFolder, selectedSide);
        }

        if (!string.IsNullOrWhiteSpace(settings.LastProjectGcaPath) &&
            File.Exists(settings.LastProjectGcaPath) &&
            Viewer.HasBackground)
        {
            LoadGcaFromPath(settings.LastProjectGcaPath);
        }

        if (_currentSessionIsCustom)
        {
            CurrentCarLabel.Text = $"Car: Custom - {settings.LastProjectSide ?? "LHD"}";

            if (!Viewer.HasBackground)
                MibLabel.Text = "MIB: -";
        }
        else if (!string.IsNullOrWhiteSpace(_currentCarId))
        {
            var carName = string.IsNullOrWhiteSpace(_currentCarName) ? "Unknown" : _currentCarName;
            var mib = string.IsNullOrWhiteSpace(_currentMib) ? "-" : _currentMib;
            var sideText = settings.LastProjectSide ?? "LHD";

            CurrentCarLabel.Text = $"Car: {_currentCarId} - {carName} - {mib} - {sideText}";
        }

        SetStartupLocked(false);
        RefreshCommandStates();
        UpdateWindowTitle();
    }
    
    private bool TrySaveCurrentGca()
    {
        if (_doc == null)
            return true;

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "GCA (*.gca)|*.gca",
            Title = "Save GCA",
            InitialDirectory = GetEffectiveSaveDirectory(),
            FileName = _gcaPath != null ? Path.GetFileName(_gcaPath) : "menu.gca"
        };

        if (sfd.ShowDialog() != true)
            return false;

        GcaCodec.Save(sfd.FileName, _doc);
        _gcaPath = sfd.FileName;
        MarkDocumentClean();
        SaveLastProjectSnapshot();

        AppMessageBox.Show("GCA saved.");
        return true;
    }

    private bool TryConfirmDiscardChanges()
    {
        if (!AppSettingsStore.Current.ConfirmBeforeResettingWorkspace)
            return true;

        if (!IsDocumentDirty())
            return true;

        var result = AppMessageBox.Show(
            "The current GCA has unsaved changes.\n\nDo you want to save before continuing?",
            "Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.No)
            return true;

        return TrySaveCurrentGca();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ChooseCar_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ChooseCar_Click(sender, new RoutedEventArgs());
    }

    private void ChooseCar_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = true;
    }

    private void ImportBackground_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ImportBackground_Click(sender, new RoutedEventArgs());
    }

    private void ImportBackground_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !_startupLocked;
    }

    private void OpenGca_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        OpenGca_Click(sender, new RoutedEventArgs());
    }

    private void OpenGca_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !_startupLocked && Viewer != null && Viewer.HasBackground;
    }

    private void WireWindowEvents()
    {
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;
        Closing += (_, __) =>
        {
            SaveLastProjectSnapshot();
            SaveWindowPlacementToSettings();
        };
    }

    private void WireViewerEvents()
    {
        Viewer.ZoneDragCommitted += (_, beforeSnapshot) =>
        {
            if (_doc == null) return;
            _history.PushUndoSnapshot(CaptureState(beforeSnapshot));
            RefreshZonesUi();
            RefreshDirtyState();
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

            // Selection rules with AllZones:
            // - When AllZones is toggled ON, all suns are cleared.
            // - Clicking a sun while AllZones is ON disables AllZones.
            // - Clicking outside (zoneId == null) disables AllZones.
            if (zoneId == null)
            {
                if (_ignoreNextNullZoneSelection)
                {
                    _ignoreNextNullZoneSelection = false;
                    UpdateZoneOpacitySelection(null);
                    return;
                }

                if (_zoneOpacityAllZones)
                {
                    Viewer.SetOpacityAllZonesChecked(false);
                    _zoneOpacityAllZones = false;
                }

                UpdateZoneOpacitySelection(null);
                return;
            }

            if (_zoneOpacityAllZones)
            {
                Viewer.SetOpacityAllZonesChecked(false);
                _zoneOpacityAllZones = false;
            }

            UpdateZoneOpacitySelection(zoneId);
        };

        Viewer.SetZoneNames(_zoneCatalog.Names);

        Viewer.AmbientPlaceRequested += Viewer_AmbientPlaceRequested;

        Viewer.OpacityBarValueChanged += (_, v) =>
        {
            OnZoneOpacityValueChanged(v);
        };

        Viewer.OpacityAllZonesChanged += (_, enabled) =>
        {
            SetZoneOpacityAllZones(enabled);
        };

        Viewer.AmbientMoveCommitted += (_, e) =>
        {
            if (_doc == null) return;

            var img = _doc.Images.FirstOrDefault(x => x.Id == (ushort)e.Id);
            if (img == null) return;

            var clamped = Viewer.ClampAmbientTopLeft(e.Id, e.NewX, e.NewY);

            ushort finalX = (ushort)Math.Round(clamped.X);
            ushort finalY = (ushort)Math.Round(clamped.Y);

            if (img.X == finalX && img.Y == finalY)
            {
                ExitAmbientMoveMode();
                return;
            }

            _history.PushUndoSnapshot(CaptureState());

            img.X = finalX;
            img.Y = finalY;

            Viewer.RefreshAmbientIdFromDoc(e.Id);
            RefreshAmbientUi();
            RefreshDirtyState();

            ExitAmbientMoveMode();
        };
    }
}
