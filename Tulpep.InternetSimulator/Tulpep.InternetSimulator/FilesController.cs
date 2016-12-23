using System.Collections.Generic;
using System.Web.Http;

namespace OwinSelfhostSample
{
    public class FilesController : ApiController
    {
        // GET api/values 
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }
    }
}