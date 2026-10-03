using FluentValidation;

namespace GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;

public sealed class GetGameBySlugValidator : AbstractValidator<GetGameBySlugQuery>
{
    public GetGameBySlugValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(200)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with hyphens only.");
    }
}
