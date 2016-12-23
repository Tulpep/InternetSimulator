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

            StartWebServer("http://localhost:8080");
        }

        static void StartWebServer(string baseUri)
        {

            Console.WriteLine("Starting web Server...");
            WebApp.Start<WebServerStartup>(baseUri);
            Console.WriteLine("Server running at {0} - press Enter to quit. ", baseUri);
            Console.ReadLine();
        }

    }
}
