using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace OwinSelfhostSample
{
    public class FilesController : ApiController
    {
        public async Task<HttpResponseMessage> Get()
        {
            var requestedUri = Request.RequestUri.AbsoluteUri;
            var localFilePath = @"C:\hello.txt";

            if(File.Exists(localFilePath))
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(new FileStream(localFilePath, FileMode.Open, FileAccess.Read));
                return response;
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, requestedUri);
            }

        }
    }
}