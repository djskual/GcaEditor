namespace GcaEditor.Data;

public static class FeatureZoneMap
{
    // Mapping between feature slot index (0..22) and zone id.
    // Unknown/unmapped features are simply not included.
    private static readonly Dictionary<int, ushort> _featureToZone = new()
    {
        // Zone 0 - Center console
        { 0, 0 },
        { 11, 0 },
        { 12, 0 },

        // Zone 1 - Front background lighting
        { 1, 1 },
        { 7, 1 },
        { 10, 1 },
        { 13, 1 },

        // Zone 5 - Footwell
        { 5, 5 },
        { 15, 5 },

        // Zone 7 - Doors
        { 2, 7 },
        { 3, 7 },
        { 8, 7 },
        { 14, 7 },
        { 16, 7 },
        { 19, 7 },
        { 20, 7 },

        // Zone 9 - Roof
        { 6, 9 },
        { 21, 9 },
        { 22, 9 },
    };

    public static bool TryGetZoneForFeature(int featureId, out ushort zoneId)
    {
        return _featureToZone.TryGetValue(featureId, out zoneId);
    }

    public static bool FeatureBelongsToZone(int featureId, int zoneId)
    {
        return TryGetZoneForFeature(featureId, out var z) && z == zoneId;
    }
}
