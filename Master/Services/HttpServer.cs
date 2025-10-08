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

    public void Start()
    {
        _listener.Start();
        Console.WriteLine($"[Master] Server started on {_listener.Prefixes.First()}");
        Console.WriteLine("Press Ctrl+C to stop.");

        while (true)
        {
            var context = _listener.GetContext();
            Task.Run(() => _routeManager.HandleRequest(context));
        }
    }

    public void Stop()
    {
        _listener.Stop();
    }
}
