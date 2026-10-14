using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmiWifiProvisioning.Models;
using SmiWifiProvisioning.Services.Implementations;
using SmiWifiProvisioning.Services.Interfaces;
using SmiWifiProvisioning.Views;

namespace SmiWifiProvisioning.ViewModels;

public partial class ConfigUsrViewModel : ObservableObject
{
    private readonly IUsrDeviceService _deviceService;

    [ObservableProperty]
    private ObservableCollection<string> _availablePorts = new();

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private string _ssid = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isDebugLogEnabled = true;

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public ConfigUsrViewModel()
    {
        _deviceService = new UsrDeviceService();
        RefreshPorts();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (string port in SerialPort.GetPortNames())
        {
            AvailablePorts.Add(port);
        }
        if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }

    [RelayCommand]
    private async Task ScanWifiAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort)) return;

        IsBusy = true;
        LogText = string.Empty;
        StatusMessage = "Memindai Wi-Fi...";

        try
        {
            List<WifiNetwork> networks = await Task.Run(() => _deviceService.ScanWifiAsync(
                SelectedPort, Log
            ));

            StatusMessage = "Pemindaian selesai";

            // Tampilkan Window Popup Hasil Pemindaian
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new WifiScanResultWindow(networks)
                {
                    Owner = Application.Current.MainWindow
                };
                if (dialog.ShowDialog() == true && dialog.SelectedNetwork != null)
                {
                    Ssid = dialog.SelectedNetwork.Ssid;
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = "Scan Gagal";
            MessageBox.Show($"Pemindaian Wi-Fi gagal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConnectWifiAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort) || string.IsNullOrWhiteSpace(Ssid))
        {
            MessageBox.Show("Pilih COM Port dan masukkan SSID terlebih dahulu.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() => _deviceService.ConnectWifiAsync(
                SelectedPort, Ssid, Password, Log, (msg) => StatusMessage = msg
            ));

            StatusMessage = "Wi-Fi Berhasil Dikonfigurasi";
            MessageBox.Show("Pengaturan Wi-Fi berhasil dikirim ke modul.", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = "Koneksi Gagal";
            MessageBox.Show($"Gagal mengatur Wi-Fi: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckDhcpAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort)) return;

        IsBusy = true;
        try
        {
            string dhcpInfo = await Task.Run(() => _deviceService.CheckDhcpAsync(SelectedPort, Log));
            StatusMessage = string.IsNullOrWhiteSpace(dhcpInfo) ? "Tidak Ada IP" : dhcpInfo;
        }
        catch
        {
            StatusMessage = "Tidak Ada IP";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Log(string message)
    {
        if (!IsDebugLogEnabled) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            LogText += $"{message}\n";
        });
    }
}