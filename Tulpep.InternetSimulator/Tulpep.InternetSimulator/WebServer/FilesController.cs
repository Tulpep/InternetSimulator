using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

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

            bool isFile = false;
            string filePath = Program.Options.WebsMapping.FirstOrDefault(x => x.Key.ToLowerInvariant() == requestedUri).Value;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                isFile = true;
                filePath = Program.Options.FilesMapping.FirstOrDefault(x => x.Key.ToLowerInvariant() == requestedUri).Value;

            }
            if (!File.Exists(filePath)) return Request.CreateResponse(HttpStatusCode.NotFound);


            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StreamContent(new FileStream(filePath, FileMode.Open, FileAccess.Read));
            if (isFile)
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = Path.GetFileName(filePath) };
            }
            return response;
        }
    }
}