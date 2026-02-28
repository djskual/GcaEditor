using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private readonly AmbientImageOverlay _ambient;

    private int? _ambientPlacementIndex = null;
    private GcaDocument? _doc;

    public event EventHandler<GcaDocument>? ZoneDragCommitted;
    public event EventHandler<ushort?>? SelectedZoneChanged;
    public event EventHandler<Point>? AmbientPlaceRequested;

    public ushort? SelectedZoneId => _suns.SelectedZoneId;
    public bool HasBackground => _ctx.Background != null;

    public GcaViewer()
    {
        InitializeComponent();

        PlacementHitLayer.MouseLeftButtonDown += PlacementHitLayer_MouseLeftButtonDown;

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

        _ambient = new AmbientImageOverlay(_ctx);

        Loaded += (_, __) => AttachHwndHook();
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
        _doc = doc;
        _suns.SetDocument(doc);
        RenderAmbientFromDoc();
    }
}
