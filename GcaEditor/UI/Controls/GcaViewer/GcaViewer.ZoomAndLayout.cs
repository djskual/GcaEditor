using System.Windows;

namespace GcaEditor.UI.Controls;

public partial class GcaViewer
{
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
}
