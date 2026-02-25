using System.IO;
using System.Windows.Media.Imaging;

namespace GcaEditor.Imaging;

public static class ImageImport
{
    public static byte[] LoadToBgra32(string path, out int w, out int h)
    {
        using var ms = new MemoryStream(File.ReadAllBytes(path));
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        var converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);

        w = converted.PixelWidth;
        h = converted.PixelHeight;

        int stride = w * 4;
        var buffer = new byte[stride * h];
        converted.CopyPixels(buffer, stride, 0);
        return buffer;
    }
}
