using GcaEditor.Data;
using GcaEditor.UI.Dialogs;
using System.Windows;
using System.Windows.Controls;

namespace GcaEditor.Views;

public partial class ChooseCarWindow : Window
{
    private readonly CarCatalog _catalog;

    public bool IsCustom { get; private set; }
    public CarEntry? SelectedCar { get; private set; }
    public string SelectedMib { get; private set; } = "MIB25";
    public string SelectedSide { get; private set; } = "LHD";

    public ChooseCarWindow(CarCatalog catalog)
    {
        InitializeComponent();
        _catalog = catalog;

        CarCombo.ItemsSource = _catalog.cars;
        CarCombo.SelectionChanged += CarCombo_SelectionChanged;

        if (_catalog.cars.Count > 0)
            CarCombo.SelectedIndex = 0;
    }

    private void CarCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var car = CarCombo.SelectedItem as CarEntry;
        if (car == null) return;

        MibCombo.ItemsSource = car.available_mibs;
        if (car.available_mibs.Count > 0)
            MibCombo.SelectedIndex = 0;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        IsCustom = CustomCheck.IsChecked == true;
        if (IsCustom)
        {
            DialogResult = true;
            return;
        }

        SelectedCar = CarCombo.SelectedItem as CarEntry;
        if (SelectedCar == null)
        {
            AppMessageBox.Show("Select a car.");
            return;
        }

        SelectedMib = (MibCombo.SelectedItem as string) ?? "MIB25";
        SelectedSide = (RhdRadio.IsChecked == true) ? "RHD" : "LHD";

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
