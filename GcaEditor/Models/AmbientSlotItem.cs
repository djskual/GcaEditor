namespace GcaEditor.Models;

public sealed class AmbientSlotItem
{
    public int Index { get; init; }
    public string Display { get; init; } = "";

    public override string ToString() => Display;
}
