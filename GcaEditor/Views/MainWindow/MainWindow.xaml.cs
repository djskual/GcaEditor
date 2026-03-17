using GcaEditor.Data;
using GcaEditor.Models;
using GcaEditor.UndoRedo;
using GcaEditor.UI.Dialogs;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

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

    public MainWindow()
    {
        InitializeComponent();

        UpdateWindowTitle();

        _zoneCatalog = ZoneCatalog.LoadOrDefault();
        _history = new UndoRedoStack<EditorState>(s => s.DeepClone(), maxUndo: 100);

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
            UpdateAmbientAvailability();
            RefreshCommandStates();
            UpdateWindowTitle();

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
                AppMessageBox.Show(
                    "No tag found on GitHub.",
                    "Update check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

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

                if (latestVersion == null || parsed.CompareTo(latestVersion.Value) > 0)
                {
                    latestVersion = parsed;
                    latestTag = tagName.Trim();
                }
            }

            if (latestVersion == null || string.IsNullOrWhiteSpace(latestTag))
            {
                AppMessageBox.Show(
                    "No valid version tag found on GitHub.",
                    "Update check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var currentTag = GetBuildTag();

            if (!TryParseTagVersion(currentTag, out var currentVersion))
            {
                AppMessageBox.Show(
                    $"Current version tag is invalid: {currentTag}",
                    "Update check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
            else
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
            AppMessageBox.Show(
                $"Unable to check updates.\n\n{ex.Message}",
                "Update error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }


    private readonly struct TagVersion : IComparable<TagVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string? PreLabel { get; }
        public int PreNumber { get; }

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

    private void UpdateWindowTitle()
    {
        var buildTag = GetBuildTag();
        var title = string.IsNullOrWhiteSpace(buildTag) || buildTag == "unknown"
            ? "GcaEditor"
            : $"GcaEditor {buildTag}";

        if (!string.IsNullOrWhiteSpace(_gcaPath))
            title += $" - {Path.GetFileName(_gcaPath)}";

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
