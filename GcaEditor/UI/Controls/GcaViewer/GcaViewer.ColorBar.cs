using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GcaEditor.UI.Controls;

public partial class GcaViewer
{
    private void ColorSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b) return;
        if (b.Tag is not string s) return;

        if (!TryParseArgbHex(s, out var color))
            return;

        SetAmbientTintColor(color);
        e.Handled = true;
    }

    private static bool TryParseArgbHex(string s, out Color color)
    {
        color = Colors.White;

        // Accept "#AARRGGBB" or "#RRGGBB"
        if (string.IsNullOrWhiteSpace(s)) return false;

        s = s.Trim();
        if (s.StartsWith("#", StringComparison.Ordinal))
            s = s.Substring(1);

        if (s.Length == 6)
            s = "FF" + s;

        if (s.Length != 8) return false;

        if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return false;

        byte a = (byte)((v >> 24) & 0xFF);
        byte r = (byte)((v >> 16) & 0xFF);
        byte g = (byte)((v >> 8) & 0xFF);
        byte b = (byte)(v & 0xFF);

        color = Color.FromArgb(a, r, g, b);
        return true;
    }
}
