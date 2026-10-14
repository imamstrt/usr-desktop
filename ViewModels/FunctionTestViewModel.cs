using System;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmiWifiProvisioning.Models;
using SmiWifiProvisioning.Services.Implementations;

namespace SmiWifiProvisioning.ViewModels;

public partial class FunctionTestViewModel : ObservableObject
{
    private readonly WifiService _wifiService = new();
    private readonly DlmsService _dlmsService = new();
    private readonly TestDbService _dbService = new();
    private readonly DispatcherTimer _wifiCheckTimer;

    [ObservableProperty]
    private string _barcodeInput = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Siap. Silakan Scan Barcode.";

    [ObservableProperty]
    private string _deviceResponseHex = string.Empty;

    [ObservableProperty]
    private string _testResultStatus = "PENDING"; // OK, FAILED, TESTING

    [ObservableProperty]
    private string _wifiStatusText = "Wi-Fi: Disconnected";

    [ObservableProperty]
    private bool _isWifiConnected;

    [ObservableProperty]
    private bool _isBusy;

    public FunctionTestViewModel()
    {
        _ = CheckWifiStatusAsync();

        // Polling Timer: Otomatis perbarui indikator Wi-Fi setiap 2 detik
        _wifiCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _wifiCheckTimer.Tick += async (s, e) => await CheckWifiStatusAsync();
        _wifiCheckTimer.Start();
    }

    [RelayCommand]
    public async Task CheckWifiStatusAsync()
    {
        string connectedSsid = _wifiService.GetConnectedSsid();
        if (!string.IsNullOrEmpty(connectedSsid))
        {
            IsWifiConnected = true;
            WifiStatusText = $"Wi-Fi Connected: {connectedSsid}";
        }
        else
        {
            IsWifiConnected = false;
            WifiStatusText = "Wi-Fi: Disconnected";
        }
        await Task.CompletedTask;
    }

    partial void OnBarcodeInputChanged(string value)
    {
        if (value != null && value.Trim().Length == 11 && !IsBusy)
        {
            _ = RunFunctionTestAsync(value.Trim());
        }
    }


    [RelayCommand]
    private async Task RunFunctionTestAsync(string ssidMeterId)
    {
        if (IsBusy) return;   // guard supaya tidak double-trigger

        IsBusy = true;
        TestResultStatus = "TESTING";

        StringBuilder traceLog = new StringBuilder();
        traceLog.AppendLine($"=== MULAI PENGUJIAN [{DateTime.Now:HH:mm:ss}] ===");
        traceLog.AppendLine($"Target SSID / Meter ID : {ssidMeterId}");
        DeviceResponseHex = traceLog.ToString();

        try
        {
            // -------------------------------------------------------------
            // STEP 1: KONEKSI WI-FI (WITH AUTO RETRY)
            // -------------------------------------------------------------
            bool wifiConnected = false;
            int maxWifiRetries = 3;

            for (int wifiAttempt = 1; wifiAttempt <= maxWifiRetries; wifiAttempt++)
            {
                StatusMessage = $"[1/4] Menghubungkan Wi-Fi '{ssidMeterId}' (Coba #{wifiAttempt})...";
                traceLog.AppendLine($"\n[STEP 1] Percobaan Wi-Fi #{wifiAttempt} ke SSID: {ssidMeterId}");

                wifiConnected = await _wifiService.ConnectToWifiAsync(ssidMeterId, ProductionConfig.ApPassword);
                await CheckWifiStatusAsync();

                if (wifiConnected)
                {
                    traceLog.AppendLine("✔ Wi-Fi Terhubung.");
                    break;
                }

                traceLog.AppendLine($"⚠️ Percobaan Wi-Fi #{wifiAttempt} gagal. Mencoba lagi...");
                await Task.Delay(1500);
            }

            if (!wifiConnected)
            {
                traceLog.AppendLine("❌ ERROR: Windows gagal terhubung ke AP Wi-Fi target setelah retries.");
                StatusMessage = "Gagal terhubung ke Wi-Fi Target!";
                TestResultStatus = "FAILED";

                bool isSaved = await _dbService.SaveTestRecordAsync(ssidMeterId, string.Empty, string.Empty,
                                                                    "FAILED", "Gagal Terhubung ke Wi-Fi Target");
                traceLog.AppendLine(isSaved ? "💾 DB: Record Gagal tersimpan."
                                            : "❌ DB: Gagal menyimpan ke database.");

                DeviceResponseHex = traceLog.ToString();
                return;   // ← keluar, finally akan bersihkan
            }

            // -------------------------------------------------------------
            // STEP 2: SETTLEMENT DELAY & PING
            // -------------------------------------------------------------
            StatusMessage = "[2/4] Menunggu stabilisasi alokasi IP DHCP...";
            traceLog.AppendLine("\n[STEP 2] Jeda stabilisasi jaringan (4 detik)...");
            await Task.Delay(4000);

            bool pingOk = await WaitForPingAsync("10.10.100.254", timeoutSeconds: 3);
            traceLog.AppendLine(pingOk ? "✔ Ping 10.10.100.254 OK."
                                       : "⚠️ WARNING: IP 10.10.100.254 tidak membalas Ping. Mencoba TCP Socket...");
            DeviceResponseHex = traceLog.ToString();

            // -------------------------------------------------------------
            // STEP 3: TRANSMISI TCP DLMS
            // -------------------------------------------------------------
            int targetPort = ProductionConfig.ServerPort;
            StatusMessage = $"[3/4] Mengirim Frame DLMS ke 10.10.100.254:{targetPort}...";
            traceLog.AppendLine($"\n[STEP 3] Membuka Socket TCP ke 10.10.100.254:{targetPort}");

            var (success, dlmsLog, extractedId) = await _dlmsService.ExecuteDlmsTestAsync(
                "10.10.100.254",
                targetPort,
                maxRetries: 3,
                onProgressLog: (logText) => StatusMessage = $"[3/4] {logText}...");

            traceLog.AppendLine(dlmsLog);
            DeviceResponseHex = traceLog.ToString();

            if (!success)
            {
                StatusMessage = "Komunikasi TCP / Frame DLMS Gagal!";
                TestResultStatus = "FAILED";

                bool isSaved = await _dbService.SaveTestRecordAsync(ssidMeterId, extractedId, ssidMeterId,
                                                                    "FAILED", "Komunikasi TCP / Frame DLMS Gagal");
                traceLog.AppendLine(isSaved ? "💾 DB: Record Gagal tersimpan."
                                            : "❌ DB: Gagal menyimpan ke database.");

                DeviceResponseHex = traceLog.ToString();
                return;
            }

            // -------------------------------------------------------------
            // STEP 4: VERIFIKASI & SIMPAN DB
            // -------------------------------------------------------------
            StatusMessage = "[4/4] Memverifikasi Hasil Baca Meter ID...";
            traceLog.AppendLine($"\n[STEP 4] Hasil Ekstraksi ID Meter: '{extractedId}'");
            traceLog.AppendLine($"Target Barcode ID         : '{ssidMeterId}'");

            string failureReason = "-";

            if (!string.IsNullOrEmpty(extractedId) && extractedId.Contains(ssidMeterId))
            {
                traceLog.AppendLine("✔ MATCH! Nomor Meter Cocok.");
                StatusMessage = $"TESTING SUCCESS! (Meter ID: {extractedId})";
                TestResultStatus = "OK";
            }
            else
            {
                traceLog.AppendLine("❌ MISMATCH! Nomor Meter tidak sesuai barcode.");
                StatusMessage = $"GAGAL! Terbaca: '{extractedId}' vs Barcode: '{ssidMeterId}'";
                TestResultStatus = "FAILED";
                failureReason = $"Nomor Meter Tidak Cocok (Terbaca: {extractedId})";
            }

            bool dbSaved = await _dbService.SaveTestRecordAsync(ssidMeterId, extractedId, ssidMeterId,
                                                                TestResultStatus, failureReason);

            if (dbSaved)
            {
                traceLog.AppendLine("💾 DB: Record hasil tes berhasil disimpan ke Database SQLite.");
                StatusMessage += " [Data Tersimpan]";
            }
            else
            {
                traceLog.AppendLine("❌ DB: Gagal menyimpan record ke Database SQLite.");
                StatusMessage += " [Gagal Simpan DB]";
            }

            DeviceResponseHex = traceLog.ToString();
        }
        finally
        {
            // ===== KUNCI: kosongkan barcode dulu, baru reset IsBusy =====
            // Urutan penting! Kalau IsBusy=false dulu, OnBarcodeInputChanged bisa re-trigger.
            BarcodeInput = string.Empty;
            IsBusy = false;   // ← perubahan ini memicu auto-focus di code-behind
        }
    }

    private async Task<bool> WaitForPingAsync(string ipAddress, int timeoutSeconds)
    {
        using var ping = new Ping();
        DateTime startTime = DateTime.Now;

        while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
        {
            try
            {
                var reply = await ping.SendPingAsync(ipAddress, 1000);
                if (reply.Status == IPStatus.Success)
                    return true;
            }
            catch { }
            await Task.Delay(300);
        }

        return false;
    }
}