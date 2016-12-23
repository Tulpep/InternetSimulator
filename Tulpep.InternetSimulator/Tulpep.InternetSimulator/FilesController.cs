using System.Collections.Generic;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace OwinSelfhostSample
{
    public class FilesController : ApiController
    {
        public HttpResponseMessage Get()
        {
            var requestedUri = Request.RequestUri.AbsoluteUri;
            return Request.CreateResponse(System.Net.HttpStatusCode.OK, requestedUri);
        }
    }
}