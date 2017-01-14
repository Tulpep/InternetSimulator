using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Tulpep.InternetSimulator
{
    class LocalDnsServer
    {
        private DnsServer _server;
        private DnsClient _upStreamDnsClient;
        private static DomainName _ncsiDomain = DomainName.Parse("www.msftncsi.com");

        public LocalDnsServer(IEnumerable<string> simulatedDomains, IEnumerable<IPAddress> upStreamServers)
        {
            _server = new DnsServer(10, 10);
            _server.ClientConnected += OnlyAcceptLoopbackConnections;
            _server.QueryReceived += (sender, e) => OnDnsQueryReceived(e, simulatedDomains.Select(x => DomainName.Parse(x)));

            IEnumerable<IPAddress> usefullUpstreamServers = upStreamServers.Distinct().Where(x => !IPAddress.IsLoopback(x));
            if (usefullUpstreamServers.Count() > 0)
            {
                _upStreamDnsClient = new DnsClient(usefullUpstreamServers, 5000);
            }

        }

        public bool Start()
        {
            try
            {
                _server.Start();
                Logging.WriteVerbose("DNS Server started");
                return true;
            }
            catch
            {
                Logging.WriteVerbose("Can not start DNS Server");
                return false;
            }
        }

        private Task OnlyAcceptLoopbackConnections(object sender, ClientConnectedEventArgs e)
        {
            if (!IPAddress.IsLoopback(e.RemoteEndpoint.Address))
            {
                e.RefuseConnect = true;
                Logging.WriteVerbose("Denied access to DNS Client " + e.RemoteEndpoint.Address);
            }
            return Task.Delay(0);
        }

        private async Task OnDnsQueryReceived(QueryReceivedEventArgs e, IEnumerable<DomainName> domains)
        {
            DnsMessage message = e.Query as DnsMessage;

            if (message == null || message.Questions.Count != 1) return;

            DnsQuestion question = message.Questions[0];
            DnsMessage response = message.CreateResponseInstance();

            //If domain match return localhost
            if ((Program.Options.Ncsi && question.Name.Equals(_ncsiDomain)) || domains.Any(x => x.Equals(question.Name)))
            {
                if (question.RecordType == RecordType.A)
                {
                    response.ReturnCode = ReturnCode.NoError;
                    response.AnswerRecords.Add(new ARecord(question.Name, 10, IPAddress.Loopback));
                }
                else if (question.RecordType == RecordType.Aaaa)
                {
                    response.ReturnCode = ReturnCode.NoError;
                    response.AnswerRecords.Add(new AaaaRecord(question.Name, 10, IPAddress.IPv6Loopback));
                }
            }
            else if (_upStreamDnsClient != null)
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

            if (response.AnswerRecords.Count != 0)
            {
                Logging.WriteVerbose("DNS Response: {0}", response.AnswerRecords.FirstOrDefault());
            }
            else
            {
                Logging.WriteVerbose("DNS Response: Can not find {0} records for {1}", question.RecordType.ToString().ToUpperInvariant(), question.Name);
            }
        }

    }
}
