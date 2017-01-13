using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Tulpep.InternetSimulator
{
    public class Options
    {
        [ValueList(typeof(List<string>))]
        public IList<string> Urls { get; set; }

        [Option('v', "verbose", DefaultValue = false,
          HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

}
