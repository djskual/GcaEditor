using GcaEditor.Models;
using System.IO;
using System.Linq;

namespace GcaEditor.IO;

public static class GcaCodec
{
    private const ushort ExpectedMagic = 0xFECA;
    private const ushort SupportedVersion = 4;
    private const int HeaderSize = 8;
    private const int ZoneSize = 42;
    private const int ZonePayloadSize = 24;
    private const int ImageEntrySize = 8;

    public static GcaDocument Load(string path)
    {
        var data = File.ReadAllBytes(path);
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        RequireRemaining(br, HeaderSize, "Unexpected EOF while reading GCA header.");

        ushort magic = br.ReadUInt16();      // bytes CA FE => value FECA (little-endian)
        ushort version = br.ReadUInt16();    // 04 00

        if (magic != ExpectedMagic)
            throw new InvalidDataException($"Bad magic: 0x{magic:X4} (expected FECA / bytes CA FE).");

        if (version != SupportedVersion)
            throw new InvalidDataException($"Unsupported GCA version: {version} (only v4 implemented).");

        var doc = new GcaDocument { Version = version };

        doc.HeaderUnk0 = br.ReadUInt16();
        ushort zoneCount = br.ReadUInt16();

        for (int i = 0; i < zoneCount; i++)
        {
            RequireRemaining(br, ZoneSize, $"Unexpected EOF while reading zone {i}.");

            long zoneStart = br.BaseStream.Position;

            var z = new GcaZone
            {
                Id = br.ReadUInt16(),
                A = br.ReadUInt16(),
                B = br.ReadUInt16(),
                C = br.ReadUInt16(),

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

            long consumed = br.BaseStream.Position - zoneStart;
            long pad = ZoneSize - consumed;

            if (consumed != ZonePayloadSize)
                throw new InvalidDataException($"Zone {z.Id}: invalid payload size {consumed} (expected {ZonePayloadSize}).");

            if (pad < 0)
                throw new InvalidDataException($"Zone {z.Id}: entry overrun ({consumed} bytes read, expected {ZoneSize}).");

            br.BaseStream.Position += pad;
        }

        RequireRemaining(br, 2, "Unexpected EOF while reading image count.");
        ushort imgCount = br.ReadUInt16();

        for (int i = 0; i < imgCount; i++)
        {
            RequireRemaining(br, ImageEntrySize, $"Unexpected EOF while reading image entry {i}.");

            ushort id1 = br.ReadUInt16();
            ushort id2 = br.ReadUInt16();
            ushort x = br.ReadUInt16();
            ushort y = br.ReadUInt16();

            if (id1 != id2)
                throw new InvalidDataException(
                    $"Image entry {i}: mismatched duplicated ids (id1={id1}, id2={id2}).");

            doc.Images.Add(new GcaImageRef
            {
                Id = id1,
                X = x,
                Y = y
            });
        }

        RequireRemaining(br, 2, "Unexpected EOF while reading GCA trailer.");
        ushort trailer = br.ReadUInt16();

        if (trailer != 0x0000)
            throw new InvalidDataException($"Invalid GCA trailer: 0x{trailer:X4} (expected 0x0000).");

        if (br.BaseStream.Position != br.BaseStream.Length)
        {
            long extra = br.BaseStream.Length - br.BaseStream.Position;
            throw new InvalidDataException($"Unexpected extra data at end of file ({extra} bytes).");
        }

        return doc;
    }

    public static void Save(string path, GcaDocument doc)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));

        if (doc.Version != SupportedVersion)
            throw new InvalidDataException($"Unsupported GCA version for save: {doc.Version}.");

        var zonesToWrite = doc.Zones
            .OrderBy(z => z.Id)
            .ToList();

        var imagesToWrite = doc.Images
            .OrderBy(i => i.Id)
            .ToList();

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        bw.Write((ushort)ExpectedMagic);   // CA FE
        bw.Write((ushort)SupportedVersion);
        bw.Write(doc.HeaderUnk0);
        bw.Write((ushort)zonesToWrite.Count);

        foreach (var z in zonesToWrite)
        {
            long start = fs.Position;

            bw.Write(z.Id);
            bw.Write(z.A);
            bw.Write(z.B);
            bw.Write(z.C);

            bw.Write(z.X1); bw.Write(z.Y1);
            bw.Write(z.X2); bw.Write(z.Y2);
            bw.Write(z.X3); bw.Write(z.Y3);
            bw.Write(z.X4); bw.Write(z.Y4);

            long written = fs.Position - start;
            int pad = checked((int)(ZoneSize - written));

            if (pad < 0)
                throw new InvalidDataException($"Zone {z.Id}: wrote {written} bytes (> {ZoneSize}).");

            if (pad > 0)
                bw.Write(new byte[pad]);
        }

        bw.Write((ushort)imagesToWrite.Count);

        foreach (var img in imagesToWrite)
        {
            bw.Write(img.Id);
            bw.Write(img.Id);
            bw.Write(img.X);
            bw.Write(img.Y);
        }

        bw.Write((ushort)0x0000);
    }

    private static void RequireRemaining(BinaryReader br, int count, string message)
    {
        long remaining = br.BaseStream.Length - br.BaseStream.Position;
        if (remaining < count)
            throw new EndOfStreamException(message);
    }
}
