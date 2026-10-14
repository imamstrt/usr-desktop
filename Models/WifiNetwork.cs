namespace SmiWifiProvisioning.Models;

public class WifiNetwork
{
    public string Ssid { get; set; } = string.Empty;
    public string Security { get; set; } = string.Empty;
    public int Rssi { get; set; }
    public bool IsOpen { get; set; }

    // Indikator visual sinyal (batang █)
    public string SignalBar
    {
        get
        {
            int absRssi = Math.Abs(Rssi);
            int count = Math.Clamp(absRssi / 20, 1, 5);
            return new string('█', count);
        }
    }
}