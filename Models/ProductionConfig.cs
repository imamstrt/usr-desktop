namespace SmiWifiProvisioning.Models;

public static class ProductionConfig
{
    public const string ServerIp = "smarttoken.id";
    public const int ServerPort = 5001;
    public const string ApPassword = "SMI@123321";
    public const string WebPassword = "SMI@123321";

    public const int TargetBaudRate = 19200;
    public const int DataBits = 8;
    public const int StopBits = 1; // 1 bit
    public const string Parity = "None";
    public const string FlowControl = "NFC";

    public const int AtRetryLimit = 2;
    public const int AtDelayMs = 600;
    public const string LogDir = "logs";

    public static readonly int[] StandardBaudRates = { 9600, 19200, 38400, 57600, 115200 };
}