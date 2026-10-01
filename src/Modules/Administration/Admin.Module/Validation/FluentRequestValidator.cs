using FluentValidation;
using SharedKernel.Validation;

namespace Admin.Modules.Validation;

/// <summary>Serves the module's FluentValidation validators to SharedKernel's ValidationFilter.</summary>
internal sealed class FluentRequestValidator<TRequest>(IValidator<TRequest> validator) : IRequestValidator<TRequest>
{
    public async Task<IDictionary<string, string[]>> ValidateAsync(TRequest request, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        return result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
    }
}
