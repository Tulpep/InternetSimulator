using Microsoft.Owin.Hosting;
using System;

namespace Tulpep.InternetSimulator
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                if (options.Verbose) Console.WriteLine("Filename: {0}", options.InputFile);
            }


            // Specify the URI to use for the local host:
            string baseUri = "http://localhost:8080";

            Console.WriteLine("Starting web Server...");
            WebApp.Start<Startup>(baseUri);
        

            Console.WriteLine("Server running at {0} - press Enter to quit. ", baseUri);
            Console.ReadLine();

        }

    }
}
