using System;
using System.Collections.Generic;
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

    private readonly Dictionary<ushort, string> _zoneNames = new()
    {
        { 0, "Center Console" },
        { 1, "Front Background Lighting" },
        { 5, "Footwell" },
        { 7, "Doors" },
        { 9, "Roof" },
    };

    private Image? _selectedSun;
    private GcaZone? _selectedZone;

    private GcaDocument? _doc;

    private GcaDocument? _dragBefore;
    private bool _dragMoved;

    public event EventHandler<GcaDocument>? ZoneDragCommitted;

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
            if (e.OriginalSource is Image img && img.Tag is GcaZone) return;
            ClearSelection();
        };
    }

    public void SetDocument(GcaDocument? doc)
    {
        _doc = doc;
        Render();
    }

    public void Render()
    {
        _ctx.SunLayer.Children.Clear();
        ClearSelection();

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

            AttachDrag(sun, z, sunSize, halfZone);

            _ctx.SunLayer.Children.Add(sun);
        }
    }

    private static void SetSunPositionFromZone(Image sun, GcaZone z, double sunSize)
    {
        double left = z.CenterX - sunSize / 2.0;
        double top = z.CenterY - sunSize / 2.0;
        Canvas.SetLeft(sun, left);
        Canvas.SetTop(sun, top);
    }

    private void SelectSun(Image sun, GcaZone zone)
    {
        if (_selectedSun == sun) return;

        if (_selectedSun != null)
            _selectedSun.Source = _sun;

        _selectedSun = sun;
        _selectedZone = zone;

        _selectedSun.Source = _sunSelected;
        _ctx.SelectedZoneText.Text = GetZoneName(zone.Id);
    }

    private void ClearSelection()
    {
        if (_selectedSun != null)
            _selectedSun.Source = _sun;

        _selectedSun = null;
        _selectedZone = null;
        _ctx.SelectedZoneText.Text = "";
    }

    private string GetZoneName(ushort zoneId)
    {
        if (_zoneNames.TryGetValue(zoneId, out var name))
            return $"{zoneId} - {name}";
        return $"Zone {zoneId}";
    }

    private void AttachDrag(Image sun, GcaZone zone, double sunSize, double halfZone)
    {
        bool dragging = false;
        System.Windows.Point startMouse = default;
        double startLeft = 0, startTop = 0;

        sun.MouseLeftButtonDown += (s, e) =>
        {
            // selection + preparation drag
            SelectSun((Image)s, zone);

            if (_doc != null)
            {
                _dragBefore = _doc.DeepClone();
                _dragMoved = false;
            }

            dragging = true;
            startMouse = e.GetPosition(_ctx.SunLayer);

            startLeft = Canvas.GetLeft(sun);
            startTop = Canvas.GetTop(sun);
            if (double.IsNaN(startLeft)) startLeft = 0;
            if (double.IsNaN(startTop)) startTop = 0;

            sun.CaptureMouse();

            // Optionnel: ne pas marquer handled ici pour ne pas casser d'autres interactions
            // e.Handled = true;
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

            Canvas.SetLeft(sun, newLeft);
            Canvas.SetTop(sun, newTop);

            double cx = newLeft + sunSize / 2.0;
            double cy = newTop + sunSize / 2.0;

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
            sun.ReleaseMouseCapture();
            e.Handled = true;

            if (_dragBefore != null && _dragMoved)
                ZoneDragCommitted?.Invoke(this, _dragBefore);

            _dragBefore = null;
            _dragMoved = false;
        };
    }
}
