using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace OwinSelfhostSample
{
    public class FilesController : ApiController
    {
        public HttpResponseMessage Get()
        {
            string requestedUri = Request.RequestUri.AbsoluteUri;
            if(requestedUri == "http://www.msftncsi.com/ncsi.txt")
            {
                HttpResponseMessage ncsiResponse = new HttpResponseMessage(HttpStatusCode.OK);
                ncsiResponse.Content = new StringContent("Microsoft NCSI");
                return ncsiResponse;
            }

            string localFilePath = @"C:\hello.txt";

            if (!File.Exists(localFilePath)) return Request.CreateResponse(HttpStatusCode.NotFound);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StreamContent(new FileStream(localFilePath, FileMode.Open, FileAccess.Read));
            return response;
        }
    }
}