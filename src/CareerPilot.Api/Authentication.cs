using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;

namespace CareerPilot.Api;

public sealed class DevelopmentAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "Development";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "local"), new(ClaimTypes.Name, "Local developer")], Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}

public static class GitHubAuthentication
{
    public static async Task CreateTicketAsync(OAuthCreatingTicketContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        request.Headers.UserAgent.ParseAdd("CareerPilot/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
        var id = user.RootElement.GetProperty("id").GetInt64().ToString();
        var allowed = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Authentication:GitHub:AllowedUserId"];
        if (!string.Equals(id, allowed, StringComparison.Ordinal))
            throw new AuthenticationFailureException("This GitHub account is not authorised for CareerPilot.");
        context.RunClaimActions(user.RootElement);
    }
}

public static class ApiErrors
{
    public static async Task WriteAsync(HttpContext context)
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var exception = feature?.Error;
        var status = exception is InvalidOperationException or ArgumentException ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
        context.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: status == 400 ? exception?.Message : "An unexpected error occurred.").ExecuteAsync(context);
    }
}
