using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tulpep.InternetSimulator.WebServer
{
    class LocalWebServer
    {
        private string _url;
        private string _sucessMessage;
        public LocalWebServer(string url, string successMessage)
        {
            _url = url;
            _sucessMessage = successMessage;
        }

        public bool Start()
        {
            try
            {
                WebApp.Start<WebServerStartup>(_url);
                //Removes exceptions from console
                Trace.Listeners.Remove("HostingTraceListener");
                Logging.WriteVerbose(_sucessMessage);
                return true;
            }
            catch (Exception ex)
            {
                Logging.WriteVerbose("Cannot start Web Server " + ex.InnerException.Message);
                return false;
            }
        }
    }
}
