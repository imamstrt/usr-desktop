using SmiWifiProvisioning.Helpers;
using SmiWifiProvisioning.Models;
using SmiWifiProvisioning.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SmiWifiProvisioning.Services.Implementations;

/// <summary>
/// Layanan utama pengelola komunikasi serial UART / RS485 ke modul hardware USR-DR162 & USR-DR404.
/// </summary>
public class UsrDeviceService : IUsrDeviceService
{
    /// <summary>
    /// Mengeksekusi sekuens konfigurasi otomatis (provisioning) untuk lini produksi.
    /// </summary>
    public async Task ProvisionDeviceAsync(
        string comPortName,
        string meterId,
        ProvisioningConfig config,
        Action<string> log,
        CancellationToken ct = default)
    {
        log("=== MULAI PENGUJIAN ===");
        log($"COM          : {comPortName}");
        log($"Tipe Modul   : {config.DeviceType}");
        log($"Meter ID     : {meterId}");

        if (meterId.Length < 11)
        {
            log("[WARN] Peringatan: Panjang Meter ID kurang dari 11 karakter!");
        }

        int detectedBaud = await ScanBaudRateAsync(comPortName, log, ct);
        if (detectedBaud == 0)
            throw new Exception("Baudrate tidak ditemukan / Perangkat tidak merespon.");

        using var port = new SerialPort(comPortName, detectedBaud, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        log($"[OPEN] Port {comPortName} @ {detectedBaud} baud");
        port.Open();

        try
        {
            if (!await EnterAtModeAsync(port, log, ct))
                throw new Exception("Gagal masuk Mode AT setelah beberapa kali percobaan.");

            await SendAtCheckedAsync(port, "AT+VER", log, ct);

            // =========================================================================
            // PERCABANGAN SEKUENS BERDASARKAN TIPE MODUL
            // =========================================================================
            // Bagian method ProvisionDeviceAsync di UsrDeviceService.cs:

            if (config.DeviceType == UsrDeviceType.DR162)
            {
                // Sekuens Perintah AT USR-DR162
                await SendAtCheckedAsync(port, "AT+WMODE=AP", log, ct);
                await SendAtCheckedAsync(port, $"AT+WAP=11BGN,{meterId},AUTO", log, ct);
                await SendAtCheckedAsync(port, $"AT+WAKEY=WPA2PSK,AES,{config.ApPassword}", log, ct);
                await SendAtCheckedAsync(port, $"AT+NETP=TCP,SERVER,{config.ServerPort},{ProductionConfig.ServerIp}", log, ct);
                await SendAtCheckedAsync(port, $"AT+WEBU={meterId},{config.WebPassword}", log, ct);
                await SendAtCheckedAsync(port, "AT+EVENT=off", log, ct);

                string formattedMeterId = meterId.Length % 2 != 0 ? meterId + "0" : meterId;

                await SendAtCheckedAsync(port, "AT+REGEN=USR", log, ct);
                await SendAtCheckedAsync(port, $"AT+REGUSR=HEX,{formattedMeterId}", log, ct);
                await SendAtCheckedAsync(port, "AT+REGSND=FIRST", log, ct);
                await SendAtCheckedAsync(port, $"AT+NETHEARTCFG=ON,900,HEX,{formattedMeterId}", log, ct);
                await SendAtCheckedAsync(port, $"AT+UART={config.TargetBaudRate},{config.DataBits},{config.StopBits},{config.Parity},{ProductionConfig.FlowControl}", log, ct);
                
            }
            else if (config.DeviceType == UsrDeviceType.DR404)
            {
                // meterId di sini adalah MAC Address 12 digit (contoh: D4AD20964C68)
                string macAddress = meterId;
                string hexMacPayload = QrParser.StringToHex(macAddress);

                log($"[INFO] MAC Address DR404 : {macAddress}");
                log($"[INFO] Payload HEX MAC   : {hexMacPayload}");

                // 1. Inisialisasi Mode Awal & Aktifkan FAPSTA
                await SendAtCheckedAsync(port, "AT+WMODE=AP", log, ct);
                await SendAtCheckedAsync(port, "AT+FAPSTA=on", log, ct);

                // 2. Reload Default & Reboot Modul
                log("[INFO] Mengirim AT+RELD (Reload factory & reboot)...");
                try
                {
                    await SendAtCheckedAsync(port, "AT+RELD", log, ct);
                }
                catch
                {
                    // Modul terkadang langsung restart sebelum selesai merespon +ok
                    log("[INFO] Modul mereset dan memutus serial sementara...");
                }

                // Tutup port sementara
                port.Close();
                log("[INFO] Menunggu modul reboot awal (5 detik)...");
                await Task.Delay(5000, ct);

                // 3. Retry Loop: Scan baudrate ulang & Masuk ke Mode AT
                bool isReconnected = false;
                int maxReconnectAttempts = 4; // Coba 3 kali

                for (int attempt = 1; attempt <= maxReconnectAttempts; attempt++)
                {
                    log($"[INFO] Percobaan koneksi kembali ke-{attempt} setelah RELD...");

                    detectedBaud = await ScanBaudRateAsync(comPortName, log, ct);
                    if (detectedBaud > 0)
                    {
                        port.BaudRate = detectedBaud;
                        port.Open();
                        log($"[OPEN] Port {comPortName} tersambung kembali @ {detectedBaud} baud");

                        // Coba masuk AT mode
                        if (await EnterAtModeAsync(port, log, ct))
                        {
                            isReconnected = true;
                            break; // Berhasil, keluar dari loop retry
                        }
                        else
                        {
                            // Gagal masuk AT mode, tutup port, coba lagi
                            port.Close();
                        }
                    }

                    if (attempt < maxReconnectAttempts)
                    {
                        log($"[WARN] Percobaan {attempt} gagal. Menunggu 3 detik sebelum coba lagi...");
                        await Task.Delay(3000, ct); // Jeda sebelum retry berikutnya
                    }
                }

                if (!isReconnected)
                {
                    throw new Exception("Gagal terhubung dan masuk Mode AT setelah reboot AT+RELD. Batas retry tercapai.");
                }

                // 4. Sekuens Parameter DR404 (Lanjutan)
                await SendAtCheckedAsync(port, $"AT+WAP=11BGN,{macAddress},AUTO", log, ct);
                await SendAtCheckedAsync(port, $"AT+WAKEY=WPA2PSK,AES,{config.ApPassword}", log, ct);

                // DR404 Command Socket TCP (Penyesuaian jika DR404 tidak menggunakan AT+NETP)
                // Jika DR404 di firmware Anda tetap mendukung AT+NETP, gunakan baris ini:
                await SendAtCheckedAsync(port, $"AT+NETP=TCP,SERVER,{config.ServerPort},{ProductionConfig.ServerIp}", log, ct);

                // AT+WEBU menggunakan MAC Address 12 digit
                await SendAtCheckedAsync(port, $"AT+WEBU={macAddress},{config.WebPassword}", log, ct);

                // Registrasi Paket DR404
                await SendAtCheckedAsync(port, "AT+REGEN=usr", log, ct);
                await SendAtCheckedAsync(port, $"AT+REGUSR={hexMacPayload}", log, ct);
                await SendAtCheckedAsync(port, "AT+REGTCP=first", log, ct);

                // Heartbeat Package DR404
                await SendAtCheckedAsync(port, "AT+HEARTEN=on", log, ct);
                await SendAtCheckedAsync(port, "AT+HEARTTP=NET", log, ct);
                await SendAtCheckedAsync(port, $"AT+HEARTDT={hexMacPayload}", log, ct);
                await SendAtCheckedAsync(port, "AT+HEARTTM=900", log, ct);

                // Setting UART
                await SendAtCheckedAsync(port, $"AT+UART={config.TargetBaudRate},{config.DataBits},{config.StopBits},{config.Parity},{ProductionConfig.FlowControl}", log, ct);

                // 5. Pengalihan Mode Akhir
                await SendAtCheckedAsync(port, "AT+WMODE=STA", log, ct);
                await SendAtCheckedAsync(port, "AT+STTC=off", log, ct);
            }

            // Reboot Modul
            await SendAtCheckedAsync(port, "AT+Z", log, ct);
            log("=== SUCCESS ===");

            // Reboot Modul
            
        }
        finally
        {
            await ExitAtModeAsync(port, log, ct);
        }
    }

    /// <summary>
    /// Membaca MAC Address fisik dari modul DR404 via AT+WSMAC.
    /// </summary>
    private async Task<string> FetchMacAddressAsync(SerialPort port, Action<string> log, CancellationToken ct)
    {
        log("[TX CMD] AT+WSMAC");
        port.DiscardInBuffer();
        byte[] data = Encoding.ASCII.GetBytes("AT+WSMAC\r\n");
        port.Write(data, 0, data.Length);

        await Task.Delay(ProductionConfig.AtDelayMs, ct);
        string response = port.ReadExisting().Trim();
        log($"[RX CMD] {response}");

        // Ekstrak karakter Hex murni dari respon MAC
        string cleanedMac = Regex.Replace(response, "[^a-fA-F0-9]", "");

        if (cleanedMac.Length >= 12)
        {
            return cleanedMac.Substring(cleanedMac.Length - 12);
        }

        throw new Exception($"Gagal membaca MAC Address dari modul DR404. Respon: {response}");
    }

    /// <summary>
    /// Memindai seluruh Access Point Wi-Fi di sekitar modul USR.
    /// </summary>
    public async Task<List<WifiNetwork>> ScanWifiAsync(string comPortName, Action<string> log, CancellationToken ct = default)
    {
        int baud = await ScanBaudRateAsync(comPortName, log, ct);
        if (baud == 0) throw new Exception("Baudrate tidak terdeteksi");

        using var port = new SerialPort(comPortName, baud, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        port.Open();
        try
        {
            if (!await EnterAtModeAsync(port, log, ct))
                throw new Exception("Gagal masuk Mode AT");

            for (int i = 0; i < 3; i++)
            {
                log($"[WSCAN] Percobaan {i + 1}");
                log("[TX CMD] AT+WSCAN");

                port.DiscardInBuffer();
                byte[] cmd = Encoding.ASCII.GetBytes("AT+WSCAN\r\n");
                port.Write(cmd, 0, cmd.Length);

                StringBuilder buf = new();
                DateTime start = DateTime.Now;

                while ((DateTime.Now - start).TotalSeconds < 15)
                {
                    ct.ThrowIfCancellationRequested();
                    if (port.BytesToRead > 0)
                    {
                        string txt = port.ReadExisting();
                        buf.Append(txt);
                        log(txt.TrimEnd());
                        if (buf.ToString().Trim().EndsWith("+ok"))
                            break;
                    }
                    await Task.Delay(100, ct);
                }

                string resultStr = buf.ToString();
                if (resultStr.Contains("Ch,SSID") || resultStr.Contains("SSID"))
                {
                    return ParseWscanResult(resultStr);
                }
                await Task.Delay(1000, ct);
            }

            throw new Exception("Pemindaian Wi-Fi gagal / tidak ada respon");
        }
        finally
        {
            await ExitAtModeAsync(port, log, ct);
        }
    }

    /// <summary>
    /// Mengkonfigurasi modul USR agar terhubung ke Access Point Wi-Fi target.
    /// </summary>
    public async Task ConnectWifiAsync(string comPortName, string ssid, string password, Action<string> log, Action<string> statusCallback, CancellationToken ct = default)
    {
        statusCallback("Pemindaian baudrate...");
        int baud = await ScanBaudRateAsync(comPortName, log, ct);
        if (baud == 0) throw new Exception("Baudrate tidak terdeteksi");

        using var port = new SerialPort(comPortName, baud, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        port.Open();
        try
        {
            statusCallback("Menghubungkan Wi-Fi...");
            if (!await EnterAtModeAsync(port, log, ct))
                throw new Exception("Gagal masuk Mode AT");

            await SendAtCheckedAsync(port, $"AT+WSSSID={ssid}", log, ct);
            await SendAtCheckedAsync(port, $"AT+WSKEY=WPA2PSK,AES,{password}", log, ct);
            await SendAtCheckedAsync(port, $"AT+NETP=TCP,CLIENT,{ProductionConfig.ServerPort},{ProductionConfig.ServerIp}", log, ct);
            await SendAtCheckedAsync(port, "AT+WMODE=APSTA", log, ct);
            await SendAtCheckedAsync(port, "AT+Z", log, ct);

            statusCallback("Perangkat sedang restart...");
            await Task.Delay(8000, ct);
        }
        finally
        {
            await ExitAtModeAsync(port, log, ct);
        }
    }

    /// <summary>
    /// Memeriksa alokasi IP DHCP / WAN pada modul USR.
    /// </summary>
    public async Task<string> CheckDhcpAsync(string comPortName, Action<string> log, CancellationToken ct = default)
    {
        int baud = await ScanBaudRateAsync(comPortName, log, ct);
        if (baud == 0) throw new Exception("Baudrate tidak terdeteksi");

        using var port = new SerialPort(comPortName, baud, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        port.Open();
        try
        {
            if (!await EnterAtModeAsync(port, log, ct))
                throw new Exception("Gagal masuk Mode AT");

            port.DiscardInBuffer();
            byte[] cmd = Encoding.ASCII.GetBytes("AT+WANN\r\n");
            port.Write(cmd, 0, cmd.Length);
            await Task.Delay(600, ct);

            string response = port.ReadExisting().Trim();
            log(response);
            return response;
        }
        finally
        {
            await ExitAtModeAsync(port, log, ct);
        }
    }

    /// <summary>
    /// Mengeksekusi sekelompok (batch) perintah AT sekaligus dalam 1 sesi Mode AT untuk performa tinggi.
    /// </summary>
    public async Task<Dictionary<string, string>> SendBatchAtCommandsAsync(string comPortName, List<string> commands, Action<string> log, CancellationToken ct = default)
    {
        var results = new Dictionary<string, string>();
        int baud = await ScanBaudRateAsync(comPortName, log, ct);
        if (baud == 0) throw new Exception("Baudrate tidak terdeteksi");

        using var port = new SerialPort(comPortName, baud, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        port.Open();
        try
        {
            if (!await EnterAtModeAsync(port, log, ct))
                throw new Exception("Gagal masuk Mode AT");

            foreach (var cmd in commands)
            {
                ct.ThrowIfCancellationRequested();
                port.DiscardInBuffer();
                log($"[TX BATCH] {cmd}");

                byte[] data = Encoding.ASCII.GetBytes($"{cmd}\r\n");
                port.Write(data, 0, data.Length);

                await Task.Delay(250, ct);
                string response = port.ReadExisting().Trim();
                log($"[RX BATCH] {response}");

                results[cmd] = response;
            }
        }
        finally
        {
            await ExitAtModeAsync(port, log, ct);
        }

        return results;
    }

    /// <summary>
    /// Mengeksekusi 1 perintah AT tunggal untuk pengujian individual di halaman Admin.
    /// </summary>
    public async Task<string> SendSingleAtCommandAsync(string comPortName, string fullAtCmd, Action<string> log, CancellationToken ct = default)
    {
        int baud = await ScanBaudRateAsync(comPortName, log, ct);
        if (baud == 0) throw new Exception("Baudrate tidak terdeteksi");

        using var port = new SerialPort(comPortName, baud, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        port.Open();
        try
        {
            if (!await EnterAtModeAsync(port, log, ct))
                throw new Exception("Gagal masuk Mode AT");

            port.DiscardInBuffer();
            log($"[TX CMD] {fullAtCmd}");
            byte[] data = Encoding.ASCII.GetBytes($"{fullAtCmd}\r\n");
            port.Write(data, 0, data.Length);

            await Task.Delay(400, ct);
            string response = port.ReadExisting().Trim();
            log($"[RX CMD] {response}");
            return response;
        }
        finally
        {
            await ExitAtModeAsync(port, log, ct);
        }
    }

    // =========================================================================
    // PRIVATE HELPER FUNCTIONS (INTERNAL LOGIC)
    // =========================================================================

    private List<WifiNetwork> ParseWscanResult(string resp)
    {
        List<WifiNetwork> list = new();
        string[] lines = resp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("+ok") || line.StartsWith("Ch,"))
                continue;

            string[] parts = line.Split(',');
            if (parts.Length < 5) continue;

            try
            {
                string ssid = parts[1].Trim();
                string security = parts[3].Trim();
                _ = int.TryParse(parts[4].Trim(), out int rssi);
                bool isOpen = security.ToUpper().Contains("OPEN");

                if (!isOpen)
                {
                    list.Add(new WifiNetwork
                    {
                        Ssid = ssid,
                        Security = security,
                        Rssi = rssi,
                        IsOpen = isOpen
                    });
                }
            }
            catch { }
        }

        return list.OrderByDescending(n => n.Rssi).ToList();
    }

    private async Task ExitAtModeAsync(SerialPort port, Action<string> log, CancellationToken ct = default)
    {
        try
        {
            if (port.IsOpen)
            {
                log("[TX CMD] AT+ENTM");
                port.DiscardInBuffer();

                byte[] entm = Encoding.ASCII.GetBytes("AT+ENTM\r\n");
                port.Write(entm, 0, entm.Length);

                await Task.Delay(300, ct);

                string resp = port.ReadExisting().Trim();
                if (!string.IsNullOrWhiteSpace(resp))
                {
                    log($"[RX CMD] {resp}");
                }
            }
        }
        catch { }
        finally
        {
            if (port.IsOpen)
            {
                port.Close();
                log("[CLOSE] Tutup port");
            }
        }
    }

    private async Task<int> ScanBaudRateAsync(string portName, Action<string> log, CancellationToken ct)
    {
        int[] baudRates = { 300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };

        foreach (int baud in baudRates)
        {
            ct.ThrowIfCancellationRequested();
            log($"[SCAN] Testing baudrate {baud}...");
            try
            {
                using var testPort = new SerialPort(portName, baud, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };
                testPort.Open();
                testPort.DiscardInBuffer();

                byte[] bytes = Encoding.ASCII.GetBytes("+++");
                testPort.Write(bytes, 0, bytes.Length);
                await Task.Delay(400, ct);

                string resp = testPort.ReadExisting();
                log($"[RX@{baud}] '{resp.Trim()}'");

                if (resp.Contains("a") || resp.Contains("+ok"))
                {
                    log($"[OK] Baudrate cocok: {baud}");
                    return baud;
                }

                byte[] atBytes = Encoding.ASCII.GetBytes("AT\r\n");
                testPort.Write(atBytes, 0, atBytes.Length);
                await Task.Delay(300, ct);
                string atResp = testPort.ReadExisting();

                if (atResp.ToLower().Contains("+ok"))
                {
                    log($"[OK] Perangkat sudah dalam Mode AT @ {baud}");
                    return baud;
                }
            }
            catch { }
        }
        return 0;
    }

    private async Task<bool> EnterAtModeAsync(SerialPort port, Action<string> log, CancellationToken ct)
    {
        int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            log($"[INFO] Mencoba masuk AT Mode (percobaan {attempt})");

            port.DiscardInBuffer();
            port.DiscardOutBuffer();

            await Task.Delay(400, ct);

            log("[TX RAW] +++");
            byte[] bytes = Encoding.ASCII.GetBytes("+++");
            port.Write(bytes, 0, bytes.Length);

            await Task.Delay(600, ct);

            string response = port.ReadExisting();
            log($"[RX] '{response.Trim()}'");

            if (response.Contains("a"))
            {
                log("[TX RAW] a");
                byte[] aByte = Encoding.ASCII.GetBytes("a");
                port.Write(aByte, 0, aByte.Length);

                await Task.Delay(600, ct);
                string okResponse = port.ReadExisting();
                log($"[RX2] '{okResponse.Trim()}'");

                if (okResponse.Contains("+ok"))
                {
                    log("[OK] Masuk AT mode");
                    return true;
                }
            }
            else if (response.ToLower().Contains("+ok"))
            {
                log("[OK] Perangkat sudah dalam AT mode");
                return true;
            }
            else
            {
                log($"[WARN] Tidak ada respon 'a' pada percobaan {attempt}.");
            }

            await Task.Delay(500, ct);
        }

        return false;
    }

    private async Task SendAtCheckedAsync(SerialPort port, string cmd, Action<string> log, CancellationToken ct)
    {
        for (int i = 0; i <= ProductionConfig.AtRetryLimit; i++)
        {
            ct.ThrowIfCancellationRequested();
            log($"[AT] {cmd} (try {i + 1})");

            try
            {
                port.DiscardInBuffer();
                log($"[TX CMD] {cmd}");
                byte[] data = Encoding.ASCII.GetBytes($"{cmd}\r\n");
                port.Write(data, 0, data.Length);

                await Task.Delay(ProductionConfig.AtDelayMs, ct);
                string response = port.ReadExisting().Trim();
                log($"[RX CMD] {response}");

                if (response.ToLower().Contains("+ok"))
                {
                    log($"[OK] {cmd}");
                    return;
                }
                if (response.ToLower().Contains("+err"))
                {
                    log($"[WARN] Response Error: {response}");
                }
            }
            catch (Exception ex)
            {
                log($"[WARN] {ex.Message}");
            }
        }

        throw new Exception($"Perintah AT Gagal setelah retry: {cmd}");
    }
}