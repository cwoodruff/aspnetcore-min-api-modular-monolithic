using FluentValidation;
using Admin.Modules.Models;

namespace Admin.Modules.Validation;

internal sealed class GenreValidator : AbstractValidator<GenreApiModel>
{
    public GenreValidator()
    {
        RuleFor(g => g.Name).NotNull();
        RuleFor(g => g.Name).MaximumLength(120);
    }
}
