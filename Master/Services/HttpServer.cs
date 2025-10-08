using System.Net;
using System.Threading.Tasks;
using Master.Controllers;

namespace Master.Services;

public class HttpServer
{
    private readonly HttpListener _listener;
    private readonly RouteManager _routeManager;

    public HttpServer(string url)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add(url);
        _routeManager = new RouteManager(new MasterController());
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine($"[Master] Server started on {_listener.Prefixes.First()}");
        Console.WriteLine("Press Ctrl+C to stop.");

        while (_listener.IsListening)
        {
            var context = await _listener.GetContextAsync();
            _ = Task.Run(() => _routeManager.HandleRequest(context));
        }
    }

    public void Stop()
    {
        _listener.Stop();
    }
}
