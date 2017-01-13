using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;

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

        public Dictionary<string,string> GetUrlMappings()
        { 
            Dictionary<string,string> result = new Dictionary<string, string>();
            foreach (string pair in Urls)
            {
                string[] spplitedString = pair.Split(new char[] { ',' },2);
                if(spplitedString.Length < 2)
                {
                    Program.WriteInConsole(string.Format("Cannot parse entry {0}", pair));
                    Program.Exit(1);
                }

                string url = spplitedString[0];
                string filePath = spplitedString[1];

                Uri uriResult;
                bool urlIsHTTPorHTTPs  = Uri.TryCreate(url, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (!urlIsHTTPorHTTPs)
                {
                    Program.WriteInConsole(string.Format("{0} is not a valid HTTP or HTTPS url", url));
                    Program.Exit(1);
                }

                bool filePathExists;
                try
                {
                    filePathExists = File.Exists(filePath);
                }
                catch
                {
                    filePathExists = false;
                }

                if (!filePathExists)
                {
                    Program.WriteInConsole(string.Format("{0} file does not exits", filePath));
                    Program.Exit(1);
                }


                Program.WriteInConsole(string.Format("{0} -> {1}", url, filePath));
                result.Add(url, filePath);
            }


            return result;
        }

    }

}
