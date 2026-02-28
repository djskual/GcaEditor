using System;
using System.Windows.Media.Imaging;

namespace GcaEditor.Models;

public sealed class EditorState
{
    public GcaDocument? Doc { get; set; }

    public BitmapSource?[] AmbientLhd { get; set; } = new BitmapSource?[23];
    public BitmapSource?[] AmbientRhd { get; set; } = new BitmapSource?[23];

    public string?[] AmbientLhdName { get; set; } = new string?[23];
    public string?[] AmbientRhdName { get; set; } = new string?[23];

    public bool[] AmbientVisibleLhd { get; set; } = new bool[23];
    public bool[] AmbientVisibleRhd { get; set; } = new bool[23];

    public EditorState DeepClone()
    {
        var dst = new EditorState();

        dst.Doc = Doc != null ? Doc.DeepClone() : null;

        dst.AmbientLhd = (BitmapSource?[])AmbientLhd.Clone();
        dst.AmbientRhd = (BitmapSource?[])AmbientRhd.Clone();

        dst.AmbientLhdName = (string?[])AmbientLhdName.Clone();
        dst.AmbientRhdName = (string?[])AmbientRhdName.Clone();

        dst.AmbientVisibleLhd = (bool[])AmbientVisibleLhd.Clone();
        dst.AmbientVisibleRhd = (bool[])AmbientVisibleRhd.Clone();

        return dst;
    }
}
