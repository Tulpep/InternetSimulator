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
            if(_nicsOriginalConfiguration.Count == 0)
            {
                Console.WriteLine("Not Enable Network Adpaters found");
                return;
            }



            if (StartDnsServer() && StartWebServer() && ChangeInterfacesToLocalDns())
            {
                Console.WriteLine("Press any key to stop...");
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

                    upStreamServers.AddRange(dnsServers.Where(x => !IPAddress.IsLoopback(x)));
                }

            }

            if(upStreamServers.Count > 0)
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
            foreach (ManagementObject nicConfiguration in new ManagementClass("Win32_NetworkAdapterConfiguration").GetInstances())
            {
                if ((bool)nicConfiguration["IPEnabled"] && nicConfiguration["Description"].Equals(nicDescription))
                {
                    ManagementBaseObject newDNS = nicConfiguration.GetMethodParameters("SetDNSServerSearchOrder");
                    if (dns != AUTO_IP_ADDRESS) newDNS["DNSServerSearchOrder"] = dns.Split(',');
                    ManagementBaseObject setDNS = nicConfiguration.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                    if ((uint)setDNS["returnValue"] == 0)
                    {
                        foreach(ManagementObject nic in new ManagementClass("Win32_NetworkAdapter").GetInstances())
                        {
                            if(nic["Description"].ToString() == nicDescription)
                            {
                                nic.InvokeMethod("Disable", null);
                                nic.InvokeMethod("Enable", null);
                                WriteInConsole(String.Format("{0} configured as DNS in {1}", dns, nicDescription));
                                return true;
                            }
                        }


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
                WriteInConsole("DNS Server started");
                return true;
            }
            catch
            {
                WriteInConsole("Can not start DNS Server");
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

            if (message == null || message.Questions.Count != 1) return;

            DnsQuestion question = message.Questions[0];
            DnsMessage response = message.CreateResponseInstance();

            //If domain match return localhost
            if (question.Name.Equals(DomainName.Parse("microsoft.com")) || question.Name.Equals(DomainName.Parse("www.msftncsi.com")))
            {
                response.ReturnCode = ReturnCode.NoError;

                if (question.RecordType == RecordType.A)
                    response.AnswerRecords.Add(new ARecord(question.Name, 10, IPAddress.Loopback));
                else if(question.RecordType == RecordType.Aaaa)
                    response.AnswerRecords.Add(new AaaaRecord(question.Name, 10, IPAddress.IPv6Loopback));
            }
            else if(_upStreamDnsClient != null)
            {
                // send query to upstream server
                DnsMessage upstreamResponse = await _upStreamDnsClient.ResolveAsync(question.Name, question.RecordType, question.RecordClass);
                if (upstreamResponse == null) return;

                // if got an answer, copy it to the message sent to the client
                response.AnswerRecords.AddRange(upstreamResponse.AnswerRecords);
                response.AdditionalRecords.AddRange(upstreamResponse.AdditionalRecords);
                response.ReturnCode = upstreamResponse.ReturnCode;
            }


            // set the response
            e.Response = response;

            if (response.AnswerRecords.Count != 0) WriteInConsole(string.Format("DNS Response: {0}", response.AnswerRecords.FirstOrDefault()));
            else WriteInConsole(string.Format("DNS Response: Can not find {0} records for {1}", question.RecordType.ToString().ToUpperInvariant(), question.Name));
        }



        static bool StartWebServer()
        {
            try
            {
                const string baseUri = "http://*:80";
                WebApp.Start<WebServerStartup>(baseUri);
                Trace.Listeners.Remove("HostingTraceListener");
                WriteInConsole(String.Format("Web Server running at {0}", baseUri));
                return true;
            }
            catch (Exception ex)
            {
                WriteInConsole("Cannot start Web Server " + ex.InnerException.Message);
                return false;
            }
        }


        static void WriteInConsole(string message)
        {
            if (_options.Verbose) Console.WriteLine(message);
        }


    }
}
