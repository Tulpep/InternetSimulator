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
                Console.WriteLine(ctx.Request.Host.ToString() + ctx.Request.Path.ToString());
        
                await next();
            });
        }

    }
}
