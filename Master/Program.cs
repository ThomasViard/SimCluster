using Master.Services;

Console.WriteLine("Hello, World!");

try
{
    var server = new HttpServer("http://localhost:8080/");
    await server.StartAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to start server: {ex.Message}");
}
