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
        [ValueList(typeof(List<string>))]
        public IList<string> Urls { get; set; }

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


        public Dictionary<string, string> UrlMappings { get; set; }
        public IEnumerable<string> Domains { get; set; }

        public void ProcessMappings()
        {
            UrlMappings = GetUrlMappings();
            if(UrlMappings != null)
            {
                Domains = UrlMappings.Select(x => new Uri(x.Key).Host);
            }
        }


        public Dictionary<string,string> GetUrlMappings()
        { 
            Dictionary<string,string> result = new Dictionary<string, string>();
            foreach (string pair in Urls)
            {
                string[] spplitedString = pair.Split(new char[] { ',' },2);
                if(spplitedString.Length < 2)
                {
                    Logging.WriteAlways("Cannot parse entry {0}", pair);
                    return null;
                }

                string url = spplitedString[0];
                string filePath = spplitedString[1];

                if (!IsValidUrl(url))
                {
                    Logging.WriteAlways("{0} is not a valid HTTP or HTTPS url", url);
                    return null;
                }



                if (!IsValidFilePath(filePath))
                {
                    Logging.WriteAlways("{0} file does not exits", filePath);
                    return null;
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
