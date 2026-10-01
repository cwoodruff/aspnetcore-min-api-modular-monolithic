namespace SharedKernel.Validation;

/// <summary>
/// Validates a request body for <see cref="ValidationFilter{TRequest}" />. SharedKernel does not know which
/// validation library a module uses (ADR-0012); a module adapts its own validators to this.
/// </summary>
public interface IRequestValidator<in TRequest>
{
    /// <returns>Errors by property name; empty when the request is valid.</returns>
    Task<IDictionary<string, string[]>> ValidateAsync(TRequest request, CancellationToken ct);
}
