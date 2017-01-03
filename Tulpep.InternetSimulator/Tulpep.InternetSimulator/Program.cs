using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.IO;

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


            string hostFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            string hostFileBackup = hostFile + "_Backup_Internet_Simulator";

            List<string> hostModifications = new List<string>();
            hostModifications.Add("microsoft.com");
            hostModifications.Add("google.com");

            bool backupSuccess = BackupHostFile(options, hostFile, hostFileBackup);
            if (!backupSuccess)
            {
                Console.WriteLine("Cannot modify HOSTS file. Run this program as Admininistrator");
                return;
            }

            ModifyHostFile(options, hostFile, hostModifications);
            StartWebServer(options);
            RestoreHostFile(options, hostFile, hostFileBackup);
        }

        static void StartWebServer(Options options)
        {
            if (options.Verbose) Console.WriteLine("Starting web Server...");
            const string baseUri = "http://*:8080";
            WebApp.Start<WebServerStartup>(baseUri);
            Console.WriteLine("Server running at {0} - press Enter to quit. ", baseUri);
            Console.ReadLine();
        }

        static bool BackupHostFile(Options options, string originalPath, string backupPath)
        {
            if (options.Verbose) Console.WriteLine("Creating backup of HOSTS file in " + originalPath);
            try
            {
                File.Copy(originalPath, backupPath, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void RestoreHostFile(Options options, string originalPath, string backupPath)
        {
            if (options.Verbose) Console.WriteLine("Restoring backup of HOSTS file to " + originalPath);
            File.Copy(backupPath, originalPath, true);
            if (options.Verbose) Console.WriteLine("Deleting backup file in " + backupPath);
            File.Delete(backupPath);
        }

        static void ModifyHostFile(Options options, string hostFilePath, IEnumerable<string> hostModifications)
        {
            using (StreamWriter writer = File.AppendText(hostFilePath))
            {
                foreach (string entry in hostModifications)
                {
                    if (options.Verbose) Console.WriteLine(String.Format("Adding domain {0} to HOSTS file", entry));
                    writer.WriteLine(String.Format("{0}\t{1}", "127.0.0.1", entry));
                }

            }
        }

    }
}
