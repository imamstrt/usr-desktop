using System.Windows;
using System.Windows.Input;
using SmiWifiProvisioning.Models;

namespace SmiWifiProvisioning.Views;

public partial class WifiScanResultWindow : Window
{
    public WifiNetwork? SelectedNetwork { get; private set; }

    public WifiScanResultWindow(List<WifiNetwork> networks)
    {
        InitializeComponent();
        GridWifi.ItemsSource = networks;
    }

    private void BtnSelect_Click(object sender, RoutedEventArgs e)
    {
        SelectAndClose();
    }

    private void GridWifi_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        SelectAndClose();
    }

    private void SelectAndClose()
    {
        if (GridWifi.SelectedItem is WifiNetwork selected)
        {
            SelectedNetwork = selected;
            DialogResult = true;
            Close();
        }
    }
}