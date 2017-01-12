using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace Tulpep.InternetSimulator
{
    class Program
    {
        private static Options _options = new Options();
        private const string AUTO_IP_ADDRESS = "DHCP";
        private static Dictionary<string, string> _nicsOriginalConfiguration = new Dictionary<string, string>();
        private static DnsClient _upStreamDnsClient = null;
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments(args, _options);

            GetDnsConfiguration();
            if (StartDnsServer() && ChangeInterfacesToLocalDns())
            {
                Console.WriteLine("Press any key to stop server");
                Console.ReadLine();
            }
            else return;

            ;
           // StartWebServer();


            ChangeInterfacesToOriginalDnsConfig();
            Console.ReadLine();
        }



        static void GetDnsConfiguration()
        {
            List<IPAddress> upStreamServers = new List<IPAddress>();
            IEnumerable<NetworkInterface> adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.NetworkInterfaceType != NetworkInterfaceType.Tunnel && 
                x.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            foreach (NetworkInterface adapter in adapters)
            {
                IEnumerable<IPAddress> dnsServers = adapter.GetIPProperties().DnsAddresses.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (dnsServers.Count() > 0)
                {
                    if(DnsNameIsFromDHCP(adapter.Name)) _nicsOriginalConfiguration.Add(adapter.Description, AUTO_IP_ADDRESS);
                    else _nicsOriginalConfiguration.Add(adapter.Description, string.Join(",", dnsServers));

                    upStreamServers.AddRange(dnsServers);
                }

            }

            _upStreamDnsClient = new DnsClient(upStreamServers.Distinct(), 5000);
        }

        static bool ChangeInterfacesToLocalDns()
        {
            WriteInConsole("Configuring DNS servers in Network Intercaces as 127.0.0.1...");
            foreach (var nic in _nicsOriginalConfiguration)
            {
                if (!SetDns(nic.Key, "127.0.0.1")) return false;
            }
            return true;
        }


        static bool DnsNameIsFromDHCP(string nicName)
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

        static bool ChangeInterfacesToOriginalDnsConfig()
        {
            WriteInConsole("Restoring DNS servers in Network Interfaces to their original configuration...");
            foreach (var nic in _nicsOriginalConfiguration)
            {
                if (!SetDns(nic.Key, nic.Value)) return false;
            }
            return true;
        }

        static bool SetDns(string nicDescription, string dns)
        {
            ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMC.GetInstances();

            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"] && objMO["Description"].Equals(nicDescription))
                {
                    ManagementBaseObject newDNS = objMO.GetMethodParameters("SetDNSServerSearchOrder");
                    if (dns != AUTO_IP_ADDRESS) newDNS["DNSServerSearchOrder"] = dns.Split(',');
                    ManagementBaseObject setDNS = objMO.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                    if ((uint)setDNS["returnValue"] == 0)
                    {
                        WriteInConsole(String.Format("{0} configured as DNS in {1}", dns, nicDescription));
                        return true;
                    }
                }
            }
            WriteInConsole(String.Format("Cannot configure {0} as DNS in {1}", dns, nicDescription));
            return false;
        }


        static bool StartDnsServer()
        {
            try
            {
                DnsServer server = new DnsServer(10, 10);
                server.ClientConnected += OnDnsClientConnected;
                server.QueryReceived += OnDnsQueryReceived;
                server.Start();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static async Task OnDnsClientConnected(object sender, ClientConnectedEventArgs e)
        {
            if (!IPAddress.IsLoopback(e.RemoteEndpoint.Address))
            {
                e.RefuseConnect = true;
                WriteInConsole("Denied access to DNS Client " + e.RemoteEndpoint.Address);
            }
        }

        static async Task OnDnsQueryReceived(object sender, QueryReceivedEventArgs e)
        {
            DnsMessage message = e.Query as DnsMessage;

            if (message == null)
                return;

            DnsMessage response = message.CreateResponseInstance();

            if ((message.Questions.Count == 1))
            {
                //If domain match return localhost
                DnsQuestion question = message.Questions[0];
                if (question.RecordType == RecordType.A && question.Name.Equals(DomainName.Parse("microsoft.com")))
                {
                    response.ReturnCode = ReturnCode.NoError;
                    response.AnswerRecords.Add(new ARecord(question.Name, 10, IPAddress.Parse("127.0.0.1")));
                }
                else
                {
                    // send query to upstream server
                    DnsMessage upstreamResponse = await _upStreamDnsClient.ResolveAsync(question.Name, question.RecordType, question.RecordClass);

                    // if got an answer, copy it to the message sent to the client
                    if (upstreamResponse != null)
                    {
                        foreach (DnsRecordBase record in (upstreamResponse.AnswerRecords))
                        {
                            response.AnswerRecords.Add(record);
                        }
                        foreach (DnsRecordBase record in (upstreamResponse.AdditionalRecords))
                        {
                            response.AdditionalRecords.Add(record);
                        }


                        response.ReturnCode = ReturnCode.NoError;
                    }
                }

                // set the response
                e.Response = response;

                if (response.AnswerRecords.Count != 0) WriteInConsole(string.Format("DNS Response: {0}", response.AnswerRecords.FirstOrDefault()));
                else WriteInConsole(string.Format("DNS Response: Can not find {0} records for {1}", question.RecordType.ToString().ToUpperInvariant(), question.Name));

            }
        }



        static bool StartWebServer()
        {
            try
            {
                WriteInConsole("Starting web Server...");
                const string baseUri = "http://*:80";
                WebApp.Start<WebServerStartup>(baseUri);
                WriteInConsole(String.Format("Server running at {0} - press Enter to quit. ", baseUri));
                return true;
            }
            catch (Exception ex)
            {
                WriteInConsole(ex.InnerException.Message);
                return false;
            }
        }


        static void WriteInConsole(string message)
        {
            if (_options.Verbose) Console.WriteLine(message);
        }


    }
}
