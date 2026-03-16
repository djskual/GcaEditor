using GcaEditor.Models;
using GcaEditor.UI.Viewer;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        InitOpacityIcon();

        PlacementHitLayer.MouseLeftButtonDown += PlacementHitLayer_MouseLeftButtonDown;

        MoveHitLayer.MouseLeftButtonDown += MoveHitLayer_MouseLeftButtonDown;
        MoveHitLayer.MouseMove += MoveHitLayer_MouseMove;
        MoveHitLayer.MouseLeftButtonUp += MoveHitLayer_MouseLeftButtonUp;
        MoveHitLayer.LostMouseCapture += MoveHitLayer_LostMouseCapture;

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

        InitOpacityBar();
    }

    private void InitOpacityIcon()
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/Assets/bright.png", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();

        OpacityIcon.OpacityMask = new ImageBrush(image)
        {
            Stretch = Stretch.Uniform
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
        _doc = doc;
        _suns.SetDocument(doc);
        RenderAmbientFromDoc();
    }

    public void SetAmbientDisplayedOpacity(int index, double opacity)
        => _ambient.SetDisplayedOpacity(index, opacity);

    public void SetAllAmbientDisplayedOpacity(double opacity)
        => _ambient.SetAllDisplayedOpacity(opacity);

    public void SetAmbientTintColor(Color tint)
        => _ambient.SetTintColor(tint);

    public void SetAmbientTintEnabledForSlot(int index, bool enabled)
        => _ambient.SetTintEnabled(index, enabled);

    public BitmapSource? GetAmbientSlotBitmap(int index)
        => _ambient.GetSlotBitmap(index);
}
