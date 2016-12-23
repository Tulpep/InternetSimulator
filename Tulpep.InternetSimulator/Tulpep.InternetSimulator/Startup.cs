using Owin;
using System;

namespace Tulpep.InternetSimulator
{
    class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.Use(async (ctx, next) =>
            {
                string url = ctx.Request.Host.ToString() + ctx.Request.Path.ToString();
                Console.WriteLine(url);
                await next();
            });
        }

    }
}
