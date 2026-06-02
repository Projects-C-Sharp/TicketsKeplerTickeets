using System.Security.Claims;
using TicketsKeplerTickets.Services;
using Microsoft.AspNetCore.Authentication;

namespace TicketsKeplerTickets.Middleware;

/// <summary>
/// Intercepts every request: if the stored AccessToken is expired (or close to it),
/// calls api/auth/refresh and re-issues the auth cookie with fresh tokens.
/// This runs before controllers, so all downstream code gets a valid token.
/// </summary>
public class TokenRefreshMiddleware
{
    private readonly RequestDelegate _next;

    public TokenRefreshMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Only for authenticated users
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            var accessToken  = ctx.User.FindFirstValue("AccessToken")  ?? "";
            var refreshToken = ctx.User.FindFirstValue("RefreshToken") ?? "";

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                // Check if the token is expired or expiring within the next 3 minutes
                if (IsExpiredOrExpiringSoon(accessToken, minutesBuffer: 3))
                {
                    var api = ctx.RequestServices.GetRequiredService<IApiService>();
                    var result = await api.RefreshTokenAsync(refreshToken);

                    if (result != null &&
                        !string.IsNullOrEmpty(result.AccessToken) &&
                        !string.IsNullOrEmpty(result.RefreshToken))
                    {
                        // Re-build claims with fresh tokens
                        var claims = ctx.User.Claims
                            .Where(c => c.Type != "AccessToken" && c.Type != "RefreshToken")
                            .ToList();

                        claims.Add(new Claim("AccessToken",  result.AccessToken));
                        claims.Add(new Claim("RefreshToken", result.RefreshToken));

                        var identity  = new ClaimsIdentity(claims, "KeplerCookies");
                        var principal = new ClaimsPrincipal(identity);

                        // Re-issue the auth cookie
                        await ctx.SignInAsync("KeplerCookies", principal, new AuthenticationProperties
                        {
                            IsPersistent    = true,
                            ExpiresUtc      = DateTimeOffset.UtcNow.AddHours(8),
                        });

                        // Replace the current user so controllers see the new token
                        ctx.User = principal;
                    }
                    else
                    {
                        // Refresh token is also expired → sign out gracefully
                        // (only for non-API requests to avoid redirect loops)
                        if (!ctx.Request.Path.StartsWithSegments("/api") &&
                            !IsAjaxRequest(ctx))
                        {
                            await ctx.SignOutAsync("KeplerCookies");
                            ctx.Response.Redirect("/Auth/Login?reason=session_expired");
                            return;
                        }
                    }
                }
            }
        }

        await _next(ctx);
    }

    private static bool IsExpiredOrExpiringSoon(string token, int minutesBuffer)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(token);
            return jwt.ValidTo <= DateTime.UtcNow.AddMinutes(minutesBuffer);
        }
        catch
        {
            return true; // If we can't read it, assume expired
        }
    }

    private static bool IsAjaxRequest(HttpContext ctx) =>
        ctx.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
        ctx.Request.Headers["Content-Type"].ToString().Contains("application/json");
}
