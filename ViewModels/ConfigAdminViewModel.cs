using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmiWifiProvisioning.Services.Implementations;
using SmiWifiProvisioning.Services.Interfaces;

namespace SmiWifiProvisioning.ViewModels;

public partial class ConfigAdminViewModel : ObservableObject
{
    private readonly IUsrDeviceService _deviceService;

    [ObservableProperty] private ObservableCollection<string> _availablePorts = new();
    [ObservableProperty] private string? _selectedPort;
    [ObservableProperty] private string _logText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    // --- AT Command Manual ---
    [ObservableProperty] private string _manualCmd = "AT+VER";
    [ObservableProperty] private string _manualResp = string.Empty;

    // --- Mode USR (AT+WMODE) ---
    [ObservableProperty] private ObservableCollection<string> _wmodeOptions = new() { "APSTA", "AP", "STA" };
    [ObservableProperty] private string _selectedWmode = "APSTA";
    [ObservableProperty] private string _wmodeResp = string.Empty;

    // --- AP Parameter (AT+WAP) ---
    [ObservableProperty] private ObservableCollection<string> _wapModeOptions = new() { "11BGN", "11BG", "11B" };
    [ObservableProperty] private string _selectedWapMode = "11BGN";
    [ObservableProperty] private string _wapSsid = "0015454";
    [ObservableProperty] private ObservableCollection<string> _wapChannelOptions = new() { "AUTO", "1", "6", "11" };
    [ObservableProperty] private string _selectedWapChannel = "AUTO";
    [ObservableProperty] private string _wapResp = string.Empty;

    // --- Password AP (AT+WAKEY) ---
    [ObservableProperty] private ObservableCollection<string> _wakeyAuthOptions = new() { "WPA2PSK", "WPAPSK", "OPEN" };
    [ObservableProperty] private string _selectedWakeyAuth = "WPA2PSK";
    [ObservableProperty] private ObservableCollection<string> _wakeyEncOptions = new() { "AES", "TKIP", "NONE" };
    [ObservableProperty] private string _selectedWakeyEnc = "AES";
    [ObservableProperty] private string _wakeyPass = "SMI@123321";
    [ObservableProperty] private string _wakeyResp = string.Empty;

    // --- TCP Protokol (AT+NETP) ---
    [ObservableProperty] private ObservableCollection<string> _netpProtoOptions = new() { "TCP", "UDP", "HTTP", "MQTT" };
    [ObservableProperty] private string _selectedNetpProto = "TCP";
    [ObservableProperty] private ObservableCollection<string> _netpCsOptions = new() { "CLIENT", "SERVER" };
    [ObservableProperty] private string _selectedNetpCs = "CLIENT";
    [ObservableProperty] private string _netpPort = "5001";
    [ObservableProperty] private string _netpIp = "smarttoken.id";
    [ObservableProperty] private string _netpResp = string.Empty;

    // --- MQTT Dynamic State & Extra Parameters ---
    [ObservableProperty] private bool _isMqttMode;
    [ObservableProperty] private string _mqttClientId = "USR-DR404";
    [ObservableProperty] private string _mqttUser = "";
    [ObservableProperty] private string _mqttPass = "";
    [ObservableProperty] private string _mqttSubTopic = "device/sub";
    [ObservableProperty] private string _mqttPubTopic = "device/pub";

    partial void OnSelectedNetpProtoChanged(string value)
    {
        IsMqttMode = value.Equals("MQTT", StringComparison.OrdinalIgnoreCase);
    }

    // --- Web GUI Parameter (AT+WEBU) ---
    [ObservableProperty] private string _webuUser = "0015454";
    [ObservableProperty] private string _webuPass = "SMI@123321";
    [ObservableProperty] private string _webuResp = string.Empty;

    // --- Echo On/Off (AT+EVENT) ---
    [ObservableProperty] private ObservableCollection<string> _eventOptions = new() { "off", "on" };
    [ObservableProperty] private string _selectedEvent = "off";
    [ObservableProperty] private string _eventResp = string.Empty;

    // --- Heartbeat Awal (AT+REGEN) ---
    [ObservableProperty] private ObservableCollection<string> _regenOptions = new() { "USR", "OFF", "SN", "MAC", "CLOUD" };
    [ObservableProperty] private string _selectedRegen = "USR";
    [ObservableProperty] private string _regenResp = string.Empty;

    // --- Data Heartbeat Awal (AT+REGUSR) ---
    [ObservableProperty] private ObservableCollection<string> _regusrTypeOptions = new() { "HEX", "ASC" };
    [ObservableProperty] private string _selectedRegusrType = "HEX";
    [ObservableProperty] private string _regusrData = "00154540";
    [ObservableProperty] private string _regusrResp = string.Empty;

    // --- Jumlah Heartbeat Awal (AT+REGSND) ---
    [ObservableProperty] private ObservableCollection<string> _regsndOptions = new() { "FIRST", "EVERY" };
    [ObservableProperty] private string _selectedRegsnd = "FIRST";
    [ObservableProperty] private string _regsndResp = string.Empty;

    // --- TCP Timeout (AT+TCPTO) ---
    [ObservableProperty] private string _tcptoTime = "600";
    [ObservableProperty] private string _tcptoResp = string.Empty;

    // --- Heartbeat Parameter (AT+NETHEARTCFG) ---
    [ObservableProperty] private ObservableCollection<string> _heartSwitchOptions = new() { "ON", "OFF" };
    [ObservableProperty] private string _selectedHeartSwitch = "ON";
    [ObservableProperty] private string _heartCycle = "900";
    [ObservableProperty] private ObservableCollection<string> _heartTypeOptions = new() { "HEX", "ASC" };
    [ObservableProperty] private string _selectedHeartType = "HEX";
    [ObservableProperty] private string _heartPayload = "00154540";
    [ObservableProperty] private string _heartResp = string.Empty;

    // --- PARAMETER SOCKET B (SOCKB) ---
    [ObservableProperty] private ObservableCollection<string> _sockbProtoOptions = new() { "TCP", "UDP", "UDPS", "IGMP", "OFF" };
    [ObservableProperty] private string _selectedSockbProto = "TCP";
    [ObservableProperty] private string _sockbPort = "6000";
    [ObservableProperty] private string _sockbIp = "192.168.1.100";
    [ObservableProperty] private string _sockbResp = string.Empty;

    // AT+TCPDISB (Koneksi TCP Client Socket B)
    [ObservableProperty] private ObservableCollection<string> _tcpdisbOptions = new() { "on", "off" };
    [ObservableProperty] private string _selectedTcpdisbSta = "on";
    [ObservableProperty] private ObservableCollection<string> _tcpdisbOptOptions = new() { "0", "1" };
    [ObservableProperty] private string _selectedTcpdisbOpt = "0";
    [ObservableProperty] private string _tcpdisbResp = string.Empty;

    // AT+TCPTOB (TCP Timeout Socket B)
    [ObservableProperty] private string _tcptobTime = "600";
    [ObservableProperty] private string _tcptobResp = string.Empty;

    // AT+TCPLKB (Status Koneksi Socket B - Read Only)
    [ObservableProperty] private string _tcplkbResp = string.Empty;

    // AT+UDPLCPT (Port Lokal UDP Socket A & B)
    [ObservableProperty] private string _udplcptPortA = "0";
    [ObservableProperty] private string _udplcptPortB = "0";
    [ObservableProperty] private string _udplcptResp = string.Empty;

    // --- AT Command Umum Lainnya ---
    [ObservableProperty] private string _wsssidSsid = string.Empty;
    [ObservableProperty] private string _wsssidResp = string.Empty;
    [ObservableProperty] private string _lanIp = string.Empty;
    [ObservableProperty] private string _lanResp = string.Empty;
    [ObservableProperty] private string _wannResp = string.Empty;
    [ObservableProperty] private string _snResp = string.Empty;
    [ObservableProperty] private string _wsmacResp = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _wifiSwitchOptions = new() { "on", "off" };
    [ObservableProperty] private string _selectedWifiSwitch = "on";
    [ObservableProperty] private string _wifiResp = string.Empty;
    [ObservableProperty] private string _wslkResp = string.Empty;
    [ObservableProperty] private string _reldPassword = "123smi321";
    [ObservableProperty] private string _reldResp = string.Empty;

    public ConfigAdminViewModel()
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

    public void AppendLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}\n";
        });
    }

    private async Task QueryCommandAsync(string getCmd, Action<string> updateUiValue, Action<string> setRawResponse)
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            MessageBox.Show("Pilih COM Port terlebih dahulu.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        setRawResponse("Reading...");
        try
        {
            string result = await Task.Run(() => _deviceService.SendSingleAtCommandAsync(SelectedPort, getCmd, AppendLog));
            ProcessResponse(result, setRawResponse, updateUiValue);
        }
        catch (Exception ex)
        {
            setRawResponse($"ERR: {ex.Message}");
            AppendLog($"[ERROR] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Executions Read (Query) ---
    [RelayCommand] private async Task ReadWmodeAsync() => await QueryCommandAsync("AT+WMODE", val => SelectedWmode = val, res => WmodeResp = res);
    [RelayCommand] private async Task ReadWapAsync() => await QueryCommandAsync("AT+WAP", val => { var p = val.Split(','); if (p.Length >= 3) { SelectedWapMode = p[0]; WapSsid = p[1]; SelectedWapChannel = p[2]; } }, res => WapResp = res);
    [RelayCommand] private async Task ReadWakeyAsync() => await QueryCommandAsync("AT+WAKEY", val => { var p = val.Split(','); if (p.Length >= 3) { SelectedWakeyAuth = p[0]; SelectedWakeyEnc = p[1]; WakeyPass = p[2]; } }, res => WakeyResp = res);
    [RelayCommand] private async Task ReadNetpAsync() => await QueryCommandAsync("AT+NETP", val => { var p = val.Split(','); if (p.Length >= 3) { SelectedNetpProto = p[0]; SelectedNetpCs = p[1]; NetpPort = p[2]; if (p.Length >= 4) NetpIp = p[3]; } }, res => NetpResp = res);
    [RelayCommand] private async Task ReadWebuAsync() => await QueryCommandAsync("AT+WEBU", val => { var p = val.Split(','); if (p.Length >= 2) { WebuUser = p[0]; WebuPass = p[1]; } }, res => WebuResp = res);
    [RelayCommand] private async Task ReadEventAsync() => await QueryCommandAsync("AT+EVENT", val => SelectedEvent = val, res => EventResp = res);
    [RelayCommand] private async Task ReadRegenAsync() => await QueryCommandAsync("AT+REGEN", val => SelectedRegen = val, res => RegenResp = res);
    [RelayCommand] private async Task ReadRegusrAsync() => await QueryCommandAsync("AT+REGUSR", val => { var p = val.Split(','); if (p.Length >= 2) { SelectedRegusrType = p[0]; RegusrData = p[1]; } }, res => RegusrResp = res);
    [RelayCommand] private async Task ReadRegsndAsync() => await QueryCommandAsync("AT+REGSND", val => SelectedRegsnd = val, res => RegsndResp = res);
    [RelayCommand] private async Task ReadTcptoAsync() => await QueryCommandAsync("AT+TCPTO", val => TcptoTime = val, res => TcptoResp = res);
    [RelayCommand] private async Task ReadNetheartAsync() => await QueryCommandAsync("AT+NETHEARTCFG", val => { var p = val.Split(','); if (p.Length >= 4) { SelectedHeartSwitch = p[0]; HeartCycle = p[1]; SelectedHeartType = p[2]; HeartPayload = p[3]; } }, res => HeartResp = res);

    // Kueri Socket B
    [RelayCommand] private async Task ReadSockbAsync() => await QueryCommandAsync("AT+SOCKB", val => { var p = val.Split(','); if (p.Length >= 3) { SelectedSockbProto = p[0]; SockbPort = p[1]; SockbIp = p[2]; } }, res => SockbResp = res);
    [RelayCommand] private async Task ReadTcpdisbAsync() => await QueryCommandAsync("AT+TCPDISB", val => SelectedTcpdisbSta = val, res => TcpdisbResp = res);
    [RelayCommand] private async Task ReadTcptobAsync() => await QueryCommandAsync("AT+TCPTOB", val => TcptobTime = val, res => TcptobResp = res);
    [RelayCommand] private async Task ReadTcplkbAsync() => await QueryCommandAsync("AT+TCPLKB", _ => { }, res => TcplkbResp = res);
    [RelayCommand] private async Task ReadUdplcptAsync() => await QueryCommandAsync("AT+UDPLCPT", val => { var p = val.Split(','); if (p.Length >= 2) { UdplcptPortA = p[0]; UdplcptPortB = p[1]; } }, res => UdplcptResp = res);

    // Kueri Status Sistem
    [RelayCommand] private async Task ReadWsssidAsync() => await QueryCommandAsync("AT+WSSSID", val => WsssidSsid = val, res => WsssidResp = res);
    [RelayCommand] private async Task ReadLanAsync() => await QueryCommandAsync("AT+LAN", val => LanIp = val, res => LanResp = res);
    [RelayCommand] private async Task ReadWannAsync() => await QueryCommandAsync("AT+WANN", _ => { }, res => WannResp = res);
    [RelayCommand] private async Task ReadSnAsync() => await QueryCommandAsync("AT+SN", _ => { }, res => SnResp = res);
    [RelayCommand] private async Task ReadWsmacAsync() => await QueryCommandAsync("AT+WSMAC", _ => { }, res => WsmacResp = res);
    [RelayCommand] private async Task ReadWifiAsync() => await QueryCommandAsync("AT+WIFI", val => SelectedWifiSwitch = val, res => WifiResp = res);
    [RelayCommand] private async Task ReadWslkAsync() => await QueryCommandAsync("AT+WSLK", _ => { }, res => WslkResp = res);

    // --- Batch Master Read ---
    [RelayCommand]
    private async Task ReadAllConfigsAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            MessageBox.Show("Pilih COM Port terlebih dahulu.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        LogText = string.Empty;
        AppendLog("=== MEMULAI BACA SEMUA PARAMETER ===");
        try
        {
            var cmdList = new List<string>
            {
                "AT+WMODE", "AT+WAP", "AT+WAKEY", "AT+NETP", "AT+WEBU",
                "AT+EVENT", "AT+REGEN", "AT+REGUSR", "AT+REGSND", "AT+TCPTO", "AT+NETHEARTCFG",
                "AT+SOCKB", "AT+TCPDISB", "AT+TCPTOB", "AT+TCPLKB", "AT+UDPLCPT",
                "AT+WSSSID", "AT+LAN", "AT+WANN", "AT+SN", "AT+WSMAC", "AT+WIFI", "AT+WSLK"
            };

            var responses = await Task.Run(() => _deviceService.SendBatchAtCommandsAsync(SelectedPort, cmdList, AppendLog));

            if (responses.TryGetValue("AT+WMODE", out var wmodeRes)) ProcessResponse(wmodeRes, res => WmodeResp = res, val => SelectedWmode = val);
            if (responses.TryGetValue("AT+WAP", out var wapRes)) ProcessResponse(wapRes, res => WapResp = res, val => { var p = val.Split(','); if (p.Length >= 3) { SelectedWapMode = p[0]; WapSsid = p[1]; SelectedWapChannel = p[2]; } });
            if (responses.TryGetValue("AT+WAKEY", out var wakeyRes)) ProcessResponse(wakeyRes, res => WakeyResp = res, val => { var p = val.Split(','); if (p.Length >= 3) { SelectedWakeyAuth = p[0]; SelectedWakeyEnc = p[1]; WakeyPass = p[2]; } });
            if (responses.TryGetValue("AT+NETP", out var netpRes)) ProcessResponse(netpRes, res => NetpResp = res, val => { var p = val.Split(','); if (p.Length >= 3) { SelectedNetpProto = p[0]; SelectedNetpCs = p[1]; NetpPort = p[2]; if (p.Length >= 4) NetpIp = p[3]; } });
            if (responses.TryGetValue("AT+WEBU", out var webuRes)) ProcessResponse(webuRes, res => WebuResp = res, val => { var p = val.Split(','); if (p.Length >= 2) { WebuUser = p[0]; WebuPass = p[1]; } });
            if (responses.TryGetValue("AT+EVENT", out var eventRes)) ProcessResponse(eventRes, res => EventResp = res, val => SelectedEvent = val);
            if (responses.TryGetValue("AT+REGEN", out var regenRes)) ProcessResponse(regenRes, res => RegenResp = res, val => SelectedRegen = val);
            if (responses.TryGetValue("AT+REGUSR", out var regusrRes)) ProcessResponse(regusrRes, res => RegusrResp = res, val => { var p = val.Split(','); if (p.Length >= 2) { SelectedRegusrType = p[0]; RegusrData = p[1]; } });
            if (responses.TryGetValue("AT+REGSND", out var regsndRes)) ProcessResponse(regsndRes, res => RegsndResp = res, val => { var p = val.Split(','); if (p.Length >= 1) SelectedRegsnd = p[0]; });
            if (responses.TryGetValue("AT+TCPTO", out var tcptoRes)) ProcessResponse(tcptoRes, res => TcptoResp = res, val => TcptoTime = val);
            if (responses.TryGetValue("AT+NETHEARTCFG", out var heartRes)) ProcessResponse(heartRes, res => HeartResp = res, val => { var p = val.Split(','); if (p.Length >= 4) { SelectedHeartSwitch = p[0]; HeartCycle = p[1]; SelectedHeartType = p[2]; HeartPayload = p[3]; } });

            // Socket B Batch
            if (responses.TryGetValue("AT+SOCKB", out var sockbRes)) ProcessResponse(sockbRes, res => SockbResp = res, val => { var p = val.Split(','); if (p.Length >= 3) { SelectedSockbProto = p[0]; SockbPort = p[1]; SockbIp = p[2]; } });
            if (responses.TryGetValue("AT+TCPDISB", out var tcpdisbRes)) ProcessResponse(tcpdisbRes, res => TcpdisbResp = res, val => SelectedTcpdisbSta = val);
            if (responses.TryGetValue("AT+TCPTOB", out var tcptobRes)) ProcessResponse(tcptobRes, res => TcptobResp = res, val => TcptobTime = val);
            if (responses.TryGetValue("AT+TCPLKB", out var tcplkbRes)) ProcessResponse(tcplkbRes, res => TcplkbResp = res, _ => { });
            if (responses.TryGetValue("AT+UDPLCPT", out var udplcptRes)) ProcessResponse(udplcptRes, res => UdplcptResp = res, val => { var p = val.Split(','); if (p.Length >= 2) { UdplcptPortA = p[0]; UdplcptPortB = p[1]; } });

            // Status Sistem
            if (responses.TryGetValue("AT+WSSSID", out var wsssidRes)) ProcessResponse(wsssidRes, res => WsssidResp = res, val => WsssidSsid = val);
            if (responses.TryGetValue("AT+LANN", out var lanRes)) ProcessResponse(lanRes, res => LanResp = res, val => LanIp = val);
            if (responses.TryGetValue("AT+WANN", out var wannRes)) ProcessResponse(wannRes, res => WannResp = res, _ => { });
            if (responses.TryGetValue("AT+SN", out var snRes)) ProcessResponse(snRes, res => SnResp = res, _ => { });
            if (responses.TryGetValue("AT+WSMAC", out var wsmacRes)) ProcessResponse(wsmacRes, res => WsmacResp = res, _ => { });
            if (responses.TryGetValue("AT+WIFI", out var wifiRes)) ProcessResponse(wifiRes, res => WifiResp = res, val => SelectedWifiSwitch = val);
            if (responses.TryGetValue("AT+WSLK", out var wslkRes)) ProcessResponse(wslkRes, res => WslkResp = res, _ => { });

            AppendLog("=== SELESAI MEMBACA SEMUA PARAMETER ===");
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] {ex.Message}");
            MessageBox.Show($"Gagal membaca semua parameter: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ProcessResponse(string rawResult, Action<string> setResponseText, Action<string> parseValues)
    {
        setResponseText(string.IsNullOrWhiteSpace(rawResult) ? "NO RESPONSE" : rawResult);
        if (rawResult.StartsWith("+ok="))
        {
            string cleanVal = rawResult.Replace("+ok=", "").Trim();
            parseValues(cleanVal);
        }
    }

    private async Task ExecuteCommandAsync(string fullCmd, Action<string> setResponse)
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            MessageBox.Show("Pilih COM Port terlebih dahulu.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        setResponse("Sending...");
        try
        {
            string result = await Task.Run(() => _deviceService.SendSingleAtCommandAsync(SelectedPort, fullCmd, AppendLog));
            setResponse(string.IsNullOrWhiteSpace(result) ? "NO RESPONSE" : result);
        }
        catch (Exception ex)
        {
            setResponse($"ERR: {ex.Message}");
            AppendLog($"[ERROR] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Action KIRIM (Set) ---
    [RelayCommand] private async Task SendManualAsync() => await ExecuteCommandAsync(ManualCmd, res => ManualResp = res);
    [RelayCommand] private async Task SendWmodeAsync() => await ExecuteCommandAsync($"AT+WMODE={SelectedWmode}", res => WmodeResp = res);
    [RelayCommand] private async Task SendWapAsync() => await ExecuteCommandAsync($"AT+WAP={SelectedWapMode},{WapSsid},{SelectedWapChannel}", res => WapResp = res);
    [RelayCommand] private async Task SendWakeyAsync() => await ExecuteCommandAsync($"AT+WAKEY={SelectedWakeyAuth},{SelectedWakeyEnc},{WakeyPass}", res => WakeyResp = res);
    [RelayCommand] private async Task SendNetpAsync() => await ExecuteCommandAsync($"AT+NETP={SelectedNetpProto},{SelectedNetpCs},{NetpPort},{NetpIp}", res => NetpResp = res);
    [RelayCommand] private async Task SendWebuAsync() => await ExecuteCommandAsync($"AT+WEBU={WebuUser},{WebuPass}", res => WebuResp = res);
    [RelayCommand] private async Task SendEventAsync() => await ExecuteCommandAsync($"AT+EVENT={SelectedEvent}", res => EventResp = res);
    [RelayCommand] private async Task SendRegenAsync() => await ExecuteCommandAsync($"AT+REGEN={SelectedRegen}", res => RegenResp = res);

    [RelayCommand]
    private async Task SendRegusrAsync()
    {
        if (RegusrData.Length < 10)
        {
            var result = MessageBox.Show(
                $"Panjang Data Heartbeat ({RegusrData.Length} karakter) kurang dari 10 karakter.\nYakin ingin mengirim?",
                "Peringatan Panjang Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.No)
                return;
        }

        await ExecuteCommandAsync($"AT+REGUSR={SelectedRegusrType},{RegusrData}", res => RegusrResp = res);
    }

    [RelayCommand] private async Task SendRegsndAsync() => await ExecuteCommandAsync($"AT+REGSND={SelectedRegsnd}", res => RegsndResp = res);
    [RelayCommand] private async Task SendTcptoAsync() => await ExecuteCommandAsync($"AT+TCPTO={TcptoTime}", res => TcptoResp = res);

    [RelayCommand]
    private async Task SendNetheartAsync()
    {
        if (HeartPayload.Length < 10)
        {
            var result = MessageBox.Show(
                $"Panjang Payload Heartbeat ({HeartPayload.Length} karakter) kurang dari 10 karakter.\nYakin ingin mengirim?",
                "Peringatan Panjang Payload",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.No)
                return;
        }

        await ExecuteCommandAsync($"AT+NETHEARTCFG={SelectedHeartSwitch},{HeartCycle},{SelectedHeartType},{HeartPayload}", res => HeartResp = res);
    }

    // Kirim Socket B
    [RelayCommand] private async Task SendSockbAsync() => await ExecuteCommandAsync($"AT+SOCKB={SelectedSockbProto},{SockbPort},{SockbIp}", res => SockbResp = res);
    [RelayCommand] private async Task SendTcpdisbAsync() => await ExecuteCommandAsync($"AT+TCPDISB={SelectedTcpdisbSta},{SelectedTcpdisbOpt}", res => TcpdisbResp = res);
    [RelayCommand] private async Task SendTcptobAsync() => await ExecuteCommandAsync($"AT+TCPTOB={TcptobTime}", res => TcptobResp = res);
    [RelayCommand] private async Task SendUdplcptAsync() => await ExecuteCommandAsync($"AT+UDPLCPT={UdplcptPortA},{UdplcptPortB}", res => UdplcptResp = res);

    // Kirim Perintah Lainnya
    [RelayCommand] private async Task SendWsssidAsync() => await ExecuteCommandAsync($"AT+WSSSID={WsssidSsid}", res => WsssidResp = res);
    [RelayCommand] private async Task SendLanAsync() => await ExecuteCommandAsync($"AT+LAN={LanIp}", res => LanResp = res);
    [RelayCommand] private async Task SendWifiAsync() => await ExecuteCommandAsync($"AT+WIFI={SelectedWifiSwitch}", res => WifiResp = res);

    [RelayCommand]
    private async Task SendReldAsync()
    {
        if (ReldPassword != "123smi321")
        {
            MessageBox.Show("Kata sandi RELD salah!", "Akses Ditolak", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (MessageBox.Show("Apakah Anda yakin ingin mengembalikan modul ke pengaturan PABRIK (Reset RELD)?",
            "Konfirmasi Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            await ExecuteCommandAsync("AT+RELD", res => ReldResp = res);
        }
    }

    [RelayCommand] private async Task SendRebootAsync() => await ExecuteCommandAsync("AT+Z", res => MessageBox.Show("Modul Restart...", "Info"));
}