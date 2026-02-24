using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Master.Services;

public class DockerScalingService
{
    private readonly HttpClient _client;
    private readonly string _workerImage;
    private readonly string _networkName;
    private readonly List<string> _dynamicContainerIds = [];
    private int _counter;

    public DockerScalingService()
    {
        var socketPath = Environment.GetEnvironmentVariable("DOCKER_SOCKET") ?? "/var/run/docker.sock";
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };
        _client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        _workerImage = Environment.GetEnvironmentVariable("WORKER_IMAGE") ?? "simcluster-worker:dev";
        _networkName = Environment.GetEnvironmentVariable("DOCKER_NETWORK") ?? "simcluster-net";
    }

    public async Task<string?> AddWorkerAsync()
    {
        try
        {
            var name = $"simcluster-worker-dyn-{++_counter}";
            var body = new
            {
                Image = _workerImage,
                Env = new[] { "ASPNETCORE_ENVIRONMENT=Development", "MASTER_URL=http://simcluster-master:8080" },
                HostConfig = new
                {
                    RestartPolicy = new { Name = "unless-stopped" }
                },
                NetworkingConfig = new
                {
                    EndpointsConfig = new Dictionary<string, object>
                    {
                        [_networkName] = new { }
                    }
                }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _client.PostAsync($"/v1.47/containers/create?name={name}", content);
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"Docker create failed: {await resp.Content.ReadAsStringAsync()}");
                return null;
            }

            var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var containerId = result.GetProperty("Id").GetString()!;

            var startResp = await _client.PostAsync($"/v1.47/containers/{containerId}/start", null);
            if (!startResp.IsSuccessStatusCode)
            {
                Console.WriteLine($"Docker start failed: {await startResp.Content.ReadAsStringAsync()}");
                return null;
            }

            _dynamicContainerIds.Add(containerId);
            Console.WriteLine($"Dynamic worker container {containerId[..12]} created");
            return containerId[..12];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AddWorker error: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> RemoveWorkerAsync()
    {
        if (_dynamicContainerIds.Count == 0) return null;

        try
        {
            var containerId = _dynamicContainerIds[^1];
            await _client.PostAsync($"/v1.47/containers/{containerId}/stop", null);
            var resp = await _client.DeleteAsync($"/v1.47/containers/{containerId}");

            if (resp.IsSuccessStatusCode || (int)resp.StatusCode == 404)
            {
                _dynamicContainerIds.RemoveAt(_dynamicContainerIds.Count - 1);
                Console.WriteLine($"Dynamic worker container {containerId[..12]} removed");
                return containerId[..12];
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RemoveWorker error: {ex.Message}");
            return null;
        }
    }

    public int DynamicWorkerCount => _dynamicContainerIds.Count;
}
