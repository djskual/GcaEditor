using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GcaEditor.Imaging;

namespace GcaEditor.UI.Viewer;

/// <summary>
/// Manages ambient lighting images displayed on the AmbientLayer.
/// Images are expected to be black background with white shapes. We convert luminance to alpha
/// so only the "lit" parts are visible (similar to MIB rendering).
/// </summary>
public sealed class AmbientImageOverlay
{
    private readonly ViewerContext _ctx;

    // slot index (0..22) -> bitmap
    private readonly BitmapSource?[] _slots = new BitmapSource?[23];
    // slot index -> WPF Image element
    private readonly Dictionary<int, Image> _images = new();

    public AmbientImageOverlay(ViewerContext ctx)
    {
        _ctx = ctx;
    }

    public void ClearAll()
    {
        Array.Clear(_slots, 0, _slots.Length);
        _ctx.AmbientLayer.Children.Clear();
        _images.Clear();
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= _slots.Length) return;
        _slots[index] = null;
        if (_images.Remove(index, out var img))
            _ctx.AmbientLayer.Children.Remove(img);
    }

    public bool HasSlot(int index) => index >= 0 && index < _slots.Length && _slots[index] != null;

    /// <summary>
    /// Provide a bitmap already converted to display format.
    /// </summary>
    public void SetSlot(int index, BitmapSource bitmap)
    {
        if (index < 0 || index >= _slots.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        _slots[index] = bitmap;
    }

    /// <summary>
    /// Loads a PNG from disk and converts it to a white-with-alpha bitmap.
    /// </summary>
    public BitmapSource LoadAndConvertMask(string path)
    {
        var bgra = ImageImport.LoadToBgra32(path, out int w, out int h);
        var outBuf = ConvertToWhiteAlpha(bgra);

        var bmp = BitmapSource.Create(
            w, h,
            96, 96,
            PixelFormats.Bgra32,
            null,
            outBuf,
            w * 4);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>
    /// Render loaded slots at positions provided by the GCA image table.
    /// Any slot without a position will not be displayed (but remains loaded/pending).
    /// </summary>
    public void RenderAtPositions(IEnumerable<(int index, int x, int y)> positions)
    {
        // Remove any displayed images that are no longer positioned
        var posDict = positions.ToDictionary(p => p.index, p => (p.x, p.y));
        var toRemove = _images.Keys.Where(k => !posDict.ContainsKey(k)).ToList();
        foreach (var k in toRemove)
        {
            var img = _images[k];
            _ctx.AmbientLayer.Children.Remove(img);
            _images.Remove(k);
        }

        // Add/update displayed images
        foreach (var (idx, (x, y)) in posDict)
        {
            var bmp = (idx >= 0 && idx < _slots.Length) ? _slots[idx] : null;
            if (bmp == null)
                continue;

            if (!_images.TryGetValue(idx, out var img))
            {
                img = new Image
                {
                    Source = bmp,
                    Stretch = Stretch.None,
                    SnapsToDevicePixels = true,
                    IsHitTestVisible = false
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                _images[idx] = img;
                _ctx.AmbientLayer.Children.Add(img);
            }
            else
            {
                img.Source = bmp;
            }

            Canvas.SetLeft(img, x);
            Canvas.SetTop(img, y);
        }
    }

    private static byte[] ConvertToWhiteAlpha(byte[] bgra)
    {
        // Input BGRA32. We output BGRA32 where RGB=255 and A=luminance (max channel).
        var dst = new byte[bgra.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            byte b = bgra[i + 0];
            byte g = bgra[i + 1];
            byte r = bgra[i + 2];

            byte a = r;
            if (g > a) a = g;
            if (b > a) a = b;

            dst[i + 0] = 255; // B
            dst[i + 1] = 255; // G
            dst[i + 2] = 255; // R
            dst[i + 3] = a;   // A
        }
        return dst;
    }
}
