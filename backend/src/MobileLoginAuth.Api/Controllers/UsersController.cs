using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileLoginAuth.Api.Extensions;
using MobileLoginAuth.Application.Dtos;
using MobileLoginAuth.Application.Services;

namespace MobileLoginAuth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me")]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    [HttpGet]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        var result = await _users.GetAsync(userId, ct);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("login-history")]
    [ProducesResponseType(typeof(IReadOnlyList<LoginHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginHistory([FromQuery] int take = 20, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        return Ok(await _users.GetLoginHistoryAsync(userId, Math.Clamp(take, 1, 100), ct));
    }

    /// <summary>Deletes the account and all its data (required for App Store apps with sign-up).</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        var result = await _users.DeleteAsync(userId, ct);
        return result.Succeeded ? NoContent() : this.ToProblem(result);
    }
}
