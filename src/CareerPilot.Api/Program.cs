using System.Security.Claims;
using System.Text.Json.Serialization;
using CareerPilot.Api;
using CareerPilot.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<CareerPilotDbContext>("sqlite", tags: ["ready"]);
builder.Services.AddCareerPilotInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(githubClientId))
{
    builder.Services.AddAuthentication(DevelopmentAuthHandler.Scheme)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthHandler>(DevelopmentAuthHandler.Scheme, _ => { });
}
else
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "GitHub";
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-CareerPilot";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    })
    .AddOAuth("GitHub", options =>
    {
        options.ClientId = githubClientId ?? throw new InvalidOperationException("GitHub OAuth client ID is required in production.");
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"]
            ?? throw new InvalidOperationException("GitHub OAuth client secret is required in production.");
        options.CallbackPath = "/signin-github";
        options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        options.TokenEndpoint = "https://github.com/login/oauth/access_token";
        options.UserInformationEndpoint = "https://api.github.com/user";
        options.Scope.Add("read:user");
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        options.Events.OnCreatingTicket = GitHubAuthentication.CreateTicketAsync;
    });
}
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseExceptionHandler(exceptionApp => exceptionApp.Run(ApiErrors.WriteAsync));
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CareerPilotDbContext>();
    await db.Database.MigrateAsync();
    await db.ConfigureSqliteAsync();
}

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });
app.MapCareerPilotApi();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;
