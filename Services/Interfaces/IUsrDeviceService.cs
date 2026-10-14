using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmiWifiProvisioning.Models;

namespace SmiWifiProvisioning.Services.Interfaces;

public interface IUsrDeviceService
{
    // Fitur Provisioning Lini Produksi (dengan Parameter Manual Config)
    Task ProvisionDeviceAsync(
        string comPort,
        string meterId,
        ProvisioningConfig config,
        Action<string> logCallback,
        CancellationToken ct = default);

    // Fitur Config USR
    Task<List<WifiNetwork>> ScanWifiAsync(string comPortName, Action<string> log, CancellationToken ct = default);
    Task ConnectWifiAsync(string comPortName, string ssid, string password, Action<string> log, Action<string> statusCallback, CancellationToken ct = default);
    Task<string> CheckDhcpAsync(string comPortName, Action<string> log, CancellationToken ct = default);
    Task<Dictionary<string, string>> SendBatchAtCommandsAsync(string comPortName, List<string> commands, Action<string> log, CancellationToken ct = default);
    Task<string> SendSingleAtCommandAsync(string comPortName, string fullAtCmd, Action<string> log, CancellationToken ct = default);
}