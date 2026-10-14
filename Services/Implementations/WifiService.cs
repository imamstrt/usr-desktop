using System;
using System.Linq;
using System.Threading.Tasks;
using ManagedNativeWifi;

namespace SmiWifiProvisioning.Services.Implementations;

public class WifiService
{
    public string GetConnectedSsid()
    {
        try
        {
            var connectedNetwork = NativeWifi.EnumerateConnectedNetworkSsids().FirstOrDefault();
            return connectedNetwork != null ? connectedNetwork.ToString() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<bool> ConnectToWifiAsync(string ssid, string password, int timeoutSeconds = 15)
    {
        return await Task.Run(async () =>
        {
            try
            {
                var wifiInterface = NativeWifi.EnumerateInterfaces().FirstOrDefault();

                if (wifiInterface == null)
                    return false;

                NativeWifi.SetProfile(
                    wifiInterface.Id,
                    ProfileType.AllUser,
                    GetWpa2PskProfileXml(ssid, password),
                    null,
                    true);

                var connected = await NativeWifi.ConnectNetworkAsync(
                    wifiInterface.Id,
                    ssid,
                    BssType.Infrastructure,
                    TimeSpan.FromSeconds(timeoutSeconds));

                return connected;
            }
            catch
            {
                return false;
            }
        });
    }

    private string GetWpa2PskProfileXml(string ssid, string password)
    {
        return $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{ssid}</name>
    <SSIDConfig>
        <SSID>
            <name>{ssid}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>auto</connectionMode>
    <MSM>
        <security>
            <authEncryption>
                <authentication>WPA2PSK</authentication>
                <encryption>AES</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{password}</keyMaterial>
            </sharedKey>
        </security>
    </MSM>
</WLANProfile>";
    }
}