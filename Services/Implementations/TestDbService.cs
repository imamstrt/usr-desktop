using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SmiWifiProvisioning.Services.Implementations;

public class TestDbService
{
    private readonly string _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "function_test.db");

    public TestDbService()
    {
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS FunctionTestLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TestTimestamp TEXT NOT NULL,
                ScannedBarcode TEXT NOT NULL,
                ExtractedMeterId TEXT,
                ConnectedSsid TEXT,
                TestStatus TEXT NOT NULL,
                FailureReason TEXT
            );";
        command.ExecuteNonQuery();
    }

    public async Task<bool> SaveTestRecordAsync(
        string scannedBarcode,
        string extractedMeterId = "",
        string connectedSsid = "",
        string testStatus = "PENDING",
        string failureReason = "")
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO FunctionTestLogs (TestTimestamp, ScannedBarcode, ExtractedMeterId, ConnectedSsid, TestStatus, FailureReason)
                VALUES ($timestamp, $barcode, $extracted, $ssid, $status, $reason);";

            command.Parameters.AddWithValue("$timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("$barcode", scannedBarcode);
            command.Parameters.AddWithValue("$extracted", extractedMeterId ?? string.Empty);
            command.Parameters.AddWithValue("$ssid", connectedSsid ?? string.Empty);
            command.Parameters.AddWithValue("$status", testStatus);
            command.Parameters.AddWithValue("$reason", failureReason ?? string.Empty);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch
        {
            return false;
        }
    }
}