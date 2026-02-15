using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NoMercy.NmSystem.Information;

namespace NoMercy.Setup;

public class TempServer
{
    public static WebApplication Start()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        WebApplication app = builder.Build();
        app.Urls.Add("http://0.0.0.0:" + Config.InternalServerPort);
        app.Run(async context =>
        {
            string code = context.Request.Query["code"].ToString();

            context.Response.Headers.Append("Content-Type", "text/html");
            await context.Response.WriteAsync("<script>window.close();</script>");
            await context.Response.CompleteAsync();

            await Auth.TokenByAuthorizationCode(code);
        });

        return app;
    }
}
