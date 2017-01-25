using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;

namespace Tulpep.InternetSimulator
{
    public class Options
    {

        [OptionArray('w', "webs", HelpText = "List of websites")]
        public string[] WebpagesArray { get; set; }

        [OptionArray('f', "files", HelpText = "List of files")]
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

        public List<Mapping> Mappings { get; set; }

        public void ProcessMappings()
        {
            Mappings = new List<Mapping>();
            GetUrlMappings(WebpagesArray, FileBehavior.Web);
            GetUrlMappings(FilesArray, FileBehavior.File);
        }

        public void GetUrlMappings(string[] inputArray, FileBehavior fileBehavior)
        {
            if (inputArray == null) return;
            foreach (string pair in inputArray)
            {
                Mapping mapping = new Mapping{ OriginalEntry = pair, Behavior = fileBehavior, ParsingSuccess = false };
                string[] spplitedString = pair.Split(new char[] { ',' },2);
                if(spplitedString.Length < 2)
                {
                    mapping.ParsingMessage = string.Format("Cannot parse entry {0}", pair);
                    continue;
                }

                string uriString = spplitedString[0].ToLowerInvariant();
                try
                {
                    mapping.Uri = new Uri(uriString, UriKind.Absolute);
                }
                catch
                {
                    mapping.ParsingMessage = string.Format("{0} is not a valid url", uriString);
                    continue;
                }

                if (mapping.Uri.Scheme != Uri.UriSchemeHttp && mapping.Uri.Scheme != Uri.UriSchemeHttps)
                {
                    mapping.ParsingMessage = string.Format("{0} is not a valid HTTP or HTTPS url", mapping.Uri);
                    continue;
                }

                mapping.FilePath = spplitedString[1];
                if (!IsValidFilePath(mapping.FilePath))
                {
                    mapping.ParsingMessage = string.Format("{0} file does not exits", mapping.FilePath);
                    continue;
                }

                mapping.ParsingSuccess = true;

                Mappings.Add(mapping);
            }
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
