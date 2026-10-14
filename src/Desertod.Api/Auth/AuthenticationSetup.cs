using System.Security.Claims;
using System.Threading.RateLimiting;
using AspNet.Security.OAuth.Twitch;
using Desertod.Api.Game;
using Desertod.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.RateLimiting;

namespace Desertod.Api.Auth;

public sealed record LoginProviders(bool Twitch, bool Google, string? TelegramBotUsername, bool Password, bool DevelopmentLogin);

public static class AuthenticationSetup
{
    public const string TwitchProvider = "twitch";
    public const string GoogleProvider = "google";
    public const string TelegramProvider = "telegram";
    public const string PasswordProvider = "email";
    public const string DevelopmentProvider = "dev";
    public const string PasswordRateLimit = "password";

    public static IServiceCollection AddDesertodAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var twitch = configuration.GetSection("Authentication:Twitch");
        var google = configuration.GetSection("Authentication:Google");
        var telegram = configuration.GetSection("Authentication:Telegram");
        services.Configure<TelegramOptions>(telegram);

        var twitchConfigured = !string.IsNullOrWhiteSpace(twitch["ClientId"]) && !string.IsNullOrWhiteSpace(twitch["ClientSecret"]);
        var googleConfigured = !string.IsNullOrWhiteSpace(google["ClientId"]) && !string.IsNullOrWhiteSpace(google["ClientSecret"]);
        var telegramOptions = telegram.Get<TelegramOptions>() ?? new TelegramOptions();
        var password = configuration.GetSection("Authentication:Password");
        services.Configure<PasswordLoginOptions>(password);
        var passwordOptions = password.Get<PasswordLoginOptions>() ?? new PasswordLoginOptions();

        services.AddSingleton(new LoginProviders(
            twitchConfigured,
            googleConfigured,
            telegramOptions.IsConfigured ? telegramOptions.BotUsername : null,
            passwordOptions.Enabled,
            environment.IsDevelopment()));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PasswordRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress.Of(http) ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, passwordOptions.AttemptsPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                }));
            options.AddPolicy(ChatService.RateLimit, http => RateLimitPartition.GetSlidingWindowLimiter(
                http.User.GetAccountId()?.ToString() ?? ClientAddress.Of(http) ?? "unknown",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromSeconds(10),
                    SegmentsPerWindow = 5,
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                var chat = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                           == ChatService.RateLimit;
                var problem = new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Detail = chat
                            ? "You are writing too fast. Wait a few seconds."
                            : "Too many attempts. Wait a minute and try again.",
                    },
                };
                problem.ProblemDetails.Extensions["code"] = chat ? "chat.tooFast" : "auth.tooManyAttempts";
                problem.ProblemDetails.Extensions["arguments"] = new Dictionary<string, object>();
                await problems.TryWriteAsync(problem);
            };
        });

        var authentication = services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "desertod";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        if (twitchConfigured)
        {
            authentication.AddTwitch(options =>
            {
                options.ClientId = twitch["ClientId"]!;
                options.ClientSecret = twitch["ClientSecret"]!;
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.Events.OnCreatingTicket = context => AttachAccountAsync(
                    context,
                    TwitchProvider,
                    context.Identity?.FindFirst(TwitchAuthenticationConstants.Claims.DisplayName)?.Value);
            });
        }

        if (googleConfigured)
        {
            authentication.AddGoogle(options =>
            {
                options.ClientId = google["ClientId"]!;
                options.ClientSecret = google["ClientSecret"]!;
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.Events.OnCreatingTicket = context => AttachAccountAsync(context, GoogleProvider, null);
            });
        }

        return services;
    }

    public static string? SchemeFor(string provider) => provider switch
    {
        TwitchProvider => TwitchAuthenticationDefaults.AuthenticationScheme,
        GoogleProvider => GoogleDefaults.AuthenticationScheme,
        _ => null,
    };

    private static async Task AttachAccountAsync(OAuthCreatingTicketContext context, string provider, string? preferredName)
    {
        var identity = context.Identity ?? throw new InvalidOperationException("Missing identity.");
        var providerUserId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? throw new InvalidOperationException($"{provider} did not return a user identifier.");
        var displayName = preferredName ?? identity.FindFirst(ClaimTypes.Name)?.Value ?? $"{provider}-user";

        var accounts = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
        var account = await accounts.ResolveExternalLoginAsync(
            provider, providerUserId, displayName, context.HttpContext.RequestAborted);

        identity.AddClaim(new Claim(AccountClaims.AccountId, account.Id.ToString()));
        identity.AddClaim(new Claim(AccountClaims.Provider, provider));
    }
}
