using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GcaEditor.UI.Viewer;

public sealed class ViewerContext
{
    public ViewerContext(
        ScrollViewer scroll,
        Image backgroundImage,
        Canvas ambientLayer,
        Canvas sunLayer,
        ScaleTransform zoomScale,
        TextBlock zoomText,
        TextBlock selectedZoneText,
        FrameworkElement zoomRoot)
    {
        Scroll = scroll;
        BackgroundImage = backgroundImage;
        AmbientLayer = ambientLayer;
        SunLayer = sunLayer;
        ZoomScale = zoomScale;
        ZoomText = zoomText;
        SelectedZoneText = selectedZoneText;
        ZoomRoot = zoomRoot;
    }

    public ScrollViewer Scroll { get; }
    public Image BackgroundImage { get; }
    public Canvas AmbientLayer { get; }
    public Canvas SunLayer { get; }
    public ScaleTransform ZoomScale { get; }
    public TextBlock ZoomText { get; }
    public TextBlock SelectedZoneText { get; }
    public FrameworkElement ZoomRoot { get; }

    public BitmapSource? Background { get; set; }
}
