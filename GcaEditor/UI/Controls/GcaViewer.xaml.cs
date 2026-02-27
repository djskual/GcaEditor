using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using GcaEditor.Models;
using GcaEditor.UI.Viewer;

namespace GcaEditor.UI.Controls;

public partial class GcaViewer : UserControl
{
    private readonly ViewerContext _ctx;
    private readonly ZoomController _zoom;
    private readonly SunOverlayManager _suns;

    public event EventHandler<GcaDocument>? ZoneDragCommitted;

    // ✅ NEW: selection forwarding
    public event EventHandler<ushort?>? SelectedZoneChanged;

    public ushort? SelectedZoneId => _suns.SelectedZoneId;

    public bool HasBackground => _ctx.Background != null;

    public GcaViewer()
    {
        InitializeComponent();

        _ctx = new ViewerContext(
            EditorScroll,
            BackgroundImage,
            AmbientLayer,
            SunLayer,
            ZoomScale,
            ZoomText,
            SelectedZoneText,
            ZoomRoot
        );

        _zoom = new ZoomController(_ctx);

        _suns = new SunOverlayManager(_ctx);
        _suns.ZoneDragCommitted += (_, snap) => ZoneDragCommitted?.Invoke(this, snap);
        _suns.SelectedZoneChanged += (_, id) => SelectedZoneChanged?.Invoke(this, id);

        Loaded += (_, __) =>
        {
            var source = (HwndSource)PresentationSource.FromVisual(this)!;
            source.AddHook(WndProc);
        };
    }

    public void SetZoneNames(System.Collections.Generic.IReadOnlyDictionary<ushort, string> names)
        => _suns.SetZoneNames(names);

    public void SetBackground(BitmapSource? bg)
    {
        _ctx.Background = bg;
        BackgroundImage.Source = bg;

        if (bg == null) return;

        BackgroundImage.Width = bg.PixelWidth;
        BackgroundImage.Height = bg.PixelHeight;

        AmbientLayer.Width = bg.PixelWidth;
        AmbientLayer.Height = bg.PixelHeight;
        SunLayer.Width = bg.PixelWidth;
        SunLayer.Height = bg.PixelHeight;

        Dispatcher.BeginInvoke(new Action(() => _zoom.FitStable()),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void LoadDocument(GcaDocument? doc)
    {
        _suns.SetDocument(doc);
    }

    public bool SelectZoneById(ushort id) => _suns.SelectZoneById(id);

    public void ClearSelection() => _suns.ClearSelectionPublic();

    /// <summary>
    /// Retourne le centre du viewport en coordonnées image (pixels 0..1280/556),
    /// en tenant compte du zoom + offsets scroll.
    /// </summary>
    public Point GetViewportCenterInImageCoords()
    {
        if (_ctx.Background == null)
            return new Point(640, 278);

        double zoom = ZoomScale.ScaleX;
        if (zoom <= 0.0001) zoom = 1.0;

        double cxView = EditorScroll.HorizontalOffset + (EditorScroll.ViewportWidth / 2.0);
        double cyView = EditorScroll.VerticalOffset + (EditorScroll.ViewportHeight / 2.0);

        double cxImg = cxView / zoom;
        double cyImg = cyView / zoom;

        // clamp dans l'image
        cxImg = Math.Max(0, Math.Min(_ctx.Background.PixelWidth, cxImg));
        cyImg = Math.Max(0, Math.Min(_ctx.Background.PixelHeight, cyImg));

        return new Point(cxImg, cyImg);
    }

    public void SizeToHostAndFit(double hostWidth, double hostHeight)
    {
        if (_ctx.Background == null) return;
        if (hostWidth <= 0 || hostHeight <= 0) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateLayout();

            double chromeW = Math.Max(0, ActualWidth - EditorScroll.ViewportWidth);
            double chromeH = Math.Max(0, ActualHeight - EditorScroll.ViewportHeight);

            const double outerMargin = 8;
            double maxViewportW = Math.Max(100, hostWidth - outerMargin - chromeW);
            double maxViewportH = Math.Max(100, hostHeight - outerMargin - chromeH);

            double imgW = _ctx.Background.PixelWidth;
            double imgH = _ctx.Background.PixelHeight;

            double scale = Math.Min(maxViewportW / imgW, maxViewportH / imgH);
            scale = Math.Min(scale, 1.0);

            double viewportW = Math.Floor(imgW * scale);
            double viewportH = Math.Floor(imgH * scale);

            Width = viewportW + chromeW;
            Height = viewportH + chromeH;

            Dispatcher.BeginInvoke(new Action(() => _zoom.FitStable()),
                System.Windows.Threading.DispatcherPriority.Loaded);

        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => _zoom.FitStable();
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => _zoom.ZoomIn();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => _zoom.ZoomOut();

    private void EditorScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _zoom.OnViewportChanged();
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
