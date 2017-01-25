using CommandLine;
using System;
using System.Threading;
using Tulpep.InternetSimulator.WebServer;
using System.Linq;
using System.Collections.Generic;

namespace Tulpep.InternetSimulator
{
    class Program
    {
        public static Options Options { get; set; }
        private static NetworkAdaptersConfiguration _nicConfig = null;
        private static Certificates _certs = null;


        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            //Handling the Ctr + C
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Logging.WriteAlways("Ctrl + C pressed. Stopping the Internet Simulator");
                eventArgs.Cancel = true;
                exitEvent.Set();
            };



            //If arguments are not correct, exit
            Options = new Options();
            if (Parser.Default.ParseArguments(args, Options)) Options.ProcessMappings();
            else return 1;


            IEnumerable<Mapping> errorParsing = Options.Mappings.Where(x => x.ParsingSuccess == false);
            if(errorParsing.Count() > 0)
            {
                Logging.WriteAlways("Fail parsing entries : ");
                foreach(Mapping wrongEntry in errorParsing)
                {
                    Logging.WriteAlways(wrongEntry.OriginalEntry);
                }
                return 1;
            }


            //Check for duplicates
            if(Options.Mappings.GroupBy(x => x.Uri).Any(g => g.Count() > 1))
            {
                Logging.WriteAlways("Dupicated entries");
                return 1;
            }

            if(Options.Mappings.Count < 1)
            {
                Logging.WriteAlways("Not valid mappings provided");
                return 1;
            }


            _nicConfig = new NetworkAdaptersConfiguration();
            if(_nicConfig.DnsConfig.Count == 0)
            {
                Logging.WriteAlways("Not Enable or valid Network Adpaters found");
                return 1;
            }


            var domains = Options.Mappings.Select(x => x.Uri.Host).Distinct();
            _certs = new Certificates(domains);
            if (string.IsNullOrWhiteSpace(_certs.CertHash))
            {
                Logging.WriteAlways("Cannot manage SSL Certificates in your System");
                return 1;
            }

            if (!_certs.AddSSLBinding())
            {
                Logging.WriteAlways("Cannot modify SSL Bindings in your System");
                return 1;
            }

            LocalWebServer httpServer = new LocalWebServer("http://*:80", "HTTP Web Server running at 80 TCP Port");
            LocalWebServer httpsServer = new LocalWebServer("https://*:443", "HTTPS Web Server running at 443 TCP Port");
            LocalDnsServer dnsServer = new LocalDnsServer(domains, _nicConfig.DnsAddressess);
            if (_nicConfig.ChangeInterfacesToLocalDns() && dnsServer.Start() && httpServer.Start() && httpsServer.Start())
            {
                Logging.WriteAlways("Internet Simulator Running. Press Ctrl + C to Stop it");
                exitEvent.WaitOne();
                return 0;
            }
            return 1;

        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            if (_certs != null && !string.IsNullOrWhiteSpace(_certs.CertHash))
            {
                _certs.RemoveSSLBinding();
                _certs.RemoveCertificates();
            }
            if (_nicConfig != null && _nicConfig.DnsConfig.Count > 1)
                _nicConfig.ChangeInterfacesToOriginalDnsConfig();
       } 

    }
}
