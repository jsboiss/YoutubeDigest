using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;
using YoutubeDigest.Components;
using YoutubeDigest.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (port is not null)
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
    .SetApplicationName("YoutubeDigest");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
var googleConfigured = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);

var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

if (googleConfigured)
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId!;
        options.ClientSecret = googleClientSecret!;
        options.Scope.Add("https://www.googleapis.com/auth/youtube.readonly");
        options.SaveTokens = true;
        options.Events.OnCreatingTicket = ctx =>
        {
            if (ctx.AccessToken is not null)
                ctx.Identity!.AddClaim(new Claim("access_token", ctx.AccessToken));
            return Task.CompletedTask;
        };
    });
}

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMudServices();
builder.Services.AddHttpClient("YouTube");
builder.Services.AddHttpClient("Cerebras");
builder.Services.AddScoped<YouTubeService>();
builder.Services.AddScoped<CerebrasService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddSingleton<SummaryCache>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

if (!app.Environment.IsDevelopment())
{
    app.Use((context, next) =>
    {
        context.Request.Scheme = "https";
        return next();
    });
}
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/auth/login", (string? returnUrl) =>
    googleConfigured
        ? Results.Challenge(
            new AuthenticationProperties { RedirectUri = returnUrl ?? "/dashboard" },
            [GoogleDefaults.AuthenticationScheme])
        : Results.Problem("Google OAuth is not configured. Add Google:ClientId and Google:ClientSecret to your app settings."));

app.MapGet("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
