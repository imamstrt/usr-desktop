using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SmiWifiProvisioning.Services.Implementations;

public class DlmsService
{
    private readonly byte[] _snrmFrame = HexToBytes("7EA0070321930F017E");
    private readonly byte[] _aarqFrame = HexToBytes("7EA02B032110FBAFE6E600601DA109060760857405080101BE10040E01000000065F1F0400621E5DFFFF88557E");
    private readonly byte[] _getRequestObisMeter = HexToBytes("7EA0190321326FD8E6E600C001C1000100002A0000FF020012807E");

    public async Task<(bool success, string logTrace, string extractedMeterId)> ExecuteDlmsTestAsync(
        string targetIp,
        int port,
        int maxRetries = 3,
        Action<string>? onProgressLog = null)
    {
        StringBuilder logBuilder = new StringBuilder();

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                string attemptHeader = $"--- Percobaan TCP DLMS #{attempt} dari {maxRetries} ---";
                logBuilder.AppendLine(attemptHeader);
                onProgressLog?.Invoke(attemptHeader);

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(targetIp, port);

                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                {
                    logBuilder.AppendLine($"⚠️ Attempt {attempt}: Timeout membuka Socket TCP ke {targetIp}:{port}");
                    if (attempt < maxRetries) { await Task.Delay(1000); continue; }
                    return (false, logBuilder.ToString(), string.Empty);
                }

                using var stream = client.GetStream();
                stream.ReadTimeout = 3000;
                byte[] buffer = new byte[1024];

                // 1. Send SNRM
                logBuilder.AppendLine("TX (SNRM) : " + ConvertToHex(_snrmFrame, _snrmFrame.Length));
                await stream.WriteAsync(_snrmFrame, 0, _snrmFrame.Length);

                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 7)
                {
                    logBuilder.AppendLine($"⚠️ Attempt {attempt}: Modul tidak membalas SNRM.");
                    if (attempt < maxRetries) { await Task.Delay(1000); continue; }
                    return (false, logBuilder.ToString(), string.Empty);
                }
                logBuilder.AppendLine("RX (UA)   : " + ConvertToHex(buffer, bytesRead));

                // 2. Send AARQ
                logBuilder.AppendLine("TX (AARQ) : " + ConvertToHex(_aarqFrame, _aarqFrame.Length));
                await stream.WriteAsync(_aarqFrame, 0, _aarqFrame.Length);

                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 7)
                {
                    logBuilder.AppendLine($"⚠️ Attempt {attempt}: Modul tidak membalas AARQ.");
                    if (attempt < maxRetries) { await Task.Delay(1000); continue; }
                    return (false, logBuilder.ToString(), string.Empty);
                }
                logBuilder.AppendLine("RX (AARE) : " + ConvertToHex(buffer, bytesRead));

                // 3. Send GET Request
                logBuilder.AppendLine("TX (GET)  : " + ConvertToHex(_getRequestObisMeter, _getRequestObisMeter.Length));
                await stream.WriteAsync(_getRequestObisMeter, 0, _getRequestObisMeter.Length);

                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 7)
                {
                    logBuilder.AppendLine($"⚠️ Attempt {attempt}: Modul tidak membalas GET Request.");
                    if (attempt < maxRetries) { await Task.Delay(1000); continue; }
                    return (false, logBuilder.ToString(), string.Empty);
                }

                string getResponseHex = ConvertToHex(buffer, bytesRead);
                logBuilder.AppendLine("RX (DATA) : " + getResponseHex);

                string extractedId = ExtractMeterIdFromBuffer(buffer, bytesRead);
                return (true, logBuilder.ToString(), extractedId);
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"⚠️ Attempt {attempt} Exception: {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(1000);
                }
            }
        }

        return (false, logBuilder.ToString(), string.Empty);
    }

    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace(" ", "");
        byte[] raw = new byte[hex.Length / 2];
        for (int i = 0; i < raw.Length; i++)
            raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return raw;
    }

    private static string ConvertToHex(byte[] buffer, int length)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < length; i++)
            sb.AppendFormat("{0:X2} ", buffer[i]);
        return sb.ToString().Trim();
    }

    private static string ExtractMeterIdFromBuffer(byte[] buffer, int length)
    {
        try
        {
            for (int i = 0; i < length - 6; i++)
            {
                if (buffer[i] == 0xC4 && buffer[i + 1] == 0x01)
                {
                    if (buffer[i + 3] == 0x00 && buffer[i + 4] == 0x0A)
                    {
                        int stringLength = buffer[i + 5];
                        int dataStartIndex = i + 6;

                        if (dataStartIndex + stringLength <= length)
                        {
                            return Encoding.ASCII.GetString(buffer, dataStartIndex, stringLength).Trim();
                        }
                    }
                }
            }
        }
        catch { }

        return string.Empty;
    }
}