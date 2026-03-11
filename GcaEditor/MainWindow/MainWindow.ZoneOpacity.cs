using GcaEditor.Data;

namespace GcaEditor;

public partial class MainWindow
{
    private ushort? _zoneOpacitySelectedZoneId;
    private bool _ignoreNextNullZoneSelection;

    // Per-zone memory. Missing entry means 1.0.
    private readonly Dictionary<ushort, double> _zoneOpacityByZone = new();

    // Current bar value (0..1). In zone mode, this matches selected zone.
    private double _zoneOpacityValue01 = 1.0;

    // When enabled, the bar controls all zones.
    private bool _zoneOpacityAllZones;

    private void InitZoneOpacityUi()
    {
        if (Viewer == null) return;

        Viewer.SetOpacityBarEnabled(false);
        Viewer.SetOpacityBarValue(_zoneOpacityValue01);
    }

    private static double Clamp01(double v)
    {
        if (v < 0) return 0;
        if (v > 1) return 1;
        return v;
    }

    // Called from Viewer.OpacityBarValueChanged
    private void OnZoneOpacityValueChanged(double v)
    {
        _zoneOpacityValue01 = Clamp01(v);

        if (_zoneOpacityAllZones)
        {
            if (_doc != null)
            {
                foreach (var z in _doc.Zones)
                    _zoneOpacityByZone[z.Id] = _zoneOpacityValue01;
            }

            ApplyZoneOpacityToLinkedSlots();
            return;
        }

        if (_zoneOpacitySelectedZoneId != null)
            _zoneOpacityByZone[_zoneOpacitySelectedZoneId.Value] = _zoneOpacityValue01;

        ApplyZoneOpacityToLinkedSlots();
    }

    private void SetZoneOpacityAllZones(bool enabled)
    {
        if (!_uiReady || Viewer == null)
        {
            _zoneOpacityAllZones = enabled;
            return;
        }

        if (enabled)
        {
            // Rule: only AllZones toggle resets everything to 100%.
            _zoneOpacityAllZones = true;
            _zoneOpacityByZone.Clear();
            _zoneOpacityValue01 = 1.0;

            Viewer.SetOpacityBarEnabled(true);
            Viewer.SetOpacityBarValue(1.0);

            // AllZones behaves like a selection: clear any selected zone
            // but do not treat this programmatic clear as a user click outside.
            _ignoreNextNullZoneSelection = true;
            Viewer.ClearSelection();

            ApplyZoneOpacityToLinkedSlots();
            return;
        }

        // Leaving AllZones: keep memory as-is.
        _zoneOpacityAllZones = false;
        UpdateZoneOpacitySelection(_zoneOpacitySelectedZoneId);
        ApplyZoneOpacityToLinkedSlots();
    }

    private void UpdateZoneOpacitySelection(ushort? zoneId)
    {
        _zoneOpacitySelectedZoneId = zoneId;

        if (!_uiReady || Viewer == null) return;

        // Enable if a zone is selected OR AllZones is enabled.
        Viewer.SetOpacityBarEnabled(_zoneOpacityAllZones || zoneId != null);

        if (_zoneOpacityAllZones)
        {
            Viewer.SetOpacityBarValue(_zoneOpacityValue01);
            ApplyZoneOpacityToLinkedSlots();
            return;
        }

        if (zoneId == null)
        {
            Viewer.SetOpacityBarValue(1.0);
            ApplyZoneOpacityToLinkedSlots();
            return;
        }

        // Sync bar to memorized value for selected zone
        if (_zoneOpacityByZone.TryGetValue(zoneId.Value, out var zv))
            _zoneOpacityValue01 = Clamp01(zv);
        else
            _zoneOpacityValue01 = 1.0;

        Viewer.SetOpacityBarValue(_zoneOpacityValue01);
        ApplyZoneOpacityToLinkedSlots();
    }

    private void ApplyZoneOpacityToLinkedSlots()
    {
        if (!_uiReady || Viewer == null) return;

        if (_zoneOpacityAllZones)
        {
            Viewer.SetAllAmbientDisplayedOpacity(_zoneOpacityValue01);
            return;
        }

        // Reset baseline
        Viewer.SetAllAmbientDisplayedOpacity(1.0);

        if (_doc == null) return;

        // Combine per-zone opacity per feature using MIN (most restrictive)
        var perFeature = new double[23];
        for (int i = 0; i < perFeature.Length; i++)
            perFeature[i] = 1.0;

        foreach (var z in _doc.Zones)
        {
            if (!_zoneOpacityByZone.TryGetValue(z.Id, out var zv))
                continue; // implicit 1.0

            zv = Clamp01(zv);

            for (int featureId = 0; featureId <= 22; featureId++)
            {
                if (!FeatureZoneMap.FeatureBelongsToZone(featureId, z.Id))
                    continue;

                if (zv < perFeature[featureId])
                    perFeature[featureId] = zv;
            }
        }

        for (int featureId = 0; featureId <= 22; featureId++)
        {
            double v = perFeature[featureId];
            if (v < 1.0)
                Viewer.SetAmbientDisplayedOpacity(featureId, v);
        }
    }
}
