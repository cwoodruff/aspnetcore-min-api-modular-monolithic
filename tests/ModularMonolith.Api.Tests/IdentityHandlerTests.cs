using System.Security.Claims;
using FluentAssertions;
using Identity.Modules.Endpoints;
using Identity.Modules.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;

namespace ModularMonolith.Api.Tests;

/// <summary>Identity's handlers called directly: no host, substituted services.</summary>
public class IdentityHandlerTests
{
    [Fact]
    public async Task Login_WithBadCredentials_ReturnsUnauthorized()
    {
        var result = await AuthHandlers.Login(new AuthHandlers.LoginRequest("demo", "wrong"), new RejectingUserStore(),
            new UnusedTokenService(), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Login_WithBlankInput_ReturnsTheValidationProblemShape()
    {
        var result = await AuthHandlers.Login(new AuthHandlers.LoginRequest(" ", ""), new RejectingUserStore(),
            new UnusedTokenService(), NullLoggerFactory.Instance, CancellationToken.None);

        var problem = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        problem.StatusCode.Should().Be(400);
        problem.ProblemDetails.Title.Should().Be(SharedKernel.Validation.ValidationFilter<object>.Title);
        problem.ProblemDetails.Errors.Keys.Should().BeEquivalentTo("username", "password");
    }

    [Fact]
    public void UserInfo_MapsTheCallersClaims()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"), new Claim(ClaimTypes.Role, "Admin"), new Claim("permissions", "catalog.read")
        ], "test"));

        var info = AuthHandlers.UserInfo(user).Value!;

        info.Sub.Should().Be("user-1");
        info.Roles.Should().Equal("Admin");
        info.Permissions.Should().Equal("catalog.read");
    }

    // Hand-written fakes: the interfaces are internal (see Module.Tests' HandlerTests).
    private sealed class RejectingUserStore : IUserStore
    {
        public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string? email,
            string? tenant)> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default) =>
            Task.FromResult((false, string.Empty, (string?)null, Array.Empty<string>(), Array.Empty<string>(), (string?)null, (string?)null));

        public Task<(bool found, string? displayName, string[] roles, string[] permissions, string? email, string? tenant)>
            GetUserByIdAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class UnusedTokenService : ITokenService
    {
        public Task<TokenPair> IssueAsync(string userId, string? displayName, string[] roles, string[] permissions,
            string? email, string? tenant, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<TokenPair?> RefreshAsync(string userId, string refreshToken, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
