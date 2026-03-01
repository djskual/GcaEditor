using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GcaEditor.Models;

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
    private bool _ambientMoveDragging;
    private Point _ambientMoveStartMouseImg;
    private ushort _ambientMoveStartX;
    private ushort _ambientMoveStartY;

    public void SetAmbientMoveMode(int index)
    {
        _ambientMoveIndex = index;
        _ambientMoveDragging = false;
        Cursor = Cursors.SizeAll;

        SunLayer.IsHitTestVisible = false;
        MoveHitLayer.Visibility = Visibility.Visible;
    }

    public void ClearAmbientMoveMode()
    {
        _ambientMoveIndex = null;
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

        // Only allow move if we have a doc entry and a loaded bitmap
        if (!_ambient.HasSlot(idx)) return;

        var entry = _doc.Images.FirstOrDefault(x => x.Id == (ushort)idx);
        if (entry == null) return;

        // Mouse start in image coords
        _ambientMoveStartMouseImg = ViewportToImage(e.GetPosition(EditorScroll));
        _ambientMoveStartX = entry.X;
        _ambientMoveStartY = entry.Y;

        _ambientMoveDragging = true;

        MoveHitLayer.CaptureMouse();
        e.Handled = true;
    }

    private void MoveHitLayer_MouseMove(object sender, MouseEventArgs e)
    {
        if (_ambientMoveIndex == null) return;
        if (!_ambientMoveDragging) return;

        int idx = _ambientMoveIndex.Value;

        var nowImg = ViewportToImage(e.GetPosition(EditorScroll));
        double dx = nowImg.X - _ambientMoveStartMouseImg.X;
        double dy = nowImg.Y - _ambientMoveStartMouseImg.Y;

        double newX = _ambientMoveStartX + dx;
        double newY = _ambientMoveStartY + dy;

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
        if (!_ambientMoveDragging) return;

        FinishMove(e.GetPosition(EditorScroll));
        e.Handled = true;
    }

    private void MoveHitLayer_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_ambientMoveIndex == null) return;
        if (!_ambientMoveDragging) return;

        CancelMove();
    }

    private void FinishMove(Point viewportPos)
    {
        if (_ambientMoveIndex == null) return;
        if (_doc == null) return;

        int idx = _ambientMoveIndex.Value;

        if (MoveHitLayer.IsMouseCaptured)
            MoveHitLayer.ReleaseMouseCapture();

        _ambientMoveDragging = false;

        var nowImg = ViewportToImage(viewportPos);
        double dx = nowImg.X - _ambientMoveStartMouseImg.X;
        double dy = nowImg.Y - _ambientMoveStartMouseImg.Y;

        ushort newX = (ushort)Math.Round(_ambientMoveStartX + dx);
        ushort newY = (ushort)Math.Round(_ambientMoveStartY + dy);

        // Re-render from doc will happen after commit. Emit event.
        AmbientMoveCommitted?.Invoke(this,
            new AmbientMoveCommittedEventArgs(idx, _ambientMoveStartX, _ambientMoveStartY, newX, newY));
    }

    public void CancelMove()
    {
        if (_ambientMoveIndex == null) return;

        int idx = _ambientMoveIndex.Value;

        if (MoveHitLayer.IsMouseCaptured)
            MoveHitLayer.ReleaseMouseCapture();

        _ambientMoveDragging = false;

        // Restore visual to doc position
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
