using FluentValidation;
using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Validation;

public class ArtistValidator : AbstractValidator<ArtistApiModel>
{
    public ArtistValidator()
    {
        RuleFor(a => a.Name).NotNull();
        RuleFor(a => a.Name).MaximumLength(120);
    }
}
