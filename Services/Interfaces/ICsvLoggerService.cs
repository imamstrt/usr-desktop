namespace SmiWifiProvisioning.Services.Interfaces;

public interface ICsvLoggerService
{
    Task WriteLogAsync(string comPort, string meterId, string status, string errorMessage = "");
}