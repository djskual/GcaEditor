using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GcaEditor.UI.Controls;

public partial class GcaViewer
{
    public event EventHandler<double>? OpacityBarValueChanged;
    public event EventHandler<bool>? OpacityAllZonesChanged;

    private bool _opacityEnabled;
    private bool _opacityDrag;
    private double _opacityValue01 = 1.0;

    private bool _opacityAllZones;
    private bool _suppressAllZonesEvent;

    private void InitOpacityBar()
    {
        if (OpacityBar == null) return;

        OpacityBar.Visibility = Visibility.Visible;

        if (AllZonesButton != null)
        {
            AllZonesButton.IsChecked = false;
            AllZonesButton.Checked += AllZonesButton_Changed;
            AllZonesButton.Unchecked += AllZonesButton_Changed;
        }

        SetOpacityBarEnabled(false);
        SetOpacityBarValue(1.0);

        if (OpacityTrackBorder != null)
        {
            OpacityTrackBorder.PreviewMouseLeftButtonDown += OpacityBar_MouseDown;
            OpacityTrackBorder.PreviewMouseMove += OpacityBar_MouseMove;
            OpacityTrackBorder.PreviewMouseLeftButtonUp += OpacityBar_MouseUp;
            OpacityTrackBorder.LostMouseCapture += (_, __) => _opacityDrag = false;
        }

        if (OpacityTrackCanvas != null)
            OpacityTrackCanvas.SizeChanged += (_, __) => UpdateOpacityThumb();

        if (OpacityThumb != null)
            OpacityThumb.SizeChanged += (_, __) => UpdateOpacityThumb();
    }

    private void AllZonesButton_Changed(object? sender, RoutedEventArgs e)
    {
        if (_suppressAllZonesEvent) return;
        _opacityAllZones = AllZonesButton?.IsChecked == true;
        OpacityAllZonesChanged?.Invoke(this, _opacityAllZones);
    }

    public void SetOpacityAllZonesChecked(bool isChecked)
    {
        if (AllZonesButton == null) return;
        try
        {
            _suppressAllZonesEvent = true;
            AllZonesButton.IsChecked = isChecked;
            _opacityAllZones = isChecked;
        }
        finally
        {
            _suppressAllZonesEvent = false;
        }
    }

    public bool GetOpacityAllZonesEnabled()
    {
        return _opacityAllZones;
    }

    public void SetOpacityBarEnabled(bool enabled)
    {
        _opacityEnabled = enabled;

        if (OpacityTrackBorder != null)
        {
            OpacityTrackBorder.Opacity = enabled ? 1.0 : 0.55;
            OpacityTrackBorder.IsHitTestVisible = enabled;
        }

        if (!enabled)
            SetOpacityBarValue(1.0);
    }

    public void SetOpacityBarValue(double value01)
    {
        if (value01 < 0) value01 = 0;
        if (value01 > 1) value01 = 1;

        _opacityValue01 = value01;
        UpdateOpacityThumb();
    }

    private void OpacityBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_opacityEnabled) return;
        if (OpacityTrackCanvas == null) return;

        _opacityDrag = true;
        OpacityTrackBorder?.CaptureMouse();

        SetOpacityFromMouse(e);
        e.Handled = true;
    }

    private void OpacityBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_opacityEnabled) return;
        if (!_opacityDrag) return;

        SetOpacityFromMouse(e);
        e.Handled = true;
    }

    private void OpacityBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_opacityDrag) return;

        _opacityDrag = false;
        OpacityTrackBorder?.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void SetOpacityFromMouse(MouseEventArgs e)
    {
        if (OpacityTrackCanvas == null || OpacityThumb == null) return;

        double trackW = OpacityTrackCanvas.ActualWidth;
        double thumbW = OpacityThumb.ActualWidth;
        if (trackW <= 1 || thumbW <= 1) return;

        double maxX = trackW - thumbW;
        if (maxX < 0) maxX = 0;

        double x = e.GetPosition(OpacityTrackCanvas).X - (thumbW / 2.0);

        if (x < 0) x = 0;
        if (x > maxX) x = maxX;

        double v = maxX <= 0 ? 0 : (x / maxX);
        if (v < 0) v = 0;
        if (v > 1) v = 1;

        _opacityValue01 = v;
        UpdateOpacityThumb();
        OpacityBarValueChanged?.Invoke(this, _opacityValue01);
    }

    private void UpdateOpacityThumb()
    {
        if (OpacityTrackCanvas == null || OpacityThumb == null) return;

        double trackW = OpacityTrackCanvas.ActualWidth;
        double thumbW = OpacityThumb.ActualWidth;
        if (trackW <= 1 || thumbW <= 1) return;

        double maxX = trackW - thumbW;
        if (maxX < 0) maxX = 0;

        double x = _opacityValue01 * maxX;

        Canvas.SetLeft(OpacityThumb, x);
        Canvas.SetTop(OpacityThumb, 0);
    }
}