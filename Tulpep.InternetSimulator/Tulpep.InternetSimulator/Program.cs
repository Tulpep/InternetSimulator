using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Linq;

namespace Tulpep.InternetSimulator
{
    class Program
    {
        private static Options _options = new Options();
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments(args, _options);

            bool isAdmin = true;
            if (!isAdmin)
            {
                Console.WriteLine("Cannot modify HOSTS file. Run this program as Admininistrator");
                return;
            }

            StartDnsServer();
        }

        static void StartDnsServer()
        {
            using (DnsServer server = new DnsServer(10, 10))
            {
                server.ClientConnected += OnDnsClientConnected;
                server.QueryReceived += OnDnsQueryReceived;
                server.Start();

                Console.WriteLine("Press any key to stop server");
                Console.ReadLine();
            }
        }

        static async Task OnDnsClientConnected(object sender, ClientConnectedEventArgs e)
        {
            if (!IPAddress.IsLoopback(e.RemoteEndpoint.Address))
            {
                e.RefuseConnect = true;
                if (_options.Verbose) Console.WriteLine("Denied access to DNS Client " + e.RemoteEndpoint.Address);
            }
        }

        static async Task OnDnsQueryReceived(object sender, QueryReceivedEventArgs e)
        {

            string server = "8.8.8.8";
            DnsMessage message = e.Query as DnsMessage;

            if (message == null)
                return;

            DnsMessage response = message.CreateResponseInstance();

            if ((message.Questions.Count == 1))
            {
                //If domain match return localhost
                DnsQuestion question = message.Questions[0];
                if(question.RecordType ==  RecordType.A && question.Name.Equals(DomainName.Parse("microsoft.com")))
                {
                    response.ReturnCode = ReturnCode.NoError;
                    response.AnswerRecords.Add(new ARecord(question.Name, 10, IPAddress.Parse("127.0.0.1")));
                }
                else
                {
                    // send query to upstream server
                    DnsClient dnsClient = new DnsClient(IPAddress.Parse(server), 5000);
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

                if (_options.Verbose)
                {
                    if(response.AnswerRecords.Count != 0 ) Console.WriteLine("DNS Response: {0}", response.AnswerRecords.FirstOrDefault());
                    else Console.WriteLine("Cannot find {0} records for {1}", question.RecordType.ToString().ToUpperInvariant(), question.Name);
                }

            }
        }

        static void StartWebServer(Options options)
        {
            if (options.Verbose) Console.WriteLine("Starting web Server...");
            const string baseUri = "http://*:80";
            try
            {
                WebApp.Start<WebServerStartup>(baseUri);
                Console.WriteLine("Server running at {0} - press Enter to quit. ");
               // Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException.Message);
            }

        }


    }
}
