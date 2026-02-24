using Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

var masterUrl = Environment.GetEnvironmentVariable("MASTER_URL") ?? "http://localhost:8080";

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
