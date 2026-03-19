namespace GcaEditor.Settings;

public sealed class LastProjectAmbientEntry
{
    public int Index { get; set; }
    public string Side { get; set; } = "LHD";
    public string Path { get; set; } = string.Empty;
}

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
    public List<LastProjectAmbientEntry> LastProjectAmbientFiles { get; set; } = new();

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
            LastProjectAmbientFiles = LastProjectAmbientFiles
                .Select(x => new LastProjectAmbientEntry
                {
                    Index = x.Index,
                    Side = x.Side,
                    Path = x.Path
                })
                .ToList(),
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

        LastProjectAmbientFiles = LastProjectAmbientFiles
            .Where(x => x != null
                     && x.Index >= 0
                     && x.Index <= 22
                     && !string.IsNullOrWhiteSpace(x.Side)
                     && !string.IsNullOrWhiteSpace(x.Path))
            .Select(x => new LastProjectAmbientEntry
            {
                Index = x.Index,
                Side = x.Side.Trim().ToUpperInvariant(),
                Path = x.Path.Trim()
            })
            .Where(x => x.Side == "LHD" || x.Side == "RHD")
            .ToList();
            }
}
