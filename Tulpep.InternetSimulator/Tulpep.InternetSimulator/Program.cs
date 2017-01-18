using CommandLine;
using System;
using System.Threading;
using Tulpep.InternetSimulator.WebServer;

namespace Tulpep.InternetSimulator
{
    class Program
    {
        public static Options Options { get; set; }

        
        static int Main(string[] args)
        {
            NetworkAdaptersConfiguration nicConfig = null;
            Certificates certs = null;

            //If arguments are not correct, exit
            Options = new Options();
            if (Parser.Default.ParseArguments(args, Options)) Options.ProcessMappings();
            else return 1;

            if (Options.UrlMappings == null) return 1;

            //Handling the Ctr + C exit event
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) => {
                CleanUp(nicConfig, certs);
                eventArgs.Cancel = true;
                exitEvent.Set();
            };

            nicConfig = new NetworkAdaptersConfiguration();
            if(nicConfig.DnsConfig.Count == 0)
            {
                Logging.WriteAlways("Not Enable or valid Network Adpaters found");
                return 1;
            }


            certs = new Certificates(Options.Domains);
            if (String.IsNullOrWhiteSpace(certs.CertHash))
            {
                Logging.WriteAlways("Cannot manage SSL Certificates in your System");
                return 1;
            }

            if (!certs.AddSSLBinding())
            {
                Logging.WriteAlways("Cannot modify SSL Bindings in your System");
                return 1;
            }

            LocalWebServer httpServer = new LocalWebServer("http://*:80", "HTTP Web Server running at 80 TCP Port");
            LocalWebServer httpsServer = new LocalWebServer("https://*:443", "HTTPS Web Server running at 443 TCP Port");
            LocalDnsServer dnsServer = new LocalDnsServer(Options.Domains, nicConfig.DnsAddressess);
            if (nicConfig.ChangeInterfacesToLocalDns() && dnsServer.Start() && httpServer.Start() && httpsServer.Start())
            {
                Logging.WriteAlways("Internet Simulator Running. Press Ctrl + C to Stop it");
                exitEvent.WaitOne();
                return 0;
            }
            else
            {
                CleanUp(nicConfig, certs);
                return 1;
            }

        }


        static void CleanUp(NetworkAdaptersConfiguration nicConfig, Certificates certs)
        {
            Logging.WriteAlways("Ctrl + C pressed. Stopping the Internet Simulator");
            if(certs != null)
            {
                certs.RemoveSSLBinding();
                certs.RemoveCertificates();
            }
            if (nicConfig != null) nicConfig.ChangeInterfacesToOriginalDnsConfig();
        }



    }
}
