using Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

var masterUrl = Environment.GetEnvironmentVariable("MASTER_URL") ?? "http://localhost:8080";
var grafanaUrl = Environment.GetEnvironmentVariable("GRAFANA_URL") ?? "http://localhost:3001";

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton(new GrafanaSettings { BaseUrl = grafanaUrl });
builder.Services.AddHttpClient("grafana", client =>
{
    client.BaseAddress = new Uri(grafanaUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<ClusterApiService>(client =>
{
    client.BaseAddress = new Uri(masterUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

// Reverse proxy: /grafana/* -> Grafana internal service
// This allows the Blazor frontend (browser) to access Grafana panels
// even though Grafana is only accessible internally in K8s (ClusterIP)
app.Map("/grafana/{**path}", async (HttpContext ctx, IHttpClientFactory httpFactory) =>
{
    var path = ctx.Request.RouteValues["path"]?.ToString() ?? "";
    var query = ctx.Request.QueryString;
    var client = httpFactory.CreateClient("grafana");

    var targetUri = new Uri(client.BaseAddress!, $"/{path}{query}");

    var requestMessage = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), targetUri);

    // Forward relevant headers
    foreach (var header in ctx.Request.Headers)
    {
        if (header.Key.StartsWith("Accept", StringComparison.OrdinalIgnoreCase) ||
            header.Key.StartsWith("Content", StringComparison.OrdinalIgnoreCase))
        {
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    if (ctx.Request.ContentLength > 0)
    {
        requestMessage.Content = new StreamContent(ctx.Request.Body);
    }

    try
    {
        var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

        ctx.Response.StatusCode = (int)response.StatusCode;

        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        // Override X-Frame-Options to allow embedding
        ctx.Response.Headers.Remove("X-Frame-Options");
        ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

        await response.Content.CopyToAsync(ctx.Response.Body);
    }
    catch
    {
        ctx.Response.StatusCode = 502;
        await ctx.Response.WriteAsync("Grafana unavailable");
    }
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
