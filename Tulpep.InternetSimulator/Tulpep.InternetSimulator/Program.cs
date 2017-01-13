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
using System.Management.Automation;
using System.Threading;

namespace Tulpep.InternetSimulator
{
    class Program
    {
        private static Options _options = new Options();
        private const string AUTO_IP_ADDRESS = "DHCP";
        private const string SSL_FRIENDLY_NAME = "Internet Simulator";
        private static Dictionary<string, string> _nicsOriginalConfiguration = new Dictionary<string, string>();
        private static DnsClient _upStreamDnsClient = null;
        static int Main(string[] args)
        {
            //If arguments are not correct, exit
            if (!CommandLine.Parser.Default.ParseArguments(args, _options))
            {
                return 1;
            };

            //Handling the Ctr + C exit event
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) => {
                ExitCleanUp();
                eventArgs.Cancel = true;
                exitEvent.Set();
            };

            GetDnsConfiguration();
            if(_nicsOriginalConfiguration.Count == 0)
            {
                WriteInConsole("Not Enable or valid Network Adpaters found");
                return 1;
            }

            string certHash = InstallCertificate(new List<string> { "larnia.co", "popo.com", "www.msftncsi.com" });
            if (String.IsNullOrWhiteSpace(certHash))
            {
                WriteInConsole("Cannot manage SSL Certificates in your System");
                return 1;
            }

            if (AddSSLBinding(certHash) && StartWebServer() && StartDnsServer() && ChangeInterfacesToLocalDns())
            {
                Console.WriteLine("Internet Simulator Running. Press Ctrl + C to Stop it");
                exitEvent.WaitOne();
                return 0;
            }
            else
            {
                ExitCleanUp();
                return 1;
            }
        }


        static void ExitCleanUp()
        {
            Console.WriteLine("Ctrl + C pressed. Stopping the Internet Simulator");
            ChangeInterfacesToOriginalDnsConfig();
            RemoveSSLBinding();
            RemoveCertificates();
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


        static Task OnDnsClientConnected(object sender, ClientConnectedEventArgs e)
        {
            if (!IPAddress.IsLoopback(e.RemoteEndpoint.Address))
            {
                e.RefuseConnect = true;
                WriteInConsole("Denied access to DNS Client " + e.RemoteEndpoint.Address);
            }
            return Task.Delay(0);
        }

        static async Task OnDnsQueryReceived(object sender, QueryReceivedEventArgs e)
        {
            DnsMessage message = e.Query as DnsMessage;

            if (message == null || message.Questions.Count != 1) return;

            DnsQuestion question = message.Questions[0];
            DnsMessage response = message.CreateResponseInstance();

            //If domain match return localhost
            if (question.Name.Equals(DomainName.Parse("www.msftncsi.com")) || question.Name.Equals(DomainName.Parse("larnia.co")) || question.Name.Equals(DomainName.Parse("popo.com")))
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

        static string InstallCertificate(IEnumerable<string> domains)
        {
            //Create certificate in Computer Personal store
            string createCertificateScript = string.Format(@"New-SelfSignedCertificate -DnsName {0} -CertStoreLocation {1} -FriendlyName ""{2}""", 
                                        string.Join(",", domains), 
                                        @"Cert:\LocalMachine\My",
                                        SSL_FRIENDLY_NAME);

            var createCertificatesPs = PowerShell.Create();
            var createCertifiacateResult = createCertificatesPs.AddScript(createCertificateScript).Invoke();
            if (createCertificatesPs.HadErrors) return String.Empty;
            string certHash = createCertifiacateResult[0].Properties["Thumbprint"].Value.ToString();

            WriteInConsole("SLL Certificate created and saved in your Computer Personal Store. Thumbprint " + certHash);

            string copyCertScript = string.Format(@"
                    $srcStore = New-Object System.Security.Cryptography.X509Certificates.X509Store ""My"", ""LocalMachine""
                    $srcStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
                    $cert =  $srcStore.certificates -match ""{0}""

                    $dstStore = New-Object System.Security.Cryptography.X509Certificates.X509Store ""Root"", ""LocalMachine""
                    $dstStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
                    $dstStore.Add($cert[0])

                    $srcStore.Close
                    $dstStore.Close

                    ", certHash);

            var AddCertificateToRootPs = PowerShell.Create();
            AddCertificateToRootPs.AddScript(copyCertScript).Invoke();
            if (AddCertificateToRootPs.HadErrors) return String.Empty;

            WriteInConsole("SLL Certificate added to Trusted Root Certification Authorities");
            return certHash;
        }

        static bool RemoveCertificates()
        {
            var powerShell = PowerShell.Create();

            string removeCertScript = string.Format(@"
                    Get-ChildItem Cert:\LocalMachine\My | Where {{ $_.FriendlyName -match ""Internet Simulator""}} | Remove-Item
                    Get-ChildItem Cert:\LocalMachine\Root | Where {{ $_.FriendlyName -match ""Internet Simulator""}} | Remove-Item
                    ", SSL_FRIENDLY_NAME);


            powerShell.AddScript(removeCertScript).Invoke();
            if (powerShell.HadErrors)
            {
                WriteInConsole("Cannot remove SSL generated certificates from the system");
                return false; 
            }
            else
            {
                WriteInConsole("SLL generated certificates has been remove from the System");
                return true;
            }
        }


        static bool RemoveSSLBinding()
        {
            Process process = new Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = "http delete sslcert ipport=0.0.0.0:443";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                WriteInConsole("SSL binding of port 443 has been removed");
                return true;
            }
            return false;
        }


        static bool AddSSLBinding(string certHash)
        {
            RemoveSSLBinding();

            Process process = new Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = string.Format("http add sslcert ipport=0.0.0.0:443 certhash={0} appid={{{1}}}", certHash, Guid.NewGuid());
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                WriteInConsole("Added SSL Bindings with generated certificates to 443 Port");
                return true;
            }
            WriteInConsole("Cannot modify SSL Bindings in your System");
            return false;
        }

        static bool StartWebServer()
        {
            try
            {
                WebApp.Start<WebServerStartup>("http://*:80");
                //Removes exceptions from console
                Trace.Listeners.Remove("HostingTraceListener");
                WriteInConsole(String.Format("HTTP Web Server running at 80 TCP Port"));

                WebApp.Start<WebServerStartup>("https://*:443");
                //Removes exceptions from console
                Trace.Listeners.Remove("HostingTraceListener");
                WriteInConsole(String.Format("HTTPS Web Server running at 443 TCP Port"));
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
