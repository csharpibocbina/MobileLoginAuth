using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MobileLoginAuth.Api.Controllers;
using MobileLoginAuth.Application.Common;
using MobileLoginAuth.Application.Dtos;
using MobileLoginAuth.Application.Services;
using MobileLoginAuth.Application.Validators;

namespace MobileLoginAuth.UnitTests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _auth = new();
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        var services = new ServiceCollection().AddMvcCore().Services.AddProblemDetails().BuildServiceProvider();
        _sut = new AuthController(_auth.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } },
            ProblemDetailsFactory = services.GetRequiredService<ProblemDetailsFactory>()
        };
    }

    private static readonly AuthResponse SampleResponse = new(
        "access", "refresh", DateTime.UtcNow.AddMinutes(15), new UserDto(Guid.NewGuid(), "jay@example.com", "Jay", DateTime.UtcNow));

    // UT-42
    [Fact]
    public async Task Login_ReturnsOk_OnSuccess()
    {
        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponse>.Success(SampleResponse));

        var result = await _sut.Login(new LoginRequest("jay@example.com", "Passw0rd!"), new LoginRequestValidator(), default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(SampleResponse);
    }

    // UT-43
    [Fact]
    public async Task Login_Returns401_OnFailure()
    {
        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponse>.Failure(ErrorType.Unauthorized, AuthService.InvalidCredentialsMessage));

        var result = await _sut.Login(new LoginRequest("jay@example.com", "wrong"), new LoginRequestValidator(), default);

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Login_Returns423_WhenLockedOut()
    {
        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponse>.Failure(ErrorType.Locked, AuthService.LockedOutMessage));

        var result = await _sut.Login(new LoginRequest("jay@example.com", "x"), new LoginRequestValidator(), default);

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status423Locked);
    }

    [Fact]
    public async Task Login_ReturnsValidationProblem_AndDoesNotCallService_ForInvalidEmail()
    {
        var result = await _sut.Login(new LoginRequest("bad", "x"), new LoginRequestValidator(), default);

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _auth.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Register_Returns409_WhenEmailExists()
    {
        _auth.Setup(a => a.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserDto>.Failure(ErrorType.Conflict, "exists"));

        var result = await _sut.Register(new RegisterRequest("jay@example.com", "Passw0rd!", "Jay"), new RegisterRequestValidator(), default);

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    // UT-44
    [Fact]
    public void Logout_RequiresAuthorization()
    {
        typeof(AuthController).GetMethod(nameof(AuthController.Logout))!
            .GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
    }
}
