namespace GcaEditor.Models;

public sealed class GcaEntry
{
    public int Id { get; set; }

    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }

    public byte[] ImagePayload { get; set; } = [];

    // uniquement pour preview locale
    public string? SourcePath { get; set; }
}