using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmiWifiProvisioning.Helpers;
using SmiWifiProvisioning.Models;
using SmiWifiProvisioning.Services.Implementations;
using SmiWifiProvisioning.Services.Interfaces;

namespace SmiWifiProvisioning.ViewModels;

public partial class ConfigProduksiViewModel : ObservableObject
{
    private readonly IUsrDeviceService _deviceService;
    private readonly ICsvLoggerService _loggerService;

    private string _statusText = "IDLE";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "Siap. Silakan scan barcode meter.";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    private Brush _statusColor = Brushes.Gray;
    public Brush StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(); }
    }

    [ObservableProperty] private ObservableCollection<string> _availablePorts = new();
    [ObservableProperty] private string? _selectedPort;
    [ObservableProperty] private string _meterId = string.Empty;
    [ObservableProperty] private string _logText = string.Empty;

    // Properti Pilihan Tipe Modul (DR162 / DR404)
    [ObservableProperty] private UsrDeviceType _selectedDeviceType = UsrDeviceType.DR162;
    public UsrDeviceType[] DeviceTypeOptions { get; } = Enum.GetValues<UsrDeviceType>();

    // Properti Parameter Manual
    [ObservableProperty] private string _apPassword = ProductionConfig.ApPassword;
    [ObservableProperty] private int _serverPort = ProductionConfig.ServerPort;
    [ObservableProperty] private string _webPassword = ProductionConfig.WebPassword;
    [ObservableProperty] private int _targetBaudRate = ProductionConfig.TargetBaudRate;
    [ObservableProperty] private int _dataBits = ProductionConfig.DataBits;
    [ObservableProperty] private int _stopBits = ProductionConfig.StopBits;
    [ObservableProperty] private string _parity = ProductionConfig.Parity;

    public int[] StandardBaudRates { get; } = ProductionConfig.StandardBaudRates;
    public string[] ParityOptions { get; } = new[] { "NONE", "EVEN", "ODD" };
    public int[] StopBitsOptions { get; } = new[] { 1, 2 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public ConfigProduksiViewModel()
    {
        _deviceService = new UsrDeviceService();
        _loggerService = new CsvLoggerService();
        RefreshPorts();
    }

    partial void OnMeterIdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsBusy)
            return;

        string input = value.Trim();

        // 1. DR162 Trigger: Tepat 11 Karakter
        if (SelectedDeviceType == UsrDeviceType.DR162 && input.Length == 11)
        {
            _ = StartProvisioningCommand.ExecuteAsync(null);
        }
        // 2. DR404 Trigger: Tepat 40 Karakter / mengandung "MAC:"
        else if (SelectedDeviceType == UsrDeviceType.DR404 && (input.Length >= 40 || input.Contains("MAC:", StringComparison.OrdinalIgnoreCase)))
        {
            _ = StartProvisioningCommand.ExecuteAsync(null);
        }
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
    private async Task StartProvisioningAsync()
    {
        if (string.IsNullOrWhiteSpace(MeterId) || string.IsNullOrWhiteSpace(SelectedPort))
        {
            StatusText = "SIAP";
            StatusMessage = "Pilih COM Port dan scan barcode/QR terlebih dahulu.";
            StatusColor = Brushes.Gray;
            return;
        }

        IsBusy = true;
        LogText = string.Empty;
        string currentPort = SelectedPort;
        string rawInput = MeterId;

        // Ekstrak ID Target: DR404 mengekstrak MAC Address 12 digit
        string targetDeviceCode = SelectedDeviceType == UsrDeviceType.DR404
            ? QrParser.ExtractMacAddress(rawInput)
            : rawInput;

        string status = "FAILED";
        string errorMessage = string.Empty;

        var provisioningConfig = new ProvisioningConfig
        {
            DeviceType = SelectedDeviceType,
            ApPassword = ApPassword,
            ServerPort = ServerPort,
            WebPassword = WebPassword,
            TargetBaudRate = TargetBaudRate,
            DataBits = DataBits,
            StopBits = StopBits,
            Parity = Parity
        };

        StatusText = "PROSES";
        StatusMessage = $"Provisioning [{SelectedDeviceType}] running untuk Target ID: {targetDeviceCode}...";
        StatusColor = Brushes.DodgerBlue;

        try
        {
            await Task.Run(() => _deviceService.ProvisionDeviceAsync(
                currentPort,
                targetDeviceCode, // Mengirim MAC Address 12 digit
                provisioningConfig,
                AppendLog
            ));

            status = "SUCCESS";
            StatusText = "BERHASIL";
            StatusMessage = $"✓ Provisioning [{SelectedDeviceType}] BERHASIL untuk {targetDeviceCode}.";
            StatusColor = Brushes.SeaGreen;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            AppendLog($"[ERROR] {errorMessage}");
            StatusText = "GAGAL";
            StatusMessage = $"✗ Provisioning [{SelectedDeviceType}] GAGAL: {errorMessage}";
            StatusColor = Brushes.IndianRed;
        }
        finally
        {
            await _loggerService.WriteLogAsync(currentPort, targetDeviceCode, status, errorMessage);
            MeterId = string.Empty;   // Kosongkan TextBox
            IsBusy = false;           // Trigger Auto-Focus
        }
    }

    public void AppendLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"{timestamp} {message}\n";
        });
    }
}