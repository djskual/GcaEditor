using GcaEditor.Views;
using System.Linq;
using System.Windows;

namespace GcaEditor.UI.Dialogs;

public static class AppMessageBox
{
    public static MessageBoxResult Show(string messageBoxText)
        => Show(null, messageBoxText, "GcaEditor", MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption)
        => Show(null, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        => Show(null, messageBoxText, caption, button, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        => Show(null, messageBoxText, caption, button, icon);

    public static MessageBoxResult Show(
        Window? owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon)
    {
        var dialog = new ThemedMessageBoxWindow(
            messageBoxText,
            caption,
            button,
            icon);

        var resolvedOwner = ResolveOwner(owner);
        if (resolvedOwner != null && resolvedOwner != dialog)
            dialog.Owner = resolvedOwner;

        dialog.ShowDialog();
        return dialog.Result;
    }

    private static Window? ResolveOwner(Window? owner)
    {
        if (owner != null)
            return owner;

        if (Application.Current == null)
            return null;

        var active = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive);

        return active ?? Application.Current.MainWindow;
    }
}
