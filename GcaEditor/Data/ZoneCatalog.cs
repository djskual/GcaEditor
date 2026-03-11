using System.IO;
using System.Text.Json;

namespace GcaEditor.Data;

public sealed class ZoneCatalog
{
    private readonly Dictionary<ushort, string> _names;

    private ZoneCatalog(Dictionary<ushort, string> names)
    {
        _names = names;
    }

    public IReadOnlyDictionary<ushort, string> Names => _names;

    public IEnumerable<ushort> KnownZoneIds => _names.Keys.OrderBy(x => x);

    public string GetName(ushort id)
        => _names.TryGetValue(id, out var n) ? n : $"Zone {id}";

    public static ZoneCatalog LoadOrDefault()
    {
        // fichier optionnel à côté de l’exe : Data/zones.json
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "zones.json");
        if (!File.Exists(path))
            return Default();

        try
        {
            // JSON format attendu:
            // { "0": "Center Console", "1": "Front Background Lighting", ... }
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (dict == null || dict.Count == 0)
                return Default();

            var names = new Dictionary<ushort, string>();
            foreach (var kv in dict)
            {
                if (ushort.TryParse(kv.Key, out var id))
                    names[id] = kv.Value;
            }

            if (names.Count == 0)
                return Default();

            return new ZoneCatalog(names);
        }
        catch
        {
            // si JSON cassé, on ne bloque pas l’app
            return Default();
        }
    }

    public static ZoneCatalog Default()
    {
        return new ZoneCatalog(new Dictionary<ushort, string>
        {
            { 0, "Center Console" },
            { 1, "Front Background Lighting" },
            { 5, "Footwell" },
            { 7, "Doors" },
            { 9, "Roof" },
        });
    }
}
