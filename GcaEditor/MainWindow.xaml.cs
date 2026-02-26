using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GcaEditor.IO;
using GcaEditor.Models;
using GcaEditor.UndoRedo;

namespace GcaEditor;

public partial class MainWindow : Window
{
    private GcaDocument? _doc;
    private string? _gcaPath;

    private readonly UndoRedoStack<GcaDocument> _history;

    public MainWindow()
    {
        InitializeComponent();

        _history = new UndoRedoStack<GcaDocument>(d => d.DeepClone());

        Viewer.ZoneDragCommitted += (_, beforeSnapshot) =>
        {
            if (_doc == null) return;
            _history.PushUndoSnapshot(beforeSnapshot);
        };

        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        if (e.Key == Key.Z)
        {
            if (_doc != null && _history.CanUndo)
            {
                _doc = _history.Undo(_doc);
                Viewer.LoadDocument(_doc);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Y)
        {
            if (_doc != null && _history.CanRedo)
            {
                _doc = _history.Redo(_doc);
                Viewer.LoadDocument(_doc);
            }
            e.Handled = true;
        }
    }

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

        // Redimensionne le viewer dans l'espace disponible + Fit stable
        Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, ViewerHost.ActualHeight);
    }

    private void ViewerHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Si un background est charge, on recalcule la taille optimale
        if (Viewer.HasBackground)
            Viewer.SizeToHostAndFit(ViewerHost.ActualWidth, ViewerHost.ActualHeight);
    }

    private void OpenGca_Click(object sender, RoutedEventArgs e)
    {
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
}
