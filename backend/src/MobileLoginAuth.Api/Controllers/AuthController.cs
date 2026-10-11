using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileLoginAuth.Api.Extensions;
using MobileLoginAuth.Application.Dtos;
using MobileLoginAuth.Application.Services;

namespace MobileLoginAuth.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, [FromServices] IValidator<RegisterRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return this.ValidationProblem(validation);

        var result = await _auth.RegisterAsync(request, ct);
        return result.Succeeded ? Created("/api/users/me", result.Value) : this.ToProblem(result);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, [FromServices] IValidator<LoginRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return this.ValidationProblem(validation);

        var result = await _auth.LoginAsync(request, HttpContext.GetClientInfo(), ct);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request, [FromServices] IValidator<RefreshRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return this.ValidationProblem(validation);

        var result = await _auth.RefreshAsync(request, HttpContext.GetClientInfo(), ct);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();

        var result = await _auth.LogoutAsync(userId, request, HttpContext.GetClientInfo(), ct);
        return result.Succeeded ? NoContent() : this.ToProblem(result);
    }
}
