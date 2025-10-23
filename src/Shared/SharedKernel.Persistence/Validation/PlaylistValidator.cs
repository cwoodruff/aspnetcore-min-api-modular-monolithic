using FluentValidation;
using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Validation;

public class PlaylistValidator : AbstractValidator<PlaylistApiModel>
{
    public PlaylistValidator()
    {
        RuleFor(p => p.Name).NotNull();
        RuleFor(p => p.Name).MaximumLength(120);
    }
}