using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GcaEditor.UI.Controls;

public partial class GcaViewer
{
    public sealed class AmbientMoveCommittedEventArgs : EventArgs
    {
        public int Id { get; }
        public ushort OldX { get; }
        public ushort OldY { get; }
        public ushort NewX { get; }
        public ushort NewY { get; }

        public AmbientMoveCommittedEventArgs(int id, ushort oldX, ushort oldY, ushort newX, ushort newY)
        {
            Id = id;
            OldX = oldX;
            OldY = oldY;
            NewX = newX;
            NewY = newY;
        }
    }

    public event EventHandler<AmbientMoveCommittedEventArgs>? AmbientMoveCommitted;

    private int? _ambientMoveIndex;
    private bool _ambientMovePending;
    private bool _ambientMoveDragging;
    private Point _ambientMoveStartMouseImg;
    private Point _ambientMoveStartMouseViewport;
    private ushort _ambientMoveStartX;
    private ushort _ambientMoveStartY;

    private const double AmbientMoveStartThresholdPx = 4.0;

    public void SetAmbientMoveMode(int index)
    {
        _ambientMoveIndex = index;
        _ambientMovePending = false;
        _ambientMoveDragging = false;
        Cursor = Cursors.SizeAll;

        SunLayer.IsHitTestVisible = false;
        MoveHitLayer.Visibility = Visibility.Visible;
    }

    public void ClearAmbientMoveMode()
    {
        _ambientMoveIndex = null;
        _ambientMovePending = false;
        _ambientMoveDragging = false;
        Cursor = null;

        SunLayer.IsHitTestVisible = true;
        MoveHitLayer.Visibility = Visibility.Collapsed;
    }

    private void MoveHitLayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_ambientMoveIndex == null) return;
        if (_doc == null) return;

        int idx = _ambientMoveIndex.Value;

        if (!_ambient.HasSlot(idx)) return;

        var entry = _doc.Images.FirstOrDefault(x => x.Id == (ushort)idx);
        if (entry == null) return;

        _ambientMoveStartMouseViewport = e.GetPosition(EditorScroll);
        _ambientMoveStartMouseImg = ViewportToImage(_ambientMoveStartMouseViewport);
        _ambientMoveStartX = entry.X;
        _ambientMoveStartY = entry.Y;

        _ambientMovePending = true;
        _ambientMoveDragging = false;

        MoveHitLayer.CaptureMouse();
        e.Handled = true;
    }

    private void MoveHitLayer_MouseMove(object sender, MouseEventArgs e)
    {
        if (_ambientMoveIndex == null) return;
        if (!_ambientMovePending && !_ambientMoveDragging) return;

        int idx = _ambientMoveIndex.Value;
        var currentViewport = e.GetPosition(EditorScroll);

        if (!_ambientMoveDragging)
        {
            double deltaViewportX = currentViewport.X - _ambientMoveStartMouseViewport.X;
            double deltaViewportY = currentViewport.Y - _ambientMoveStartMouseViewport.Y;

            if ((deltaViewportX * deltaViewportX) + (deltaViewportY * deltaViewportY) <
                AmbientMoveStartThresholdPx * AmbientMoveStartThresholdPx)
            {
                return;
            }

            _ambientMoveDragging = true;
            _ambientMovePending = false;
        }

        var nowImg = ViewportToImage(currentViewport);
        double dx = nowImg.X - _ambientMoveStartMouseImg.X;
        double dy = nowImg.Y - _ambientMoveStartMouseImg.Y;

        double newX = _ambientMoveStartX + dx;
        double newY = _ambientMoveStartY + dy;

        var clamped = ClampAmbientTopLeft(idx, newX, newY);
        newX = clamped.X;
        newY = clamped.Y;

        if (_ambient.TryGetDisplayedImage(idx, out var imgEl))
        {
            Canvas.SetLeft(imgEl, newX);
            Canvas.SetTop(imgEl, newY);
        }

        e.Handled = true;
    }

    private void MoveHitLayer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_ambientMoveIndex == null) return;
        if (!_ambientMovePending && !_ambientMoveDragging) return;

        if (_ambientMovePending)
        {
            CancelMove();
            e.Handled = true;
            return;
        }

        FinishMove(e.GetPosition(EditorScroll));
        e.Handled = true;
    }

    private void MoveHitLayer_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_ambientMoveIndex == null) return;
        if (!_ambientMovePending && !_ambientMoveDragging) return;

        CancelMove();
    }

    private void FinishMove(Point viewportPos)
    {
        if (_ambientMoveIndex == null) return;
        if (_doc == null) return;

        int idx = _ambientMoveIndex.Value;

        if (MoveHitLayer.IsMouseCaptured)
            MoveHitLayer.ReleaseMouseCapture();

        _ambientMovePending = false;
        _ambientMoveDragging = false;

        var nowImg = ViewportToImage(viewportPos);
        double dx = nowImg.X - _ambientMoveStartMouseImg.X;
        double dy = nowImg.Y - _ambientMoveStartMouseImg.Y;

        double fx = _ambientMoveStartX + dx;
        double fy = _ambientMoveStartY + dy;

        var clamped = ClampAmbientTopLeft(idx, fx, fy);
        fx = clamped.X;
        fy = clamped.Y;

        ushort newX = (ushort)Math.Round(fx);
        ushort newY = (ushort)Math.Round(fy);

        if (newX == _ambientMoveStartX && newY == _ambientMoveStartY)
            return;

        AmbientMoveCommitted?.Invoke(this,
            new AmbientMoveCommittedEventArgs(idx, _ambientMoveStartX, _ambientMoveStartY, newX, newY));
    }

    public void CancelMove()
    {
        if (_ambientMoveIndex == null) return;

        int idx = _ambientMoveIndex.Value;

        if (MoveHitLayer.IsMouseCaptured)
            MoveHitLayer.ReleaseMouseCapture();

        _ambientMovePending = false;
        _ambientMoveDragging = false;

        if (_doc != null)
        {
            var entry = _doc.Images.FirstOrDefault(x => x.Id == (ushort)idx);
            if (entry != null && _ambient.TryGetDisplayedImage(idx, out var imgEl))
            {
                Canvas.SetLeft(imgEl, entry.X);
                Canvas.SetTop(imgEl, entry.Y);
            }
        }
    }

    private Point ViewportToImage(Point posViewport)
    {
        double zoom = ZoomScale.ScaleX;
        if (zoom <= 0.0001) zoom = 1.0;

        double xView = posViewport.X + EditorScroll.HorizontalOffset;
        double yView = posViewport.Y + EditorScroll.VerticalOffset;

        double xImg = xView / zoom;
        double yImg = yView / zoom;

        if (_ctx.Background != null)
        {
            xImg = Math.Max(0, Math.Min(_ctx.Background.PixelWidth, xImg));
            yImg = Math.Max(0, Math.Min(_ctx.Background.PixelHeight, yImg));
        }

        return new Point(xImg, yImg);
    }
}
