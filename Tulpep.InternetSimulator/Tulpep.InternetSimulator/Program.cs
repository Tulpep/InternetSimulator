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
        private static Dictionary<string, string> _nicsOriginalConfiguration = GetDnsConfiguration();
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments(args, _options);

            ChangeInterfacesToLocalDns();

            StartDnsServer();
            Console.WriteLine("Press any key to stop server");
            Console.ReadLine();

            ChangeInterfacesToOriginalDnsConfig();
            Console.ReadLine();
        }



        static Dictionary<string, string> GetDnsConfiguration()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                IEnumerable<IPAddress> dnsServers = adapterProperties.DnsAddresses.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (dnsServers.Count() > 0)
                {
                    if(DnsNameIsFromDHCP(adapter.Name)) result.Add(adapter.Description, AUTO_IP_ADDRESS);
                    else result.Add(adapter.Description, string.Join(",", dnsServers));
                }

            }
            return result;
        }

        static void ChangeInterfacesToLocalDns()
        {
            WriteInConsole("Configuring DNS servers in Network Intercaces as 127.0.0.1...");
            foreach (var nic in _nicsOriginalConfiguration)
            {
                SetDns(nic.Key, "127.0.0.1");
            }
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

        static void ChangeInterfacesToOriginalDnsConfig()
        {
            WriteInConsole("Restoring DNS servers in Network Interfaces to their original configuration...");
            foreach (var nic in _nicsOriginalConfiguration)
            {
                SetDns(nic.Key, nic.Value);
            }
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
                    WriteInConsole(String.Format("Cannot configure {0} as DNS in {1}", dns, nicDescription));
                }
            }
            return false;
        }


        static void StartDnsServer()
        {
            DnsServer server = new DnsServer(10, 10);
            server.ClientConnected += OnDnsClientConnected;
            server.QueryReceived += OnDnsQueryReceived;
            server.Start();
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

                    List<IPAddress> upStreamServers = new List<IPAddress>();
                    foreach(var dnsServers in _nicsOriginalConfiguration.Values.Distinct())
                    {
                        if (dnsServers == AUTO_IP_ADDRESS) continue;
                        foreach (string dns in dnsServers.Split(',')) upStreamServers.Add(IPAddress.Parse(dns));
                    }

                    // send query to upstream server
                    DnsClient dnsClient = new DnsClient(upStreamServers, 5000);
                    DnsMessage upstreamResponse = await dnsClient.ResolveAsync(question.Name, question.RecordType, question.RecordClass);

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



        static void StartWebServer(Options options)
        {
            WriteInConsole("Starting web Server...");
            const string baseUri = "http://*:80";
            try
            {
                WebApp.Start<WebServerStartup>(baseUri);
                WriteInConsole("Server running at {0} - press Enter to quit. ");
                // Console.ReadLine();
            }
            catch (Exception ex)
            {
                WriteInConsole(ex.InnerException.Message);
            }

        }


        static void WriteInConsole(string message)
        {
            if (_options.Verbose) Console.WriteLine(message);
        }


    }
}
