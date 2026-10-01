using Admin.Modules.Endpoints;
using FluentValidation;

namespace Admin.Modules.Validation;

/// <summary>Runs in ValidationFilter before the handler; GenreValidator in the service is the fallback.</summary>
internal sealed class CreateGenreRequestValidator : AbstractValidator<GenreEndpoints.CreateGenreRequest>
{
    public CreateGenreRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(120);
    }
}

internal sealed class UpdateGenreRequestValidator : AbstractValidator<GenreEndpoints.UpdateGenreRequest>
{
    public UpdateGenreRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(120);
    }
}
