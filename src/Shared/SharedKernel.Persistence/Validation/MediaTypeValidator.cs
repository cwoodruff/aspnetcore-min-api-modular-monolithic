using FluentValidation;
using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Validation;

public class MediaTypeValidator : AbstractValidator<MediaTypeApiModel>
{
    public MediaTypeValidator()
    {
        RuleFor(m => m.Name).NotNull();
        RuleFor(m => m.Name).MaximumLength(120);
    }
}
