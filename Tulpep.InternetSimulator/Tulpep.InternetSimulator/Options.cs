using CommandLine;
using CommandLine.Text;

namespace Tulpep.InternetSimulator
{
    public class Options
    {
        [Option('r', "read", Required = true,
          HelpText = "Input file to be processed.")]
        public string InputFile { get; set; }

        [Option('v', "verbose", DefaultValue = true,
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
