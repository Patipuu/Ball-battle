using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.RateLimiting;
using BallBattle.PvpServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.IncludeFields = true);
// Storage: DATABASE_URL (Render + Neon Postgres, one row) or a JSON file (PVP_DATA_PATH, VM / local).
// House bots (PvpBots) fill the offers until enough real players have published a defense; PVP_BOTS=0 turns them off.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
IPvpPersistence persistence = !string.IsNullOrWhiteSpace(databaseUrl)
    ? new PgPersistence(databaseUrl)
    : new FilePersistence(Environment.GetEnvironmentVariable("PVP_DATA_PATH") ?? Path.Combine(builder.Environment.ContentRootPath, "pvp-data.json"));
builder.Services.AddSingleton(new PvpStore(persistence, Environment.GetEnvironmentVariable("PVP_BOTS") != "0"));

// Render (and similar hosts) tell the app which port to listen on.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port)) builder.WebHost.UseUrls("http://0.0.0.0:" + port);
var behindPlatformProxy = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RENDER"));

// The WebGL build on itch.io runs inside an iframe on itch's game CDN, so the browser sends a cross-origin request
// (with an Authorization header, so a preflight too). Allow those origins plus any listed in PVP_CORS_ORIGINS.
var extraOrigins = (Environment.GetEnvironmentVariable("PVP_CORS_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(origin => CorsRules.Allowed(origin, extraOrigins))
    .AllowAnyHeader().AllowAnyMethod().SetPreflightMaxAge(TimeSpan.FromHours(1))));

// Behind Caddy on the same machine: trust X-Forwarded-For from loopback only, so rate limits see the player's IP.
// On Render: trust its load balancer instead.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    if (behindPlatformProxy)
    {
        // Render's load balancer is the only way in, from addresses we cannot list: trust its X-Forwarded-For.
        options.KnownIPNetworks.Clear();
        options.ForwardLimit = 1;
    }
    else
    {
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
    }
});

// Per-IP limits. A new account per request is the cheapest abuse, so it gets its own tight window.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync("{\"error\":\"TOO MANY REQUESTS - TRY AGAIN SOON\"}", token);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(Ip(http), _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("accounts", http =>
        RateLimitPartition.GetFixedWindowLimiter(Ip(http), _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));   // phones behind one carrier NAT share an IP
    // Sign-in and sign-up: enough for typos, too few to guess passwords.
    options.AddPolicy("auth", http =>
        RateLimitPartition.GetFixedWindowLimiter(Ip(http), _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseCors();
app.UseRateLimiter();

// Deploy behind HTTPS (deploy/Caddyfile). The bearer token is an account credential and must never travel over public HTTP.
app.MapGet("/health", () => Results.Ok(new { status = "ok", protocol = 1 }));
app.MapPost("/v1/accounts", (PvpStore store) => Safe(store.Register)).RequireRateLimiting("accounts");

app.MapPost("/v1/auth/register", (HttpRequest request, PvpStore store, AuthRequest body) =>
    // An anonymous account signed in on this device (old PvP token) is upgraded instead of a new one being made.
    Safe(() => store.RegisterNamed(body, store.Authenticate(request.Headers.Authorization)))).RequireRateLimiting("auth");
app.MapPost("/v1/auth/login", (PvpStore store, AuthRequest body) => Safe(() => store.Login(body))).RequireRateLimiting("auth");
app.MapPost("/v1/auth/logout", (HttpRequest request, PvpStore store) => Authorized(request, store, id => store.Logout(id, request.Headers.Authorization)));
app.MapGet("/v1/me/save", (HttpRequest request, PvpStore store) => Authorized(request, store, store.GetSave));
app.MapPut("/v1/me/save", (HttpRequest request, PvpStore store, SaveRequest body) => Authorized(request, store, id => store.PutSave(id, body)));
app.MapGet("/v1/me", (HttpRequest request, PvpStore store) => Authorized(request, store, store.Me));
app.MapPut("/v1/me/name", (HttpRequest request, PvpStore store, NameRequest body) => Authorized(request, store, id => store.Rename(id, body)));
app.MapPut("/v1/me/defense", (HttpRequest request, PvpStore store, PublishRequest body) => Authorized(request, store, id => store.Publish(id, body)));
app.MapGet("/v1/offers", (HttpRequest request, PvpStore store) => Authorized(request, store, store.Offers));
app.MapPost("/v1/challenges", (HttpRequest request, PvpStore store, ChooseRequest body) => Authorized(request, store, id => store.Choose(id, body)));
app.MapPost("/v1/challenges/{challengeId}/ban", (HttpRequest request, PvpStore store, string challengeId, BanRequest body) =>
    Authorized(request, store, id => store.Ban(id, challengeId, body)));
app.MapPost("/v1/challenges/{challengeId}/pick", (HttpRequest request, PvpStore store, string challengeId, PickRequest body) =>
    Authorized(request, store, id => store.Pick(id, challengeId, body)));
app.MapGet("/v1/challenges/{challengeId}", (HttpRequest request, PvpStore store, string challengeId) =>
    Authorized(request, store, id => store.GetChallenge(id, challengeId)));
app.MapGet("/v1/history", (HttpRequest request, PvpStore store) => Authorized(request, store, store.History));

app.Run();

static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static IResult Authorized(HttpRequest request, PvpStore store, Func<string, object> action)
{
    var id = store.Authenticate(request.Headers.Authorization);
    return id == null ? Results.Unauthorized() : Safe(() => action(id));
}

static IResult Safe(Func<object> action)
{
    try { return Results.Ok(action()); }
    catch (ArgumentException ex)
    {
        // Players see this line in the game: drop .NET's " (Parameter 'x')" suffix.
        var message = ex.ParamName == null ? ex.Message : ex.Message.Replace($" (Parameter '{ex.ParamName}')", "");
        return Results.BadRequest(new { error = message });
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
}
