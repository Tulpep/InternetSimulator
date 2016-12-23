using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace OwinSelfhostSample
{
    public class FilesController : ApiController
    {
        public HttpResponseMessage Get()
        {
            var requestedUri = Request.RequestUri.AbsoluteUri;
            var localFilePath = @"C:\hello.txt";

            if(!File.Exists(localFilePath)) return Request.CreateResponse(HttpStatusCode.NotFound);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StreamContent(new FileStream(localFilePath, FileMode.Open, FileAccess.Read));
            return response;

        }
    }
}