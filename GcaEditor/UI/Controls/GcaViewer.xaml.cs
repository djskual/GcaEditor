using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GcaEditor.Models;

namespace GcaEditor.UI.Controls;

public partial class GcaViewer : UserControl
{
    private BitmapSource? _background;
    private BitmapImage? _sunIcon;
    private BitmapImage? _sunSelectedIcon;

    private double _zoom = 1.0;
    private double _minZoom = 1.0;

    private GcaDocument? _doc;

    // Drag -> undo snapshot “before”
    private GcaDocument? _dragBeforeSnapshot;
    private bool _dragMoved;

    // Selection
    private Image? _selectedSun;
    private GcaZone? _selectedZone;

    // Zone name DB (temp: on passera en JSON plus tard)
    private readonly Dictionary<ushort, string> _zoneNames = new()
    {
        { 0, "Center Console" },
        { 1, "Front Background Lighting" },
        { 5, "Footwell" },
        { 7, "Doors" },
        { 9, "Roof" },
    };

    public event EventHandler<GcaDocument>? ZoneDragCommitted;

    public bool HasBackground => _background != null;

    public GcaViewer()
    {
        InitializeComponent();

        _sunIcon = new BitmapImage(new Uri("pack://application:,,,/Assets/sun.png", UriKind.Absolute));
        _sunIcon.Freeze();

        _sunSelectedIcon = new BitmapImage(new Uri("pack://application:,,,/Assets/sun_selected.png", UriKind.Absolute));
        _sunSelectedIcon.Freeze();

        Loaded += (_, __) =>
        {
            var source = (HwndSource)PresentationSource.FromVisual(this)!;
            source.AddHook(WndProc);
        };

        // Clic "ailleurs" (sans toucher un sun) => deselect
        // On passe en Preview pour être sûr de capter le clic.
        ZoomRoot.PreviewMouseLeftButtonDown += (s, e) =>
        {
            // Si on clique sur un sun (Image avec Tag=GcaZone), on ne clear pas ici
            if (e.OriginalSource is Image img && img.Tag is GcaZone)
                return;

            ClearSelection();
        };
    }

    public void SetBackground(BitmapSource? bg)
    {
        _background = bg;
        BackgroundImage.Source = bg;

        if (bg == null)
            return;

        BackgroundImage.Width = bg.PixelWidth;
        BackgroundImage.Height = bg.PixelHeight;

        AmbientLayer.Width = bg.PixelWidth;
        AmbientLayer.Height = bg.PixelHeight;
        SunLayer.Width = bg.PixelWidth;
        SunLayer.Height = bg.PixelHeight;

        // Fit stable après layout réel
        Dispatcher.BeginInvoke(new Action(FitNowStable),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void SizeToHostAndFit(double hostWidth, double hostHeight)
    {
        if (_background == null) return;
        if (hostWidth <= 0 || hostHeight <= 0) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateLayout();

            double chromeW = Math.Max(0, ActualWidth - EditorScroll.ViewportWidth);
            double chromeH = Math.Max(0, ActualHeight - EditorScroll.ViewportHeight);

            const double outerMargin = 8;
            double maxViewportW = Math.Max(100, hostWidth - outerMargin - chromeW);
            double maxViewportH = Math.Max(100, hostHeight - outerMargin - chromeH);

            double imgW = _background.PixelWidth;
            double imgH = _background.PixelHeight;

            double scale = Math.Min(maxViewportW / imgW, maxViewportH / imgH);
            scale = Math.Min(scale, 1.0);

            double viewportW = Math.Floor(imgW * scale);
            double viewportH = Math.Floor(imgH * scale);

            Width = viewportW + chromeW;
            Height = viewportH + chromeH;

            Dispatcher.BeginInvoke(new Action(FitNowStable),
                System.Windows.Threading.DispatcherPriority.Loaded);

        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void LoadDocument(GcaDocument? doc)
    {
        _doc = doc;
        RenderZones();
    }

    private void RenderZones()
    {
        SunLayer.Children.Clear();
        ClearSelection();

        if (_doc == null || _sunIcon == null) return;

        const double sunSize = 44;
        const double halfZone = 52.0;

        foreach (var z in _doc.Zones)
        {
            var sun = new Image
            {
                Source = _sunIcon,
                Width = sunSize,
                Height = sunSize,
                Stretch = Stretch.Uniform,
                Tag = z,
                Cursor = Cursors.Hand
            };

            SetSunPositionFromZone(sun, z, sunSize);
            MakeSunDraggableUpdatingZone(sun, z, sunSize, halfZone);

            // Sélection au clic
            sun.MouseLeftButtonDown += (s, e) =>
            {
                SelectSun((Image)s, z);
                e.Handled = true; // important
            };

            SunLayer.Children.Add(sun);
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
        if (_sunIcon == null || _sunSelectedIcon == null)
            return;

        if (_selectedSun == sun)
            return;

        if (_selectedSun != null)
            _selectedSun.Source = _sunIcon;

        _selectedSun = sun;
        _selectedZone = zone;

        _selectedSun.Source = _sunSelectedIcon;
        SelectedZoneText.Text = GetZoneName(zone.Id);
    }

    private void ClearSelection()
    {
        if (_sunIcon != null && _selectedSun != null)
            _selectedSun.Source = _sunIcon;

        _selectedSun = null;
        _selectedZone = null;
        SelectedZoneText.Text = "";
    }

    private string GetZoneName(ushort zoneId)
    {
        if (_zoneNames.TryGetValue(zoneId, out var name))
            return $"{zoneId} - {name}";

        return $"Zone {zoneId}";
    }

    private void MakeSunDraggableUpdatingZone(Image sun, GcaZone zone, double sunSize, double halfZone)
    {
        bool dragging = false;
        Point startMouse = default;
        double startLeft = 0, startTop = 0;

        sun.MouseLeftButtonDown += (s, e) =>
        {
            // On garde la selection si on drag
            SelectSun((Image)s, zone);

            if (_doc != null)
            {
                _dragBeforeSnapshot = _doc.DeepClone();
                _dragMoved = false;
            }

            dragging = true;
            startMouse = e.GetPosition(SunLayer);

            startLeft = Canvas.GetLeft(sun);
            startTop = Canvas.GetTop(sun);
            if (double.IsNaN(startLeft)) startLeft = 0;
            if (double.IsNaN(startTop)) startTop = 0;

            sun.CaptureMouse();
            e.Handled = true;
        };

        sun.MouseMove += (s, e) =>
        {
            if (!dragging) return;

            var p = e.GetPosition(SunLayer);
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

            if (_dragBeforeSnapshot != null && _dragMoved)
                ZoneDragCommitted?.Invoke(this, _dragBeforeSnapshot);

            _dragBeforeSnapshot = null;
            _dragMoved = false;
        };
    }

    private void FitNowStable()
    {
        if (_background == null) return;

        UpdateLayout();
        FitNowInternal();

        UpdateLayout();
        FitNowInternal();
    }

    private void FitNowInternal()
    {
        if (_background == null) return;

        double vw = Math.Max(1, EditorScroll.ViewportWidth);
        double vh = Math.Max(1, EditorScroll.ViewportHeight);

        _minZoom = Math.Min(vw / _background.PixelWidth, vh / _background.PixelHeight);
        _zoom = _minZoom;

        ApplyZoom();

        EditorScroll.ScrollToHorizontalOffset(0);
        EditorScroll.ScrollToVerticalOffset(0);
    }

    private void ApplyZoom()
    {
        ZoomScale.ScaleX = _zoom;
        ZoomScale.ScaleY = _zoom;

        if (Math.Abs(_zoom - _minZoom) < 0.001)
            ZoomText.Text = $"Zoom: Fit ({(int)(_zoom * 100)}%)";
        else
            ZoomText.Text = $"Zoom: {(int)(_zoom * 100)}%";
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (_background == null) return;
        _zoom = Math.Min(8.0, _zoom * 1.25);
        ApplyZoom();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (_background == null) return;
        _zoom = Math.Max(_minZoom, _zoom / 1.25);
        ApplyZoom();
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => FitNowStable();

    private void EditorScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_background == null) return;

        if (Math.Abs(_zoom - _minZoom) < 0.001)
            FitNowStable();
        else
            _zoom = Math.Max(_zoom, _minZoom);
    }

    private const int WM_MOUSEHWHEEL = 0x020E;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEHWHEEL)
        {
            int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            double step = (delta / 120.0) * 140.0;
            EditorScroll.ScrollToHorizontalOffset(EditorScroll.HorizontalOffset - step);
            handled = true;
        }
        return IntPtr.Zero;
    }
}
