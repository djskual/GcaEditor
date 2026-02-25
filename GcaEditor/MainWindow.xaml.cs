using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

using GcaEditor.IO;
using GcaEditor.Models;

namespace GcaEditor;

public partial class MainWindow : Window
{
    private BitmapSource? _background;
    private BitmapImage? _sunIcon;
    private double _zoom = 1.0;
    private double _minZoom = 1.0;
    private GcaEditor.IO.GcaCodec.ParsedGca? _gcaParsed;
    private readonly Dictionary<int, GcaEditor.Models.GcaZone> _zonesById = new();
    private string? _gcaPath;
    private readonly Stack<GcaCodec.ParsedGca> _undo = new();
    private readonly Stack<GcaCodec.ParsedGca> _redo = new();
    private GcaCodec.ParsedGca? _dragBeforeSnapshot;
    private bool _dragMoved;

    private bool CanUndo => _undo.Count > 0;
    private bool CanRedo => _redo.Count > 0;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, __) =>
        {
            var source = (HwndSource)PresentationSource.FromVisual(this)!;
            source.AddHook(WndProc);
        };

        this.PreviewKeyDown += MainWindow_PreviewKeyDown;

        _sunIcon = new BitmapImage(new Uri("pack://application:,,,/Assets/sun.png", UriKind.Absolute));
        _sunIcon.Freeze(); // important: perf + réutilisation sûre

    }

    private const int WM_MOUSEHWHEEL = 0x020E; // horizontal wheel

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

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == 0)
            return;

        if (e.Key == System.Windows.Input.Key.Z)
        {
            // Ctrl+Z = undo (Ctrl+Shift+Z aussi)
            DoUndo();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Y)
        {
            // Ctrl+Y = redo
            DoRedo();
            e.Handled = true;
        }
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
    
    private void OpenGca_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "GCA (*.gca)|*.gca|All files|*.*",
            Title = "Ouvrir un fichier GCA"
        };
        if (ofd.ShowDialog() != true) return;

        _gcaPath = ofd.FileName;
        _gcaParsed = GcaCodec.Load(_gcaPath);

        _zonesById.Clear();
        foreach (var z in _gcaParsed.Zones)
            _zonesById[z.Id] = z;

        _undo.Clear();
        _redo.Clear();

        RenderZones(_gcaParsed.Zones);
    }

    private void RenderZones(List<GcaEditor.Models.GcaZone> zones)
    {
        if (_sunIcon == null) return;

        Scene.Children.Clear();

        const double sunSize = 44;       // taille d’affichage du PNG (visuel)
        const double halfZone = 52.0;    // 104/2

        foreach (var z in zones)
        {
            var sun = new System.Windows.Controls.Image
            {
                Source = _sunIcon,
                Width = sunSize,
                Height = sunSize,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Tag = (int)z.Id,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // place le soleil au centre de la zone
            SetSunPositionFromZone(sun, z, sunSize);

            MakeSunDraggableUpdatingZone(sun, z, sunSize, halfZone);

            Scene.Children.Add(sun);
        }
    }

    private void SaveGca_Click(object sender, RoutedEventArgs e)
    {
        if (_gcaParsed == null)
        {
            MessageBox.Show("Aucun GCA chargé.");
            return;
        }

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "GCA (*.gca)|*.gca",
            Title = "Enregistrer le GCA",
            FileName = _gcaPath != null ? System.IO.Path.GetFileName(_gcaPath) : "menu.gca"
        };

        if (sfd.ShowDialog() != true) return;

        GcaCodec.Save(sfd.FileName, _gcaParsed);
        MessageBox.Show("GCA sauvegardé.");
    }

    private static GcaCodec.ParsedGca CloneGca(GcaCodec.ParsedGca src)
    {
        var dst = new GcaCodec.ParsedGca
        {
            HeaderUnk0 = src.HeaderUnk0
        };

        foreach (var z in src.Zones)
        {
            dst.Zones.Add(new GcaEditor.Models.GcaZone
            {
                Id = z.Id,
                A = z.A,
                B = z.B,
                C = z.C,
                X1 = z.X1,
                Y1 = z.Y1,
                X2 = z.X2,
                Y2 = z.Y2,
                X3 = z.X3,
                Y3 = z.Y3,
                X4 = z.X4,
                Y4 = z.Y4
            });
        }

        foreach (var img in src.Images)
        {
            dst.Images.Add(new GcaEditor.Models.GcaImageRef
            {
                Id = img.Id,
                X = img.X,
                Y = img.Y
            });
        }

        return dst;
    }

    private void MakeSunDraggableUpdatingZone(
    System.Windows.Controls.Image sun,
    GcaEditor.Models.GcaZone zone,
    double sunSize,
    double halfZone)
    {
        bool dragging = false;
        System.Windows.Point startMouse = default;
        double startLeft = 0, startTop = 0;

        sun.MouseLeftButtonDown += (s, e) =>
        {
            if (_gcaParsed != null)
            {
                _dragBeforeSnapshot = CloneGca(_gcaParsed);
                _dragMoved = false;
            }
            
            dragging = true;
            startMouse = e.GetPosition(Scene);

            startLeft = System.Windows.Controls.Canvas.GetLeft(sun);
            startTop = System.Windows.Controls.Canvas.GetTop(sun);
            if (double.IsNaN(startLeft)) startLeft = 0;
            if (double.IsNaN(startTop)) startTop = 0;

            sun.CaptureMouse();
            e.Handled = true;
        };

        sun.MouseMove += (s, e) =>
        {
            if (!dragging) return;

            var p = e.GetPosition(Scene);
            var dx = p.X - startMouse.X;
            var dy = p.Y - startMouse.Y;

            if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
                _dragMoved = true;

            double newLeft = startLeft + dx;
            double newTop = startTop + dy;

            // bouge le soleil visuellement
            System.Windows.Controls.Canvas.SetLeft(sun, newLeft);
            System.Windows.Controls.Canvas.SetTop(sun, newTop);

            // calcule le nouveau centre de zone à partir du centre du soleil
            double cx = newLeft + sunSize / 2.0;
            double cy = newTop + sunSize / 2.0;

            // recalc les 4 points (zone 104x104)
            ushort x1 = (ushort)Math.Round(cx - halfZone);
            ushort y1 = (ushort)Math.Round(cy - halfZone);
            ushort x2 = (ushort)Math.Round(cx + halfZone);
            ushort y2 = (ushort)Math.Round(cy - halfZone);
            ushort x3 = (ushort)Math.Round(cx + halfZone);
            ushort y3 = (ushort)Math.Round(cy + halfZone);
            ushort x4 = (ushort)Math.Round(cx - halfZone);
            ushort y4 = (ushort)Math.Round(cy + halfZone);

            zone.X1 = x1; zone.Y1 = y1;
            zone.X2 = x2; zone.Y2 = y2;
            zone.X3 = x3; zone.Y3 = y3;
            zone.X4 = x4; zone.Y4 = y4;

            e.Handled = true;
        };

        sun.MouseLeftButtonUp += (s, e) =>
        {
            if (!dragging) return;
            dragging = false;
            sun.ReleaseMouseCapture();
            e.Handled = true;

            if (_gcaParsed != null && _dragBeforeSnapshot != null && _dragMoved)
            {
                _undo.Push(_dragBeforeSnapshot);
                _redo.Clear(); // une nouvelle action casse la redo chain
            }

            _dragBeforeSnapshot = null;
            _dragMoved = false;

            // Debug utile
            System.Diagnostics.Debug.WriteLine($"Zone {zone.Id} => ({zone.X1},{zone.Y1})..({zone.X3},{zone.Y3})");
        };
    }

    private static void SetSunPositionFromZone(System.Windows.Controls.Image sun, GcaEditor.Models.GcaZone z, double sunSize)
    {
        double left = z.CenterX - sunSize / 2.0;
        double top = z.CenterY - sunSize / 2.0;
        System.Windows.Controls.Canvas.SetLeft(sun, left);
        System.Windows.Controls.Canvas.SetTop(sun, top);
    }

    private void DoUndo()
    {
        if (_gcaParsed == null) return;
        if (_undo.Count == 0) return;

        _redo.Push(CloneGca(_gcaParsed));
        _gcaParsed = _undo.Pop();
        RenderZones(_gcaParsed.Zones);
    }

    private void DoRedo()
    {
        if (_gcaParsed == null) return;
        if (_redo.Count == 0) return;

        _undo.Push(CloneGca(_gcaParsed));
        _gcaParsed = _redo.Pop();
        RenderZones(_gcaParsed.Zones);
    }
}
