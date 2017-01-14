using System;

namespace Tulpep.InternetSimulator
{
    public static class Logging
    {
        public static void WriteAlways(string text, params object[] args)
        {
            if (Program.Options.Verbose) Console.WriteLine(string.Format(text, args));
        }

        public static void WriteVerbose(string text, params object[] args)
        {
            if (Program.Options.Verbose) Console.WriteLine(string.Format(text, args));
        }
    }
}
