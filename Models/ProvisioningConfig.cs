namespace SmiWifiProvisioning.Models;

public class ProvisioningConfig
{
    public UsrDeviceType DeviceType { get; set; } = UsrDeviceType.DR162;
    public string ApPassword { get; set; } = ProductionConfig.ApPassword;
    public int ServerPort { get; set; } = ProductionConfig.ServerPort;
    public string WebPassword { get; set; } = ProductionConfig.WebPassword;
    public int TargetBaudRate { get; set; } = ProductionConfig.TargetBaudRate;
    public int DataBits { get; set; } = ProductionConfig.DataBits;
    public int StopBits { get; set; } = ProductionConfig.StopBits;
    public string Parity { get; set; } = ProductionConfig.Parity;
}