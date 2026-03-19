using GcaEditor.Settings;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace GcaEditor.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _workingCopy;

    public AppSettings ResultSettings { get; private set; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        _workingCopy = settings.Clone();
        ResultSettings = _workingCopy.Clone();

        UndoHistoryCombo.ItemsSource = new[] { 50, 100, 150, 200, 300 };

        AutoCheckUpdatesCheck.IsChecked = _workingCopy.AutoCheckUpdatesOnStartup;
        IncludePrereleaseCheck.IsChecked = _workingCopy.IncludePrereleaseVersionsInUpdateCheck;
        RememberWindowPlacementCheck.IsChecked = _workingCopy.RememberWindowSizeAndPosition;
        ConfirmBeforeResetCheck.IsChecked = _workingCopy.ConfirmBeforeResettingWorkspace;
        InvertHorizontalScrollCheck.IsChecked = _workingCopy.InvertHorizontalTrackpadScrolling;
        ConfirmBeforeDeleteZoneCheck.IsChecked = _workingCopy.ConfirmBeforeDeletingZone;
        ConfirmBeforeDeleteAmbientCheck.IsChecked = _workingCopy.ConfirmBeforeDeletingAmbientImage;
        AutoFitViewerAfterBackgroundLoadCheck.IsChecked = _workingCopy.AutoFitViewerAfterBackgroundLoad;

        UndoHistoryCombo.SelectedItem = _workingCopy.MaxUndoHistory;
        if (UndoHistoryCombo.SelectedItem == null)
            UndoHistoryCombo.SelectedItem = 100;

        Loaded += SettingsWindow_Loaded;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ShowSection(0);
    }

    private void SectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ShowSection(SectionList.SelectedIndex);
    }

    private void ShowSection(int index)
    {
        if (GeneralPanel == null || EditorPanel == null || ViewerPanel == null || UpdatesPanel == null || SectionHost == null)
            return;

        if (index < 0)
            index = 0;

        GeneralPanel.Visibility = Visibility.Collapsed;
        EditorPanel.Visibility = Visibility.Collapsed;
        ViewerPanel.Visibility = Visibility.Collapsed;
        UpdatesPanel.Visibility = Visibility.Collapsed;

        switch (index)
        {
            case 0:
                GeneralPanel.Visibility = Visibility.Visible;
                break;

            case 1:
                EditorPanel.Visibility = Visibility.Visible;
                break;

            case 2:
                ViewerPanel.Visibility = Visibility.Visible;
                break;

            case 3:
                UpdatesPanel.Visibility = Visibility.Visible;
                break;

            default:
                GeneralPanel.Visibility = Visibility.Visible;
                break;
        }

        SectionHost.Opacity = 0.35;

        var fade = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(220)
        };

        SectionHost.BeginAnimation(OpacityProperty, fade);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _workingCopy.AutoCheckUpdatesOnStartup = AutoCheckUpdatesCheck.IsChecked == true;
        _workingCopy.IncludePrereleaseVersionsInUpdateCheck = IncludePrereleaseCheck.IsChecked == true;
        _workingCopy.RememberWindowSizeAndPosition = RememberWindowPlacementCheck.IsChecked == true;
        _workingCopy.ConfirmBeforeResettingWorkspace = ConfirmBeforeResetCheck.IsChecked == true;
        _workingCopy.InvertHorizontalTrackpadScrolling = InvertHorizontalScrollCheck.IsChecked == true;
        _workingCopy.ConfirmBeforeDeletingZone = ConfirmBeforeDeleteZoneCheck.IsChecked == true;
        _workingCopy.ConfirmBeforeDeletingAmbientImage = ConfirmBeforeDeleteAmbientCheck.IsChecked == true;
        _workingCopy.AutoFitViewerAfterBackgroundLoad = AutoFitViewerAfterBackgroundLoadCheck.IsChecked == true;

        if (UndoHistoryCombo.SelectedItem is int undoHistory)
            _workingCopy.MaxUndoHistory = undoHistory;
        else
            _workingCopy.MaxUndoHistory = 100;

        _workingCopy.Normalize();
        ResultSettings = _workingCopy.Clone();

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
