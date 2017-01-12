using Microsoft.Owin;
using System;
using System.Threading.Tasks;

public class CustomExceptionMiddleware : OwinMiddleware
{
    public CustomExceptionMiddleware(OwinMiddleware next) : base(next)
    { }

    public override async Task Invoke(IOwinContext context)
    {
        try
        {
            await Next.Invoke(context);
        }
        catch (Exception ex)
        {
            Console.WriteLine("FALLO");
            // Custom stuff here
        }
    }
}