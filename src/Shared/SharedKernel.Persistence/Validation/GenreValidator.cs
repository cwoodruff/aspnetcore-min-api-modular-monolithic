using FluentValidation;
using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Validation;

public class GenreValidator : AbstractValidator<GenreApiModel>
{
    public GenreValidator()
    {
        RuleFor(g => g.Name).NotNull();
        RuleFor(g => g.Name).MaximumLength(120);
    }
}
