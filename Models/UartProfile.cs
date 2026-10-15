using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmiWifiProvisioning.Models
{
    public record UartProfile(int BaudRate, int DataBits, Parity Parity, StopBits StopBits);
}
