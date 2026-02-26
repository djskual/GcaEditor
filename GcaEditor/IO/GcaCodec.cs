using System;
using System.IO;
using GcaEditor.Models;

namespace GcaEditor.IO;

public static class GcaCodec
{
    public static GcaDocument Load(string path)
    {
        var data = File.ReadAllBytes(path);
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        ushort magic = br.ReadUInt16();      // bytes CA FE => value FECA (little-endian)
        ushort version = br.ReadUInt16();    // 04 00

        if (magic != 0xFECA)
            throw new InvalidDataException($"Bad magic: 0x{magic:X4} (expected FECA / bytes CA FE)");

        if (version != 4)
            throw new InvalidDataException($"Unsupported GCA version: {version} (only v4 implemented)");

        var doc = new GcaDocument { Version = version };

        doc.HeaderUnk0 = br.ReadUInt16();
        ushort zoneCount = br.ReadUInt16();

        // Zones: 42 bytes fixed
        for (int i = 0; i < zoneCount; i++)
        {
            long zoneStart = br.BaseStream.Position;

            var z = new GcaZone
            {
                Id = br.ReadUInt16(),
                A = br.ReadUInt16(), // 0x0010
                B = br.ReadUInt16(), // 0x0010
                C = br.ReadUInt16(), // 0x0004

                X1 = br.ReadUInt16(),
                Y1 = br.ReadUInt16(),
                X2 = br.ReadUInt16(),
                Y2 = br.ReadUInt16(),
                X3 = br.ReadUInt16(),
                Y3 = br.ReadUInt16(),
                X4 = br.ReadUInt16(),
                Y4 = br.ReadUInt16(),
            };

            doc.Zones.Add(z);

            long consumed = br.BaseStream.Position - zoneStart; // 24 bytes
            long pad = 42 - consumed;                           // 18 bytes
            if (pad < 0) throw new InvalidDataException($"Zone entry overrun (consumed={consumed})");
            br.BaseStream.Position += pad;
        }

        if (br.BaseStream.Position + 2 > br.BaseStream.Length)
            return doc;

        ushort imgCount = br.ReadUInt16();

        for (int i = 0; i < imgCount; i++)
        {
            if (br.BaseStream.Position + 8 > br.BaseStream.Length)
                throw new EndOfStreamException("Unexpected EOF while reading image table");

            ushort id1 = br.ReadUInt16();
            ushort id2 = br.ReadUInt16();
            ushort x = br.ReadUInt16();
            ushort y = br.ReadUInt16();

            doc.Images.Add(new GcaImageRef { Id = id1, X = x, Y = y });
        }

        return doc;
    }

    public static void Save(string path, GcaDocument doc)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        bw.Write((ushort)0xFECA);  // CA FE
        bw.Write((ushort)4);       // 04 00
        bw.Write(doc.HeaderUnk0);
        bw.Write((ushort)doc.Zones.Count);

        foreach (var z in doc.Zones)
        {
            long start = fs.Position;

            bw.Write((ushort)z.Id);

            bw.Write(z.A != 0 ? z.A : (ushort)0x0010);
            bw.Write(z.B != 0 ? z.B : (ushort)0x0010);
            bw.Write(z.C != 0 ? z.C : (ushort)0x0004);

            bw.Write((ushort)z.X1); bw.Write((ushort)z.Y1);
            bw.Write((ushort)z.X2); bw.Write((ushort)z.Y2);
            bw.Write((ushort)z.X3); bw.Write((ushort)z.Y3);
            bw.Write((ushort)z.X4); bw.Write((ushort)z.Y4);

            long written = fs.Position - start;
            int pad = checked((int)(42 - written));
            if (pad < 0) throw new InvalidDataException($"Zone {z.Id}: wrote {written} bytes (>42)");
            if (pad > 0) bw.Write(new byte[pad]);
        }

        bw.Write((ushort)doc.Images.Count);
        foreach (var img in doc.Images)
        {
            bw.Write((ushort)img.Id);
            bw.Write((ushort)img.Id);
            bw.Write((ushort)img.X);
            bw.Write((ushort)img.Y);
        }

        bw.Write((ushort)0x0000);
    }
}
