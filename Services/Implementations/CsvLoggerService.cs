using System.IO;
using System.Text;
using SmiWifiProvisioning.Models;
using SmiWifiProvisioning.Services.Interfaces;

namespace SmiWifiProvisioning.Services.Implementations;

public class CsvLoggerService : ICsvLoggerService
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task WriteLogAsync(string comPort, string meterId, string status, string errorMessage = "")
    {
        await _semaphore.WaitAsync();
        try
        {
            Directory.CreateDirectory(ProductionConfig.LogDir);
            string fileName = Path.Combine(ProductionConfig.LogDir, $"production_log_{DateTime.Now:yyyyMMdd}.csv");
            bool isNewFile = !File.Exists(fileName);

            using var writer = new StreamWriter(fileName, append: true, encoding: Encoding.UTF8);

            if (isNewFile)
            {
                await writer.WriteLineAsync("Timestamp,COM Port,Meter ID,Status,Error");
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string safeError = errorMessage.Replace(",", ";").Replace("\r\n", " ");
            await writer.WriteLineAsync($"{timestamp},{comPort},{meterId},{status},{safeError}");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}