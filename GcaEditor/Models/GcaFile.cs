using System.Collections.Generic;

namespace GcaEditor.Models;

public sealed class GcaFile
{
    public uint Magic { get; set; }
    public ushort Version { get; set; }

    public List<GcaEntry> Entries { get; } = new();
}
