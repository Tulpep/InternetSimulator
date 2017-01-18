using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tulpep.InternetSimulator
{
    public class Options
    {

        [OptionArray("webs", HelpText = "List of websites")]
        public string[] WebpagesArray { get; set; }

        [OptionArray("files", HelpText = "List of files")]
        public string[] FilesArray { get; set; }

        [Option('v', "verbose", DefaultValue = false, HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }

        [Option("ncsci", DefaultValue = true, HelpText = "Microsoft NCSI. Network Connectivity Status Indicator")]
        public bool Ncsi { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

        public Dictionary<string, string> WebsMapping { get; set; }
        public Dictionary<string, string> FilesMapping { get; set; }

        public bool FailParsing { get; set; }

        public IEnumerable<string> Domains { get; set; }

        public void ProcessMappings()
        {
            WebsMapping = GetUrlMappings(WebpagesArray);
            FilesMapping = GetUrlMappings(FilesArray);
            if(!FailParsing)
            {
                List<string> domainsToSimulate = new List<string>();
                if(WebsMapping != null)
                    domainsToSimulate.AddRange(WebsMapping.Select(x => new Uri(x.Key).Host));
                if(FilesMapping != null)
                    domainsToSimulate.AddRange(FilesMapping.Select(x => new Uri(x.Key).Host));
                Domains = domainsToSimulate.Distinct();
            }
        }


        public Dictionary<string,string> GetUrlMappings(string[] inputArray)
        {
            Dictionary<string,string> result = new Dictionary<string, string>();
            foreach (string pair in inputArray)
            {
                string[] spplitedString = pair.Split(new char[] { ',' },2);
                if(spplitedString.Length < 2)
                {
                    Logging.WriteAlways("Cannot parse entry {0}", pair);
                    FailParsing = true;
                    break;
                }

                string url = spplitedString[0];
                string filePath = spplitedString[1];

                if (!IsValidUrl(url))
                {
                    Logging.WriteAlways("{0} is not a valid HTTP or HTTPS url", url);
                    FailParsing = true;
                    break;
                }



                if (!IsValidFilePath(filePath))
                {
                    Logging.WriteAlways("{0} file does not exits", filePath);
                    FailParsing = true;
                    break;
                }


                Logging.WriteVerbose("{0} -> {1}", url, filePath);
                result.Add(url, filePath);
            }


            return result;
        }


        private bool IsValidUrl(string url)
        {
            Uri uriResult;
            return Uri.TryCreate(url, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
        
        private bool IsValidFilePath(string filePath)
        {
            bool result;
            try
            {
                result = File.Exists(filePath);
            }
            catch
            {
                result = false;
            }

            return result;
        }
    }

}
