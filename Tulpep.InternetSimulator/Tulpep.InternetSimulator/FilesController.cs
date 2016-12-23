using System.Collections.Generic;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace OwinSelfhostSample
{
    public class FilesController : ApiController
    {
        // GET api/values 
        public string Get()
        {
            return Request.RequestUri.AbsoluteUri;
        }
    }
}