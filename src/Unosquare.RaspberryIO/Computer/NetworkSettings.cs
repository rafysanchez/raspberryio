﻿namespace Unosquare.RaspberryIO.Computer
{
    using Swan;
    using Swan.Abstractions;
    using Swan.Components;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the network information.
    /// </summary>
    public class NetworkSettings : SingletonBase<NetworkSettings>
    {
        private const string EssidTag = "ESSID:";

        /// <summary>
        /// Gets the local machine Host Name.
        /// </summary>
        public string HostName => Network.HostName;

        /// <summary>
        /// Retrieves the wireless networks.
        /// </summary>
        /// <param name="adapter">The adapter.</param>
        /// <returns>A list of WiFi networks.</returns>
        public Task<List<WirelessNetworkInfo>> RetrieveWirelessNetworks(string adapter) => RetrieveWirelessNetworks(new[] { adapter });

        /// <summary>
        /// Retrieves the wireless networks.
        /// </summary>
        /// <param name="adapters">The adapters.</param>
        /// <returns>A list of WiFi networks.</returns>
        public async Task<List<WirelessNetworkInfo>> RetrieveWirelessNetworks(string[] adapters = null)
        {
            var result = new List<WirelessNetworkInfo>();

            foreach (var networkAdapter in adapters ?? (await RetrieveAdapters()).Where(x => x.IsWireless).Select(x => x.Name))
            {
                var wirelessOutput = await ProcessRunner.GetProcessOutputAsync("iwlist", $"{networkAdapter} scanning").ConfigureAwait(false);
                var outputLines =
                    wirelessOutput.Split('\n')
                        .Select(x => x.Trim())
                        .Where(x => string.IsNullOrWhiteSpace(x) == false)
                        .ToArray();

                for (var i = 0; i < outputLines.Length; i++)
                {
                    var line = outputLines[i];

                    if (line.StartsWith(EssidTag) == false) continue;

                    var network = new WirelessNetworkInfo
                    {
                        Name = line.Replace(EssidTag, string.Empty).Replace("\"", string.Empty)
                    };

                    while (true)
                    {
                        if (i + 1 >= outputLines.Length) break;

                        // should look for two lines before the ESSID acording to the scan
                        line = outputLines[i - 2];

                        if (!line.StartsWith("Quality=")) continue;
                        network.Quality = line.Replace("Quality=", string.Empty);
                        break;
                    }

                    while (true)
                    {
                        if (i + 1 >= outputLines.Length) break;

                        // should look for a line before the ESSID  acording to the scan
                        line = outputLines[i - 1];

                        if (!line.StartsWith("Encryption key:")) continue;
                        network.IsEncrypted = line.Replace("Encryption key:", string.Empty).Trim() == "on";
                        break;
                    }

                    if (result.Any(x => x.Name == network.Name) == false)
                        result.Add(network);
                }
            }

            return result
                .OrderBy(x => x.Name)
                .ToList();
        }

        /// <summary>
        /// Setups the wireless network.
        /// </summary>
        /// <param name="adapterName">Name of the adapter.</param>
        /// <param name="networkSsid">The network ssid.</param>
        /// <param name="password">The password.</param>
        /// <param name="countryCode">The 2-letter country code in uppercase. Default is US.</param>
        /// <returns>True if successful. Otherwise, false.</returns>
        public async Task<bool> SetupWirelessNetwork(string adapterName, string networkSsid, string password = null, string countryCode = "US")
        {
            // TODO: Get the country where the device is located to set 'country' param in payload var
            var payload = $"country={countryCode}\nctrl_interface=DIR=/var/run/wpa_supplicant GROUP=netdev\nupdate_config=1\n";
            payload += string.IsNullOrEmpty(password)
                ? $"network={{\n\tssid=\"{networkSsid}\"\n\tkey_mgmt=NONE\n\t}}\n"
                : $"network={{\n\tssid=\"{networkSsid}\"\n\tpsk=\"{password}\"\n\t}}\n";
            try
            {
                File.WriteAllText("/etc/wpa_supplicant/wpa_supplicant.conf", payload);
                await ProcessRunner.GetProcessOutputAsync("pkill", "-f wpa_supplicant");
                await ProcessRunner.GetProcessOutputAsync("ifdown", adapterName);
                await ProcessRunner.GetProcessOutputAsync("ifup", adapterName);
            }
            catch (Exception ex)
            {
                ex.Log(nameof(NetworkSettings));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retrieves the network adapters.
        /// </summary>
        /// <returns>A list of network adapters.</returns>
        public async Task<List<NetworkAdapterInfo>> RetrieveAdapters()
        {
            const string hWaddr = "HWaddr ";
            const string ether = "ether ";

            var result = new List<NetworkAdapterInfo>();
            var interfacesOutput = await ProcessRunner.GetProcessOutputAsync("ifconfig");
            var wlanOutput = (await ProcessRunner.GetProcessOutputAsync("iwconfig"))
                .Split('\n')
                    .Where(x => x.Contains("no wireless extensions.") == false)
                    .ToArray();

            var outputLines = interfacesOutput.Split('\n').Where(x => string.IsNullOrWhiteSpace(x) == false).ToArray();

            for (var i = 0; i < outputLines.Length; i++)
            {
                // grab the current line
                var line = outputLines[i];

                // skip if the line is indented
                if (char.IsLetterOrDigit(line[0]) == false)
                    continue;

                // Read the line as an adapter
                var adapter = new NetworkAdapterInfo
                {
                    Name = line.Substring(0, line.IndexOf(' ')).TrimEnd(':')
                };

                // Parse the MAC address in old version of ifconfig; it comes in the first line
                if (line.IndexOf(hWaddr, StringComparison.Ordinal) >= 0)
                {
                    var startIndexHwd = line.IndexOf(hWaddr, StringComparison.Ordinal) + hWaddr.Length;
                    adapter.MacAddress = line.Substring(startIndexHwd, 17).Trim();
                }

                // Parse the info in lines other than the first
                for (var j = i + 1; j < outputLines.Length; j++)
                {
                    // Get the contents of the indented line
                    var indentedLine = outputLines[j];

                    // We have hit the next adapter info
                    if (char.IsLetterOrDigit(indentedLine[0]))
                    {
                        i = j - 1;
                        break;
                    }

                    // Parse the MAC address in new versions of ifconfig; it no longer comes in the first line
                    if (indentedLine.IndexOf(ether, StringComparison.Ordinal) >= 0 && string.IsNullOrWhiteSpace(adapter.MacAddress))
                    {
                        var startIndexHwd = indentedLine.IndexOf(ether, StringComparison.Ordinal) + ether.Length;
                        adapter.MacAddress = indentedLine.Substring(startIndexHwd, 17).Trim();
                    }

                    // Parse the IPv4 Address
                    GetIPv4(indentedLine, adapter);

                    // Parse the IPv6 Address
                    GetIPv6(indentedLine, adapter);

                    // we have hit the end of the output in an indented line
                    if (j >= outputLines.Length - 1)
                        i = outputLines.Length;
                }

                // Retrieve the wireless LAN info
                var wlanInfo = wlanOutput.FirstOrDefault(x => x.StartsWith(adapter.Name));

                if (wlanInfo != null)
                {
                    adapter.IsWireless = true;
                    var essidParts = wlanInfo.Split(new[] { EssidTag }, StringSplitOptions.RemoveEmptyEntries);
                    if (essidParts.Length >= 2)
                    {
                        adapter.AccessPointName = essidParts[1].Replace("\"", string.Empty).Trim();
                    }
                }

                // Add the current adapter to the result
                result.Add(adapter);
            }

            return result.OrderBy(x => x.Name).ToList();
        }

        /// <summary>
        /// Retrieves current wireless connected network name.
        /// </summary>
        /// <returns>The connected network name.</returns>
        public Task<string> GetWirelessNetworkName() => ProcessRunner.GetProcessOutputAsync("iwgetid", "-r");
        
        private static void GetIPv4(string indentedLine, NetworkAdapterInfo adapter)
        {
            var addressText = ParseOutputTagFromLine(indentedLine, "inet addr:") ??
                              ParseOutputTagFromLine(indentedLine, "inet ");

            if (addressText == null) return;
            if (IPAddress.TryParse(addressText, out var outValue))
                adapter.IPv4 = outValue;
        }

        private static void GetIPv6(string indentedLine, NetworkAdapterInfo adapter)
        {
            var addressText = ParseOutputTagFromLine(indentedLine, "inet6 addr:") ??
                              ParseOutputTagFromLine(indentedLine, "inet6 ");

            if (addressText == null) return;

            if (IPAddress.TryParse(addressText, out var outValue))
                adapter.IPv6 = outValue;
        }

        private static string ParseOutputTagFromLine(string indentedLine, string tagName)
        {
            if (indentedLine.IndexOf(tagName, StringComparison.Ordinal) < 0)
                return null;

            var startIndex = indentedLine.IndexOf(tagName, StringComparison.Ordinal) + tagName.Length;
            var builder = new StringBuilder(1024);
            for (var c = startIndex; c < indentedLine.Length; c++)
            {
                var currentChar = indentedLine[c];
                if (!char.IsPunctuation(currentChar) && !char.IsLetterOrDigit(currentChar))
                    break;

                builder.Append(currentChar);
            }

            return builder.ToString();
        }
    }
}
