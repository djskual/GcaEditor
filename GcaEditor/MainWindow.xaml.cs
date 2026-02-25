using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GcaEditor;

public partial class MainWindow : Window
{
    private BitmapSource? _background;
    private double _zoom = 1.0;
    private double _minZoom = 1.0;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ImportBackground_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            Title = "Fond menu lumières (1280x556)"
        };
        if (ofd.ShowDialog() != true) return;

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new Uri(ofd.FileName);
        bi.EndInit();
        bi.Freeze();

        _background = bi;
        BackgroundImage.Source = bi;

        BackgroundImage.Width = bi.PixelWidth;
        BackgroundImage.Height = bi.PixelHeight;
        Scene.Width = bi.PixelWidth;
        Scene.Height = bi.PixelHeight;

        Dispatcher.BeginInvoke(new Action(FitNow),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void FitNow()
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

        ZoomRoot.UpdateLayout();
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

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        FitNow();
    }

    private void EditorScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_background == null) return;

        if (Math.Abs(_zoom - _minZoom) < 0.001)
            FitNow();
        else
            _zoom = Math.Max(_zoom, _minZoom);
    }
}
