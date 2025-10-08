using System.Net;
using System.Text;
using Master.Controllers;

namespace Master.Services;

public class RouteManager(MasterController masterController)
{
    private readonly MasterController _masterController = masterController;

    private string GetRouteResponse(string path)
    {
        return path switch
        {
            "/ping" => _masterController.GetPingResponse(),
            _ => "Not Found"
        };
    }

    public async Task HandleRequest(HttpListenerContext context)
    {
        try
        {
            string path = context.Request.Url?.AbsolutePath ?? "/";
            Console.WriteLine($"Received: {path}");

            string responseString = GetRouteResponse(path);
            if (responseString == "Not Found")
            {
                context.Response.StatusCode = 404;
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
        }
    }
}
