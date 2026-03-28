using MudBlazor.Services;
using YoutubeDigest.Components;
using YoutubeDigest.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (port is not null)
    builder.WebHost.UseUrls($"http://*:{port}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddHttpClient("YouTube");
builder.Services.AddHttpClient("Cerebras");
builder.Services.AddScoped<YouTubeService>();
builder.Services.AddScoped<CerebrasService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
