using Owin;
using System.Web.Http;

namespace Tulpep.InternetSimulator
{
    class WebServerStartup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            HttpConfiguration config = new HttpConfiguration();
            config.Routes.MapHttpRoute(
                name: "CatchAll",
                routeTemplate: "{*uri}",
                defaults: new { controller = "Files", uri = RouteParameter.Optional });


            appBuilder.UseWebApi(config);
        }
    }
}
