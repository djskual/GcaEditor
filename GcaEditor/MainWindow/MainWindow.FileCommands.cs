using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using GcaEditor.IO;

namespace GcaEditor;

public partial class MainWindow
{
    private void ImportBackground_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            Title = "Fond menu lumieres (1280x556)"
        };
        if (ofd.ShowDialog() != true) return;

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new Uri(ofd.FileName);
        bi.EndInit();
        bi.Freeze();

        Viewer.SetBackground(bi);
        Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, ViewerHost.ActualHeight);

        // Now we can open a GCA
        OpenGcaButton.IsEnabled = true;
    }

    private void ViewerHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Viewer.HasBackground)
            Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, ViewerHost.ActualHeight);
    }

    private void OpenGca_Click(object sender, RoutedEventArgs e)
    {
        if (!Viewer.HasBackground)
        {
            MessageBox.Show("Tu dois d'abord importer un background (PNG 1280x556) avant de charger un .gca.");
            return;
        }

        var ofd = new OpenFileDialog
        {
            Filter = "GCA (*.gca)|*.gca|All files|*.*",
            Title = "Ouvrir un fichier GCA"
        };
        if (ofd.ShowDialog() != true) return;

        _gcaPath = ofd.FileName;
        _doc = GcaCodec.Load(_gcaPath);

        _history.Clear();
        Viewer.LoadDocument(_doc);

        CaptureInitialAmbientIds();

        SaveGcaButton.IsEnabled = true;

        RefreshZonesUi();

        // Refresh ambient status (some IDs may already be positioned in the GCA)
        RefreshAmbientUi();
    }

    private void SaveGca_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
        {
            MessageBox.Show("Aucun GCA charge.");
            return;
        }

        var sfd = new SaveFileDialog
        {
            Filter = "GCA (*.gca)|*.gca",
            Title = "Enregistrer le GCA",
            FileName = _gcaPath != null ? System.IO.Path.GetFileName(_gcaPath) : "menu.gca"
        };

        if (sfd.ShowDialog() != true) return;

        GcaCodec.Save(sfd.FileName, _doc);
        MessageBox.Show("GCA sauvegarde.");
    }

    private void CaptureInitialAmbientIds()
    {
        _ambientIdsInitiallyInDoc.Clear();
        if (_doc == null) return;

        foreach (var img in _doc.Images)
            _ambientIdsInitiallyInDoc.Add(img.Id);
    }

}
