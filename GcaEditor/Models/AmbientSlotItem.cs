namespace GcaEditor.Models;

public sealed class AmbientSlotItem
{
    public int Index { get; init; }
    public string Display { get; init; } = "";

    // When checked, this feature is included in color tint operations.
    // Default is false (feature stays white).
    public bool RgbEnabled { get; set; }

    public override string ToString() => Display;
}
