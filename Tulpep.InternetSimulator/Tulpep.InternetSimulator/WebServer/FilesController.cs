using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Tulpep.InternetSimulator;
using System.Linq;
using System.Net.Http.Headers;

namespace Tulpep.InternetSimulator.WebServer
{
    public class FilesController : ApiController
    {
        public HttpResponseMessage Get()
        {
            string requestedUri = Request.RequestUri.AbsoluteUri.ToLowerInvariant().TrimEnd('/');
            if(Program.Options.Ncsi && requestedUri == "http://www.msftncsi.com/ncsi.txt" )
            {
                HttpResponseMessage ncsiResponse = new HttpResponseMessage(HttpStatusCode.OK);
                ncsiResponse.Content = new StringContent("Microsoft NCSI");
                return ncsiResponse;
            }

            string filePath = Program.Options.UrlMappings.FirstOrDefault(x => x.Key.ToLowerInvariant() == requestedUri).Value;
            if (!File.Exists(filePath)) return Request.CreateResponse(HttpStatusCode.NotFound);


            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StreamContent(new FileStream(filePath, FileMode.Open, FileAccess.Read));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = Path.GetFileName(filePath) };
            return response;
        }
    }
}