using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Validation;

/// <summary>
/// Validates the endpoint's <typeparamref name="TRequest" /> argument before the handler runs and answers
/// 400 with the same RFC 7807 body the host writes for a ValidationException (title, detail, type, errors,
/// traceId). Services still validate and throw; that path is now the fallback. Fails closed: an endpoint
/// that asks for validation of a type nobody can validate is a 500, not a skipped check.
/// </summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    public const string Title = "Request validation failed.";
    public const string Detail = "One or more validation errors occurred.";
    public const string Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            // Nothing bound (for example a null body); the handler and the service decide.
            return await next(context);
        }

        var validator = context.HttpContext.RequestServices.GetService<IRequestValidator<TRequest>>()
                        ?? throw new InvalidOperationException(
                            $"No IRequestValidator<{typeof(TRequest).Name}> is registered for {context.HttpContext.Request.Path}.");

        var errors = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (errors.Count == 0)
        {
            return await next(context);
        }

        return TypedResults.ValidationProblem(errors, detail: Detail, title: Title, type: Type,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.HttpContext.TraceIdentifier });
    }
}
