using System;
using System.Text;
using System.Text.RegularExpressions;

namespace SmiWifiProvisioning.Helpers;

public static class QrParser
{
    /// <summary>
    /// Mengekstrak nilai MAC Address (12 digit HEX) dari teks QR Code.
    /// Contoh: "MAC:D4AD20964C68,SN:02200324081600026164" -> "D4AD20964C68"
    /// </summary>
    public static string ExtractMacAddress(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            return string.Empty;

        // 1. Cari pola "MAC:" diikuti 12 karakter Hex
        var match = Regex.Match(rawInput, @"MAC:([a-fA-F0-9]{12})", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim().ToUpper();
        }

        // 2. Fallback: Jika di-scan MAC murni tanpa prefix "MAC:"
        string cleaned = Regex.Replace(rawInput, "[^a-fA-F0-9]", "");
        if (cleaned.Length >= 12)
        {
            return cleaned.Substring(0, 12).ToUpper();
        }

        return rawInput.Trim().ToUpper();
    }

    /// <summary>
    /// Mengonversi string ASCII MAC Address ke Format HEX murni untuk payload AT+REGUSR & AT+HEARTDT.
    /// Contoh: "D4AD20964C68" -> "443441443230393634433638"
    /// </summary>
    public static string StringToHex(string asciiString)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(asciiString);
        return Convert.ToHexString(bytes);
    }
}