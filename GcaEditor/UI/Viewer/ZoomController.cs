namespace GcaEditor.UI.Viewer;

public sealed class ZoomController
{
    private readonly ViewerContext _ctx;

    private double _zoom = 1.0;
    private double _minZoom = 1.0;

    public ZoomController(ViewerContext ctx) => _ctx = ctx;

    public void FitStable()
    {
        if (_ctx.Background == null) return;

        // 2 passes pour stabiliser le viewport (scrollbars Auto)
        FitInternal();
        FitInternal();
    }

    private void FitInternal()
    {
        var bg = _ctx.Background!;
        _ctx.Scroll.UpdateLayout();

        double vw = Math.Max(1, _ctx.Scroll.ViewportWidth);
        double vh = Math.Max(1, _ctx.Scroll.ViewportHeight);

        _minZoom = Math.Min(vw / bg.PixelWidth, vh / bg.PixelHeight);
        _zoom = _minZoom;

        ApplyZoom();

        _ctx.Scroll.ScrollToHorizontalOffset(0);
        _ctx.Scroll.ScrollToVerticalOffset(0);
    }

    public void ZoomIn()
    {
        if (_ctx.Background == null) return;
        ZoomAroundViewportCenter(Math.Min(8.0, _zoom * 1.25));
    }

    public void ZoomOut()
    {
        if (_ctx.Background == null) return;
        ZoomAroundViewportCenter(Math.Max(_minZoom, _zoom / 1.25));
    }

    public void OnViewportChanged()
    {
        if (_ctx.Background == null) return;

        if (Math.Abs(_zoom - _minZoom) < 0.001)
            FitStable();
        else
            _zoom = Math.Max(_zoom, _minZoom);
    }

    private void ZoomAroundViewportCenter(double newZoom)
    {
        var bg = _ctx.Background;
        if (bg == null)
            return;

        _ctx.Scroll.UpdateLayout();

        double oldZoom = _zoom <= 0.0001 ? 1.0 : _zoom;

        double viewportWidth = Math.Max(1, _ctx.Scroll.ViewportWidth);
        double viewportHeight = Math.Max(1, _ctx.Scroll.ViewportHeight);

        double centerXInImage = (_ctx.Scroll.HorizontalOffset + viewportWidth / 2.0) / oldZoom;
        double centerYInImage = (_ctx.Scroll.VerticalOffset + viewportHeight / 2.0) / oldZoom;

        centerXInImage = Math.Max(0, Math.Min(bg.PixelWidth, centerXInImage));
        centerYInImage = Math.Max(0, Math.Min(bg.PixelHeight, centerYInImage));

        _zoom = newZoom;
        ApplyZoom();

        _ctx.Scroll.UpdateLayout();

        double targetHorizontalOffset = (centerXInImage * _zoom) - (viewportWidth / 2.0);
        double targetVerticalOffset = (centerYInImage * _zoom) - (viewportHeight / 2.0);

        double maxHorizontalOffset = Math.Max(0, _ctx.Scroll.ExtentWidth - viewportWidth);
        double maxVerticalOffset = Math.Max(0, _ctx.Scroll.ExtentHeight - viewportHeight);

        targetHorizontalOffset = Math.Max(0, Math.Min(maxHorizontalOffset, targetHorizontalOffset));
        targetVerticalOffset = Math.Max(0, Math.Min(maxVerticalOffset, targetVerticalOffset));

        _ctx.Scroll.ScrollToHorizontalOffset(targetHorizontalOffset);
        _ctx.Scroll.ScrollToVerticalOffset(targetVerticalOffset);
    }

    private void ApplyZoom()
    {
        _ctx.ZoomScale.ScaleX = _zoom;
        _ctx.ZoomScale.ScaleY = _zoom;

        if (Math.Abs(_zoom - _minZoom) < 0.001)
            _ctx.ZoomText.Text = $"Zoom: Fit ({(int)(_zoom * 100)}%)";
        else
            _ctx.ZoomText.Text = $"Zoom: {(int)(_zoom * 100)}%";
    }
}
