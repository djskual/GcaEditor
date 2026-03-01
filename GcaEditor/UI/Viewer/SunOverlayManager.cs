using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GcaEditor.Models;

namespace GcaEditor.UI.Viewer;

public sealed class SunOverlayManager
{
    private readonly ViewerContext _ctx;

    private readonly BitmapImage _sun;
    private readonly BitmapImage _sunSelected;

    private Dictionary<ushort, string> _zoneNames = new();

    private Image? _selectedSun;
    private GcaZone? _selectedZone;

    private readonly Dictionary<ushort, Image> _sunByZoneId = new();

    private GcaDocument? _doc;

    private GcaDocument? _dragBefore;
    private bool _dragMoved;
    private bool _isDragging;

    public event EventHandler<GcaDocument>? ZoneDragCommitted;

    // NEW: selection change event for MainWindow
    public event EventHandler<ushort?>? SelectedZoneChanged;

    public ushort? SelectedZoneId => _selectedZone?.Id;

    public SunOverlayManager(ViewerContext ctx)
    {
        _ctx = ctx;

        _sun = new BitmapImage(new Uri("pack://application:,,,/Assets/sun.png", UriKind.Absolute));
        _sun.Freeze();

        _sunSelected = new BitmapImage(new Uri("pack://application:,,,/Assets/sun_selected.png", UriKind.Absolute));
        _sunSelected.Freeze();

        // Click ailleurs -> deselect (mais pas si clic sur un sun)
        _ctx.ZoomRoot.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (_isDragging) return; // ne pas clear pendant un drag

            if (e.OriginalSource is Image img && img.Tag is GcaZone) return;
            ClearSelection();
        };
    }

    public void SetZoneNames(IReadOnlyDictionary<ushort, string> zoneNames)
    {
        _zoneNames = zoneNames.ToDictionary(k => k.Key, v => v.Value);

        if (_selectedZone != null)
            _ctx.SelectedZoneText.Text = GetZoneName(_selectedZone.Id);
    }

    public void SetDocument(GcaDocument? doc)
    {
        _doc = doc;
        Render();
    }

    public void Render()
    {
        _ctx.SunLayer.Children.Clear();
        _sunByZoneId.Clear();
        ClearSelection(raiseEvent: true);

        if (_doc == null) return;

        const double sunSize = 44;
        const double halfZone = 52.0;

        foreach (var z in _doc.Zones)
        {
            var sun = new Image
            {
                Source = _sun,
                Width = sunSize,
                Height = sunSize,
                Stretch = Stretch.Uniform,
                Tag = z,
                Cursor = Cursors.Hand
            };

            SetSunPositionFromZone(sun, z, sunSize);

            _sunByZoneId[z.Id] = sun;

            AttachDrag(sun, z, sunSize, halfZone);

            _ctx.SunLayer.Children.Add(sun);
        }
    }

    public bool SelectZoneById(ushort id)
    {
        if (_doc == null) return false;

        var zone = _doc.Zones.FirstOrDefault(z => z.Id == id);
        if (zone == null) return false;

        if (_sunByZoneId.TryGetValue(id, out var sun))
        {
            SelectSun(sun, zone, raiseEvent: true);
            return true;
        }

        return false;
    }

    public void ClearSelectionPublic() => ClearSelection(raiseEvent: true);

    private static void SetSunPositionFromZone(Image sun, GcaZone z, double sunSize)
    {
        double left = z.CenterX - sunSize / 2.0;
        double top = z.CenterY - sunSize / 2.0;
        Canvas.SetLeft(sun, left);
        Canvas.SetTop(sun, top);
    }

    private void SelectSun(Image sun, GcaZone zone, bool raiseEvent)
    {
        if (_selectedSun == sun) return;

        if (_selectedSun != null)
            _selectedSun.Source = _sun;

        _selectedSun = sun;
        _selectedZone = zone;

        _selectedSun.Source = _sunSelected;
        _ctx.SelectedZoneText.Text = GetZoneName(zone.Id);

        if (raiseEvent)
            SelectedZoneChanged?.Invoke(this, zone.Id);
    }

    private void ClearSelection(bool raiseEvent = true)
    {
        if (_selectedSun != null)
            _selectedSun.Source = _sun;

        _selectedSun = null;
        _selectedZone = null;

        _ctx.SelectedZoneText.Text = "";

        if (raiseEvent)
            SelectedZoneChanged?.Invoke(this, null);
    }

    private string GetZoneName(ushort zoneId)
    {
        if (_zoneNames.TryGetValue(zoneId, out var name))
            return $"{zoneId} - {name}";

        return $"Zone {zoneId}";
    }

    private System.Windows.Point ClampZoneCenter(System.Windows.Point center, double halfZone)
    {
        if (_ctx.Background == null)
            return center;

        double bgW = _ctx.Background.PixelWidth;
        double bgH = _ctx.Background.PixelHeight;

        double cx = center.X;
        double cy = center.Y;

        if (cx < halfZone) cx = halfZone;
        if (cy < halfZone) cy = halfZone;

        if (cx > bgW - halfZone) cx = bgW - halfZone;
        if (cy > bgH - halfZone) cy = bgH - halfZone;

        return new System.Windows.Point(cx, cy);
    }

    private void AttachDrag(Image sun, GcaZone zone, double sunSize, double halfZone)
    {
        bool dragging = false;
        System.Windows.Point startMouse = default;
        double startLeft = 0, startTop = 0;

        sun.LostMouseCapture += (s, e) => { _isDragging = false; };

        sun.MouseLeftButtonDown += (s, e) =>
        {
            // Selection + preparation drag
            SelectSun((Image)s, zone, raiseEvent: true);

            if (_doc != null)
            {
                _dragBefore = _doc.DeepClone();
                _dragMoved = false;
            }

            dragging = true;
            _isDragging = true;
            startMouse = e.GetPosition(_ctx.SunLayer);

            startLeft = Canvas.GetLeft(sun);
            startTop = Canvas.GetTop(sun);
            if (double.IsNaN(startLeft)) startLeft = 0;
            if (double.IsNaN(startTop)) startTop = 0;

            sun.CaptureMouse();
        };

        sun.MouseMove += (s, e) =>
        {
            if (!dragging) return;

            var p = e.GetPosition(_ctx.SunLayer);
            var dx = p.X - startMouse.X;
            var dy = p.Y - startMouse.Y;

            if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
                _dragMoved = true;

            double newLeft = startLeft + dx;
            double newTop = startTop + dy;

            // compute proposed center of zone
            double cx = newLeft + sunSize / 2.0;
            double cy = newTop + sunSize / 2.0;

            // clamp center so the 104x104 zone stays inside background
            var clamped = ClampZoneCenter(new System.Windows.Point(cx, cy), halfZone);
            cx = clamped.X;
            cy = clamped.Y;

            // recompute icon top-left from clamped center
            newLeft = cx - sunSize / 2.0;
            newTop = cy - sunSize / 2.0;

            Canvas.SetLeft(sun, newLeft);
            Canvas.SetTop(sun, newTop);

            zone.X1 = (ushort)Math.Round(cx - halfZone);
            zone.Y1 = (ushort)Math.Round(cy - halfZone);
            zone.X2 = (ushort)Math.Round(cx + halfZone);
            zone.Y2 = (ushort)Math.Round(cy - halfZone);
            zone.X3 = (ushort)Math.Round(cx + halfZone);
            zone.Y3 = (ushort)Math.Round(cy + halfZone);
            zone.X4 = (ushort)Math.Round(cx - halfZone);
            zone.Y4 = (ushort)Math.Round(cy + halfZone);

            e.Handled = true;
        };

        sun.MouseLeftButtonUp += (s, e) =>
        {
            if (!dragging) return;
            dragging = false;
            _isDragging = false;
            sun.ReleaseMouseCapture();
            e.Handled = true;

            if (_dragBefore != null && _dragMoved)
                ZoneDragCommitted?.Invoke(this, _dragBefore);

            _dragBefore = null;
            _dragMoved = false;
        };
    }
}
