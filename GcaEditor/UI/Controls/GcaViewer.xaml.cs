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

        Loaded += (_, __) =>
        {
            var source = (HwndSource)PresentationSource.FromVisual(this)!;
            source.AddHook(WndProc);
        };
    }

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
