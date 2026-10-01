using FluentValidation;
using Catalog.Modules.Models;

namespace Catalog.Modules.Validation;

internal sealed class ArtistValidator : AbstractValidator<ArtistApiModel>
{
    public ArtistValidator()
    {
        RuleFor(a => a.Name).NotNull();
        RuleFor(a => a.Name).MaximumLength(120);
    }
}
