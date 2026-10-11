using FluentAssertions;
using MobileLoginAuth.Application.Dtos;
using MobileLoginAuth.Application.Validators;

namespace MobileLoginAuth.UnitTests.Validators;

public class RequestValidatorTests
{
    // UT-40
    [Theory]
    [InlineData("", "Passw0rd!")]
    [InlineData("not-an-email", "Passw0rd!")]
    [InlineData("jay@example.com", "")]
    public void LoginRequestValidator_RejectsInvalidInput(string email, string password)
    {
        new LoginRequestValidator().Validate(new LoginRequest(email, password)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void LoginRequestValidator_AcceptsValidInput()
    {
        new LoginRequestValidator().Validate(new LoginRequest("jay@example.com", "x")).IsValid.Should().BeTrue();
    }

    // UT-41
    [Theory]
    [InlineData("Short1")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    public void RegisterRequestValidator_RejectsWeakPassword(string password)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("jay@example.com", password, "Jay"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void RegisterRequestValidator_AcceptsStrongPasswordAndRequiresName()
    {
        var validator = new RegisterRequestValidator();

        validator.Validate(new RegisterRequest("jay@example.com", "Passw0rd!", "Jay")).IsValid.Should().BeTrue();
        validator.Validate(new RegisterRequest("jay@example.com", "Passw0rd!", "")).IsValid.Should().BeFalse();
    }
}
