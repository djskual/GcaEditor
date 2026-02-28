using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GcaEditor.Models;

namespace GcaEditor.UI.Controls;

public partial class GcaViewer
{
    public BitmapSource LoadAndConvertAmbientMask(string path) => _ambient.LoadAndConvertMask(path);

    public void SetAmbientSlot(int index, BitmapSource bitmap)
    {
        _ambient.SetSlot(index, bitmap);
        RenderAmbientFromDoc();
    }

    public void ClearAmbientSlot(int index)
    {
        _ambient.ClearSlot(index);
        RenderAmbientFromDoc();
    }

    public void ClearAllAmbient() => _ambient.ClearAll();

    public bool HasAmbientSlot(int index) => _ambient.HasSlot(index);

    public bool IsAmbientIdPositionedInDoc(int index)
        => _doc?.Images.Any(i => i.Id == index) == true;

    public void SetAmbientPlacementMode(int index)
    {
        _ambientPlacementIndex = index;
        Cursor = Cursors.Cross;

        SunLayer.IsHitTestVisible = false;
        PlacementHitLayer.Visibility = Visibility.Visible;
    }

    public void ClearAmbientPlacementMode()
    {
        _ambientPlacementIndex = null;
        Cursor = null;

        SunLayer.IsHitTestVisible = true;
        PlacementHitLayer.Visibility = Visibility.Collapsed;
    }

    private void PlacementHitLayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_ambientPlacementIndex == null)
            return;

        var posViewport = e.GetPosition(EditorScroll);

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

        AmbientPlaceRequested?.Invoke(this, new Point(xImg, yImg));
        e.Handled = true;
    }

    private void RenderAmbientFromDoc()
    {
        if (_doc == null)
        {
            _ambient.RenderAtPositions(Array.Empty<(int index, int x, int y)>());
            return;
        }

        var positions = _doc.Images.Select(i => ((int)i.Id, (int)i.X, (int)i.Y));
        _ambient.RenderAtPositions(positions);
    }
}
