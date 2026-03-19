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

    public string? DefaultSaveFolder { get; set; }

    public bool AutoLoadLastProject { get; set; } = false;

    public bool LastProjectIsCustom { get; set; }
    public string? LastProjectCarId { get; set; }
    public string? LastProjectCarName { get; set; }
    public string? LastProjectMib { get; set; }
    public string? LastProjectSide { get; set; }
    public string? LastProjectBackgroundPath { get; set; }
    public string? LastProjectGcaPath { get; set; }

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
            AutoFitViewerAfterBackgroundLoad = AutoFitViewerAfterBackgroundLoad,
            DefaultSaveFolder = DefaultSaveFolder,
            AutoLoadLastProject = AutoLoadLastProject,
            LastProjectIsCustom = LastProjectIsCustom,
            LastProjectCarId = LastProjectCarId,
            LastProjectCarName = LastProjectCarName,
            LastProjectMib = LastProjectMib,
            LastProjectSide = LastProjectSide,
            LastProjectBackgroundPath = LastProjectBackgroundPath,
            LastProjectGcaPath = LastProjectGcaPath,
        };
    }

    public void Normalize()
    {
        if (MaxUndoHistory < 10)
            MaxUndoHistory = 10;

        if (MaxUndoHistory > 500)
            MaxUndoHistory = 500;

        if (string.IsNullOrWhiteSpace(DefaultSaveFolder))
            DefaultSaveFolder = null;
        else
            DefaultSaveFolder = DefaultSaveFolder.Trim();

        if (string.IsNullOrWhiteSpace(LastProjectCarId))
            LastProjectCarId = null;

        if (string.IsNullOrWhiteSpace(LastProjectCarName))
            LastProjectCarName = null;

        if (string.IsNullOrWhiteSpace(LastProjectMib))
            LastProjectMib = null;

        if (string.IsNullOrWhiteSpace(LastProjectSide))
            LastProjectSide = null;

        if (string.IsNullOrWhiteSpace(LastProjectBackgroundPath))
            LastProjectBackgroundPath = null;

        if (string.IsNullOrWhiteSpace(LastProjectGcaPath))
            LastProjectGcaPath = null;
    }
}
