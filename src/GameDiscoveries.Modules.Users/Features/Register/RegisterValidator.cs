using FluentValidation;

namespace GameDiscoveries.Modules.Users.Features.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(40);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(200)
            .Matches("[A-Za-z]").WithMessage("Password must include a letter.")
            .Matches("[0-9]").WithMessage("Password must include a number.");
    }
}
