using System.Windows.Interop;
using GcaEditor.Settings;

namespace GcaEditor.UI.Controls;

public partial class GcaViewer
{
    private const int WM_MOUSEHWHEEL = 0x020E;

    private void AttachHwndHook()
    {
        var source = (HwndSource)System.Windows.PresentationSource.FromVisual(this)!;
        source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEHWHEEL)
        {
            int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            double step = (delta / 120.0) * 140.0;

            if (AppSettingsStore.Current.InvertHorizontalTrackpadScrolling)
                EditorScroll.ScrollToHorizontalOffset(EditorScroll.HorizontalOffset + step);
            else
                EditorScroll.ScrollToHorizontalOffset(EditorScroll.HorizontalOffset - step);

            handled = true;
        }
        return IntPtr.Zero;
    }
}
