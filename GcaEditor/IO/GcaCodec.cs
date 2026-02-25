using GcaEditor.Models;

namespace GcaEditor.IO;

public static class GcaCodec
{
    public static GcaFile Load(string path)
    {
        // Placeholder volontaire
        return new GcaFile
        {
            Magic = 0x47434100, // "GCA\0"
            Version = 1
        };
    }

    public static void Save(string path, GcaFile gca)
    {
        // Placeholder
        // Le vrai writer viendra plus tard
    }
}