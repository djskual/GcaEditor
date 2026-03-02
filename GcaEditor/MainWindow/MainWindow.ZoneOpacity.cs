using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GcaEditor.Data;

namespace GcaEditor;

public partial class MainWindow
{
    private bool _zoneOpacityDragging;
    private double _zoneOpacityValue = 1.0;
    private ushort? _zoneOpacitySelectedZoneId;
    private readonly List<int> _zoneOpacityLinkedSlots = new();

    private void InitZoneOpacityUi()
    {
        // Load UI images (bar.png and cursor.png) from embedded Resources
        try
        {
            ZoneOpacityBar.Source = LoadPackBitmap("pack://application:,,,/Assets/bar.png");
            ZoneOpacityCursor.Source = LoadPackBitmap("pack://application:,,,/Assets/cursor.png");
        }
        catch
        {
            ZoneOpacityPanel.Visibility = Visibility.Collapsed;
        }

        ZoneOpacityCanvas.MouseLeftButtonDown += ZoneOpacityCanvas_MouseLeftButtonDown;
        ZoneOpacityCanvas.MouseMove += ZoneOpacityCanvas_MouseMove;
        ZoneOpacityCanvas.MouseLeftButtonUp += ZoneOpacityCanvas_MouseLeftButtonUp;
        ZoneOpacityCanvas.LostMouseCapture += ZoneOpacityCanvas_LostMouseCapture;

        ApplyZoneOpacityValueToCursor();
    }

    private static BitmapSource LoadPackBitmap(string uri)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new Uri(uri, UriKind.Absolute);
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    private void UpdateZoneOpacitySelection(ushort? zoneId)
    {
        _zoneOpacitySelectedZoneId = zoneId;

        if (zoneId == null || _doc == null)
        {
            ZoneOpacityPanel.Visibility = Visibility.Collapsed;
            _zoneOpacityLinkedSlots.Clear();

            if (Viewer != null)
                Viewer.SetAllAmbientDisplayedOpacity(1.0);

            return;
        }

        string zoneName;
        if (_zoneCatalog.Names.TryGetValue(zoneId.Value, out var n) && !string.IsNullOrWhiteSpace(n))
            zoneName = n;
        else
            zoneName = "Zone " + zoneId.Value;

        ZoneOpacityLabel.Text = zoneName;
        ZoneOpacityPanel.Visibility = Visibility.Visible;

        RebuildZoneOpacityLinkedSlots(zoneId.Value);
        ApplyZoneOpacityToLinkedSlots();
    }

    private void RebuildZoneOpacityLinkedSlots(ushort zoneId)
    {
        _zoneOpacityLinkedSlots.Clear();
        if (_doc == null) return;

        // Features are linked to zones by mapping.
        foreach (var img in _doc.Images)
        {
            int idx = img.Id;

            if (FeatureZoneMap.TryGetZoneForFeature(idx, out var z) && z == zoneId)
                _zoneOpacityLinkedSlots.Add(idx);
        }
    }

    private void ApplyZoneOpacityToLinkedSlots()
    {
        if (_doc == null || Viewer == null) return;

        // Reset all displayed images to full opacity
        Viewer.SetAllAmbientDisplayedOpacity(1.0);

        // Apply selected opacity only to linked slots
        foreach (var idx in _zoneOpacityLinkedSlots)
            Viewer.SetAmbientDisplayedOpacity(idx, _zoneOpacityValue);
    }

    private void ZoneOpacityCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_zoneOpacitySelectedZoneId == null) return;

        _zoneOpacityDragging = true;
        ZoneOpacityCanvas.CaptureMouse();
        SetZoneOpacityFromMouse(e.GetPosition(ZoneOpacityCanvas).X);
        e.Handled = true;
    }

    private void ZoneOpacityCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_zoneOpacityDragging) return;
        SetZoneOpacityFromMouse(e.GetPosition(ZoneOpacityCanvas).X);
    }

    private void ZoneOpacityCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_zoneOpacityDragging) return;
        _zoneOpacityDragging = false;
        ZoneOpacityCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void ZoneOpacityCanvas_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _zoneOpacityDragging = false;
    }

    private void SetZoneOpacityFromMouse(double mouseX)
    {
        double w = ZoneOpacityBar.ActualWidth;
        if (w <= 1) w = ZoneOpacityCanvas.ActualWidth;
        if (w <= 1) return;

        double v = mouseX / w;
        if (v < 0) v = 0;
        if (v > 1) v = 1;

        // Left = 0% (transparent), Right = 100% (full)
        _zoneOpacityValue = v;

        ApplyZoneOpacityValueToCursor();
        ApplyZoneOpacityToLinkedSlots();
    }

    private void ApplyZoneOpacityValueToCursor()
    {
        double w = ZoneOpacityBar.ActualWidth;
        if (w <= 1) w = ZoneOpacityCanvas.ActualWidth;
        if (w <= 1) return;

        double cursorW = ZoneOpacityCursor.ActualWidth;
        if (cursorW <= 1) cursorW = 12;

        double x = (_zoneOpacityValue * w) - (cursorW / 2.0);
        if (x < 0) x = 0;
        if (x > w - cursorW) x = w - cursorW;

        System.Windows.Controls.Canvas.SetLeft(ZoneOpacityCursor, x);
    }
}
