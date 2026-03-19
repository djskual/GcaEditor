namespace GcaEditor.Settings;

public sealed class AppSettings
{
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;
    public bool IncludePrereleaseVersionsInUpdateCheck { get; set; } = false;

    public bool RememberWindowSizeAndPosition { get; set; } = true;
    public bool ConfirmBeforeResettingWorkspace { get; set; } = true;

    public int MaxUndoHistory { get; set; } = 100;

    public bool InvertHorizontalTrackpadScrolling { get; set; } = true;

    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    public bool ConfirmBeforeDeletingZone { get; set; } = true;
    public bool ConfirmBeforeDeletingAmbientImage { get; set; } = true;

    public bool AutoFitViewerAfterBackgroundLoad { get; set; } = true;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            AutoCheckUpdatesOnStartup = AutoCheckUpdatesOnStartup,
            IncludePrereleaseVersionsInUpdateCheck = IncludePrereleaseVersionsInUpdateCheck,
            RememberWindowSizeAndPosition = RememberWindowSizeAndPosition,
            ConfirmBeforeResettingWorkspace = ConfirmBeforeResettingWorkspace,
            MaxUndoHistory = MaxUndoHistory,
            InvertHorizontalTrackpadScrolling = InvertHorizontalTrackpadScrolling,
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            WindowLeft = WindowLeft,
            WindowTop = WindowTop,
            ConfirmBeforeDeletingZone = ConfirmBeforeDeletingZone,
            ConfirmBeforeDeletingAmbientImage = ConfirmBeforeDeletingAmbientImage,
            AutoFitViewerAfterBackgroundLoad = AutoFitViewerAfterBackgroundLoad
        };
    }

    public void Normalize()
    {
        if (MaxUndoHistory < 10)
            MaxUndoHistory = 10;

        if (MaxUndoHistory > 500)
            MaxUndoHistory = 500;
    }
}
