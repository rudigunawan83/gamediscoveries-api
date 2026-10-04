using FluentValidation;

namespace GameDiscoveries.Modules.Catalog.Features.ListGames;

public sealed class ListGamesValidator : AbstractValidator<ListGamesQuery>
{
    public ListGamesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(150);
        RuleFor(x => x.Platform).MaximumLength(50);
        RuleFor(x => x.Tag).MaximumLength(100);
        RuleFor(x => x.Sort)
            .Must(s => string.IsNullOrWhiteSpace(s)
                       || s is "newest" or "popular" or "trending" or "title")
            .WithMessage("Sort must be one of: newest, popular, trending, title.");
    }
}
