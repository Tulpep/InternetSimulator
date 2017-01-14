using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;

namespace Tulpep.InternetSimulator
{
    public class NetworkAdaptersConfiguration
    {
        private const string AUTO_IP_ADDRESS = "DHCP";
        public  Dictionary<string, string> DnsConfig { get; set; }
        public List<IPAddress> DnsAddressess { get; set; }
        
        public NetworkAdaptersConfiguration()
        {
            GetDnsConfiguration();
        }



        public bool ChangeInterfacesToLocalDns()
        {
            Logging.WriteVerbose("Configuring DNS servers in Network Intercaces as 127.0.0.1...");
            foreach (var nic in DnsConfig)
            {
                if (!SetDns(nic.Key, "127.0.0.1")) return false;
            }
            return true;
        }
        public bool ChangeInterfacesToOriginalDnsConfig()
        {
            Logging.WriteVerbose("Restoring DNS servers in Network Interfaces to their original configuration...");
            foreach (var nic in DnsConfig)
            {
                if (!SetDns(nic.Key, nic.Value)) return false;
            }
            return true;
        }

        private void GetDnsConfiguration()
        {
            DnsConfig = new Dictionary<string, string>();
            DnsAddressess = new List<IPAddress>();
            IEnumerable<NetworkInterface> adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                x.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            foreach (NetworkInterface adapter in adapters)
            {
                IEnumerable<IPAddress> dnsServers = adapter.GetIPProperties().DnsAddresses.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (dnsServers.Count() > 0)
                {
                    DnsAddressess.AddRange(dnsServers);
                    if (DnsNameIsFromDHCP(adapter.Name)) DnsConfig.Add(adapter.Description, AUTO_IP_ADDRESS);
                    else DnsConfig.Add(adapter.Description, string.Join(",", dnsServers));
                }

            }


        }

        private bool DnsNameIsFromDHCP(string nicName)
        {
            Process process = new Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = "interface ipv4 show dns " + nicName;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();
            if (process.StandardOutput.ReadToEnd().Contains("DHCP:")) return true;
            else return false;
        }
        private bool SetDns(string nicDescription, string dns)
        {
            foreach (ManagementObject nicConfiguration in new ManagementClass("Win32_NetworkAdapterConfiguration").GetInstances())
            {
                if ((bool)nicConfiguration["IPEnabled"] && nicConfiguration["Description"].Equals(nicDescription))
                {
                    ManagementBaseObject newDNS = nicConfiguration.GetMethodParameters("SetDNSServerSearchOrder");
                    if (dns != AUTO_IP_ADDRESS) newDNS["DNSServerSearchOrder"] = dns.Split(',');
                    ManagementBaseObject setDNS = nicConfiguration.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                    if ((uint)setDNS["returnValue"] == 0)
                    {
                        foreach (ManagementObject nic in new ManagementClass("Win32_NetworkAdapter").GetInstances())
                        {
                            if (nic["Description"].ToString() == nicDescription)
                            {
                                nic.InvokeMethod("Disable", null);
                                nic.InvokeMethod("Enable", null);
                                Logging.WriteVerbose("{0} configured as DNS in {1}", dns, nicDescription);
                                return true;
                            }
                        }


                    }
                }
            }
            Logging.WriteVerbose("Cannot configure {0} as DNS in {1}", dns, nicDescription);
            return false;
        }

    }
}
