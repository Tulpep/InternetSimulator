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


            Mapping mapping = Program.Options.Mappings.FirstOrDefault(x => x.Uri == requestedUri);
            if (mapping == null || !File.Exists(mapping.FilePath)) return Request.CreateResponse(HttpStatusCode.NotFound);


            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StreamContent(new FileStream(mapping.FilePath, FileMode.Open, FileAccess.Read));
            if (mapping.Behavior == FileBehavior.File)
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = Path.GetFileName(mapping.FilePath) };
            }
            return response;
        }
    }
}