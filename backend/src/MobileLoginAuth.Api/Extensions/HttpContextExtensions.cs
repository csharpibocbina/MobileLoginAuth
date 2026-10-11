using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MobileLoginAuth.Application.Common;

namespace MobileLoginAuth.Api.Extensions;

public static class HttpContextExtensions
{
    public const string DeviceHeader = "X-Device-Info";

    public static ClientInfo GetClientInfo(this HttpContext context)
    {
        var device = context.Request.Headers[DeviceHeader].FirstOrDefault()
                     ?? context.Request.Headers.UserAgent.FirstOrDefault();
        // Behind the reverse proxy, UseForwardedHeaders has already put the real client IP here.
        var ip = context.Connection.RemoteIpAddress?.ToString();
        return new ClientInfo(ip, device is { Length: > 200 } ? device[..200] : device);
    }

    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
}
