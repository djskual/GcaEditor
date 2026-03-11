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
        _zoom = Math.Min(8.0, _zoom * 1.25);
        ApplyZoom();
    }

    public void ZoomOut()
    {
        if (_ctx.Background == null) return;
        _zoom = Math.Max(_minZoom, _zoom / 1.25);
        ApplyZoom();
    }

    public void OnViewportChanged()
    {
        if (_ctx.Background == null) return;

        if (Math.Abs(_zoom - _minZoom) < 0.001)
            FitStable();
        else
            _zoom = Math.Max(_zoom, _minZoom);
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
