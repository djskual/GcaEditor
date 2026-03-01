using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GcaEditor.UI.Interop;
using GcaEditor.Data;
using GcaEditor.IO;
using GcaEditor.Models;
using GcaEditor.UndoRedo;

namespace GcaEditor;

public partial class MainWindow : Window
{
    private GcaDocument? _doc;
    private string? _gcaPath;

    private readonly UndoRedoStack<EditorState> _history;
    private readonly ZoneCatalog _zoneCatalog;

    private bool _suppressListSelection;
    private bool _uiReady = false;

    public MainWindow()
    {
        InitializeComponent();

        _zoneCatalog = ZoneCatalog.LoadOrDefault();
        _history = new UndoRedoStack<EditorState>(s => s.DeepClone());

        WireViewerEvents();
        WireWindowEvents();

        // Must load a background before opening / saving a GCA
        OpenGcaButton.IsEnabled = Viewer.HasBackground;
        SaveGcaButton.IsEnabled = false;

        RefreshZonesUi();

        Loaded += (_, __) =>
        {
            _uiReady = true;

            InitAmbientUiOnLoaded();
            UpdateAmbientAvailability();
        };
    }

    private void WireWindowEvents()
    {
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;
    }

    private void WireViewerEvents()
    {
        Viewer.ZoneDragCommitted += (_, beforeSnapshot) =>
        {
            if (_doc == null) return;
            _history.PushUndoSnapshot(CaptureState(beforeSnapshot));
            RefreshZonesUi();
        };

        Viewer.SelectedZoneChanged += (_, zoneId) =>
        {
            if (_suppressListSelection) return;

            _suppressListSelection = true;
            try
            {
                if (zoneId == null)
                {
                    ZonesList.SelectedIndex = -1;
                }
                else
                {
                    for (int i = 0; i < ZonesList.Items.Count; i++)
                    {
                        if (ZonesList.Items[i] is ZoneListItem it && it.Id == zoneId.Value)
                        {
                            ZonesList.SelectedIndex = i;
                            ZonesList.ScrollIntoView(ZonesList.Items[i]);
                            break;
                        }
                    }
                }
            }
            finally
            {
                _suppressListSelection = false;
            }
        };

        Viewer.SetZoneNames(_zoneCatalog.Names);

        Viewer.AmbientPlaceRequested += Viewer_AmbientPlaceRequested;

        Viewer.AmbientMoveCommitted += (_, e) =>
        {
            if (_doc == null) return;

            // Snapshot before mutating doc
            _history.PushUndoSnapshot(CaptureState());

            var img = _doc.Images.FirstOrDefault(x => x.Id == (ushort)e.Id);
            if (img == null) return;

            var clamped = Viewer.ClampAmbientTopLeft(e.Id, e.NewX, e.NewY);

            img.X = (ushort)Math.Round(clamped.X);
            img.Y = (ushort)Math.Round(clamped.Y);

            Viewer.LoadDocument(_doc);
            ApplyAmbientSideToViewer();
            RefreshAmbientUi();

            ExitAmbientMoveMode();
        };
    }
}
