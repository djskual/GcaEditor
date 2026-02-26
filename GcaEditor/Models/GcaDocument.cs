using System.Collections.Generic;

namespace GcaEditor.Models;

public sealed class GcaDocument
{
    public ushort Version { get; set; } = 4;

    // 2 bytes après version (souvent 0x0000)
    public ushort HeaderUnk0 { get; set; }

    public List<GcaZone> Zones { get; } = new();
    public List<GcaImageRef> Images { get; } = new();

    public GcaDocument DeepClone()
    {
        var dst = new GcaDocument
        {
            Version = Version,
            HeaderUnk0 = HeaderUnk0
        };

        foreach (var z in Zones)
        {
            dst.Zones.Add(new GcaZone
            {
                Id = z.Id,
                A = z.A,
                B = z.B,
                C = z.C,
                X1 = z.X1,
                Y1 = z.Y1,
                X2 = z.X2,
                Y2 = z.Y2,
                X3 = z.X3,
                Y3 = z.Y3,
                X4 = z.X4,
                Y4 = z.Y4
            });
        }

        foreach (var img in Images)
        {
            dst.Images.Add(new GcaImageRef
            {
                Id = img.Id,
                X = img.X,
                Y = img.Y
            });
        }

        return dst;
    }
}
