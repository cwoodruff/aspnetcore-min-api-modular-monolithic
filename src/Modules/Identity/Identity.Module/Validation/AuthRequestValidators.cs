using Identity.Modules.Endpoints;
using SharedKernel.Validation;

namespace Identity.Modules.Validation;

/// <summary>
/// The identity request bodies' rules, for <see cref="ValidationFilter{TRequest}" />: every field is required.
/// Written by hand; the rules are too small to bring FluentValidation into this module.
/// </summary>
internal sealed class AuthRequestValidators :
    IRequestValidator<AuthHandlers.LoginRequest>,
    IRequestValidator<AuthHandlers.RefreshRequest>,
    IRequestValidator<AuthHandlers.LogoutRequest>
{
    public Task<IDictionary<string, string[]>> ValidateAsync(AuthHandlers.LoginRequest request, CancellationToken ct) =>
        Task.FromResult(Errors(("username", request.Username), ("password", request.Password)));

    public Task<IDictionary<string, string[]>> ValidateAsync(AuthHandlers.RefreshRequest request, CancellationToken ct) =>
        Task.FromResult(Errors(("userId", request.UserId), ("refreshToken", request.RefreshToken)));

    public Task<IDictionary<string, string[]>> ValidateAsync(AuthHandlers.LogoutRequest request, CancellationToken ct) =>
        Task.FromResult(Errors(("userId", request.UserId), ("refreshToken", request.RefreshToken)));

    /// <summary>One error per blank field, keyed by its wire name.</summary>
    internal static IDictionary<string, string[]> Errors(params (string Field, string? Value)[] fields) =>
        fields.Where(field => string.IsNullOrWhiteSpace(field.Value))
            .ToDictionary(field => field.Field, field => new[] { $"'{field.Field}' is required." });
}
