namespace GcaEditor.Models;

public sealed class ZoneListItem
{
    public ushort Id { get; init; }
    public string Display { get; init; } = "";

    public override string ToString() => Display;
}
