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
        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                 ?? context.Connection.RemoteIpAddress?.ToString();
        return new ClientInfo(ip, device is { Length: > 200 } ? device[..200] : device);
    }

    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
}
