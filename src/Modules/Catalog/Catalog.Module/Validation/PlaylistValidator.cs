using FluentValidation;
using Catalog.Modules.Models;

namespace Catalog.Modules.Validation;

internal sealed class PlaylistValidator : AbstractValidator<PlaylistApiModel>
{
    public PlaylistValidator()
    {
        RuleFor(p => p.Name).NotNull();
        RuleFor(p => p.Name).MaximumLength(120);
    }
}
