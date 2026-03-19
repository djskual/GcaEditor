using GcaEditor.Settings;
using System.Windows;

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

        UndoHistoryCombo.SelectedItem = _workingCopy.MaxUndoHistory;
        if (UndoHistoryCombo.SelectedItem == null)
            UndoHistoryCombo.SelectedItem = 100;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _workingCopy.AutoCheckUpdatesOnStartup = AutoCheckUpdatesCheck.IsChecked == true;
        _workingCopy.IncludePrereleaseVersionsInUpdateCheck = IncludePrereleaseCheck.IsChecked == true;
        _workingCopy.RememberWindowSizeAndPosition = RememberWindowPlacementCheck.IsChecked == true;
        _workingCopy.ConfirmBeforeResettingWorkspace = ConfirmBeforeResetCheck.IsChecked == true;
        _workingCopy.InvertHorizontalTrackpadScrolling = InvertHorizontalScrollCheck.IsChecked == true;

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
