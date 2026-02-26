using System;
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

    private double _zoom = 1.0;
    private double _minZoom = 1.0;

    private GcaDocument? _doc;

    private GcaDocument? _dragBeforeSnapshot;
    private bool _dragMoved;

    public event EventHandler<GcaDocument>? ZoneDragCommitted;

    public bool HasBackground => _background != null;

    public GcaViewer()
    {
        InitializeComponent();

        _sunIcon = new BitmapImage(new Uri("pack://application:,,,/Assets/sun.png", UriKind.Absolute));
        _sunIcon.Freeze();

        Loaded += (_, __) =>
        {
            var source = (HwndSource)PresentationSource.FromVisual(this)!;
            source.AddHook(WndProc);
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

        // IMPORTANT: Fit meme si le control est deja loaded
        Dispatcher.BeginInvoke(new Action(() =>
        {
            FitNowStable();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Redimensionne le viewer pour tenir dans l'espace host, puis fait un Fit stable.
    /// </summary>
    public void SizeToHostAndFit(double hostWidth, double hostHeight)
    {
        if (_background == null) return;
        if (hostWidth <= 0 || hostHeight <= 0) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            // On mesure le "chrome" reel: tout sauf le viewport du ScrollViewer
            UpdateLayout();

            double chromeW = Math.Max(0, ActualWidth - EditorScroll.ViewportWidth);
            double chromeH = Math.Max(0, ActualHeight - EditorScroll.ViewportHeight);

            // Taille max possible du viewport dans le host (avec une marge)
            const double outerMargin = 8;
            double maxViewportW = Math.Max(100, hostWidth - outerMargin - chromeW);
            double maxViewportH = Math.Max(100, hostHeight - outerMargin - chromeH);

            double imgW = _background.PixelWidth;
            double imgH = _background.PixelHeight;

            // Scale pour garder le ratio de l'image
            double scale = Math.Min(maxViewportW / imgW, maxViewportH / imgH);
            scale = Math.Min(scale, 1.0); // ne pas agrandir au-dela de 100% par defaut

            double viewportW = Math.Floor(imgW * scale);
            double viewportH = Math.Floor(imgH * scale);

            Width = viewportW + chromeW;
            Height = viewportH + chromeH;

            // Fit stable apres resize
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

    private void MakeSunDraggableUpdatingZone(Image sun, GcaZone zone, double sunSize, double halfZone)
    {
        bool dragging = false;
        Point startMouse = default;
        double startLeft = 0, startTop = 0;

        sun.MouseLeftButtonDown += (s, e) =>
        {
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

        // Pass 1
        UpdateLayout();
        FitNowInternal();

        // Pass 2: le viewport peut changer si WPF affiche/masque une scrollbar
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

        // Si on etait au Fit, on refit automatiquement
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
