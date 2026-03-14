using GcaEditor.Data;
using GcaEditor.Models;
using GcaEditor.UndoRedo;
using System.Windows;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using GcaEditor.Services;

namespace GcaEditor;

public partial class MainWindow : Window
{
    private GcaDocument? _doc;
    private string? _gcaPath;

    private readonly UndoRedoStack<EditorState> _history;
    private readonly ZoneCatalog _zoneCatalog;

    private bool _suppressListSelection;
    private bool _uiReady = false;

    public MainWindow()
    {
        InitializeComponent();

        Title = $"GcaEditor {GetBuildTag()}";

        _zoneCatalog = ZoneCatalog.LoadOrDefault();
        _history = new UndoRedoStack<EditorState>(s => s.DeepClone());

        WireViewerEvents();
        WireWindowEvents();

        // Startup: lock the UI until Choose car (or Custom) is selected
        SetStartupLocked(true);

        RefreshZonesUi();

        Loaded += (_, __) =>
        {
            _uiReady = true;

            InitAmbientUiOnLoaded();
            UpdateAmbientAvailability();

            InitZoneOpacityUi();
        };
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
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
                MessageBox.Show(
                    "No tag found on GitHub.",
                    "Update check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var latestTag = doc.RootElement[0]
                .GetProperty("name")
                .GetString();

            var currentVersion = GetBuildTag();

            if (!string.Equals(latestTag?.Trim(), currentVersion?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var result = MessageBox.Show(
                    $"New version available: {latestTag}\n\nUpdate now?",
                    "Update available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    var updaterPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Updater.exe");

                    if (!File.Exists(updaterPath))
                    {
                        MessageBox.Show(
                            "Updater.exe not found.",
                            "Update error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    UpdateLauncher.LaunchUpdater(currentVersion ?? "0.0.0");

                    Application.Current.Shutdown();
                }
            }
            else
            {
                MessageBox.Show(
                    "You already have the latest version.",
                    "No update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to check updates.\n\n{ex.Message}",
                "Update error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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
    
    void SetStartupLocked(bool locked)
    {
        // Choose car is always available
        if (ChooseCarButton != null)
            ChooseCarButton.IsEnabled = true;

        // Disable everything else until a profile or Custom is selected
        if (MainControlsPanel != null)
            MainControlsPanel.IsEnabled = !locked;

        if (MainLeftPanels != null)
            MainLeftPanels.IsEnabled = !locked;

        if (locked)
        {
            // Keep these coherent even if panels are enabled later
            if (OpenGcaButton != null) OpenGcaButton.IsEnabled = false;
            if (SaveGcaButton != null) SaveGcaButton.IsEnabled = false;
        }
    }

    private void WireWindowEvents()
    {
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;
    }

    private void WireViewerEvents()
    {
        Viewer.ZoneDragCommitted += (_, beforeSnapshot) =>
        {
            if (_doc == null) return;
            _history.PushUndoSnapshot(CaptureState(beforeSnapshot));
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

            // Snapshot before mutating doc
            _history.PushUndoSnapshot(CaptureState());

            var img = _doc.Images.FirstOrDefault(x => x.Id == (ushort)e.Id);
            if (img == null) return;

            var clamped = Viewer.ClampAmbientTopLeft(e.Id, e.NewX, e.NewY);

            img.X = (ushort)Math.Round(clamped.X);
            img.Y = (ushort)Math.Round(clamped.Y);

            Viewer.LoadDocument(_doc);
            ApplyAmbientSideToViewer();
            RefreshAmbientUi();

            ExitAmbientMoveMode();
        };
    }
}
