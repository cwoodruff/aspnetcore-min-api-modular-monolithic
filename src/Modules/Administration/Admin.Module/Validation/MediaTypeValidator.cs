using FluentValidation;
using Admin.Modules.Models;

namespace Admin.Modules.Validation;

internal sealed class MediaTypeValidator : AbstractValidator<MediaTypeApiModel>
{
    public MediaTypeValidator()
    {
        RuleFor(m => m.Name).NotNull();
        RuleFor(m => m.Name).MaximumLength(120);
    }
}
