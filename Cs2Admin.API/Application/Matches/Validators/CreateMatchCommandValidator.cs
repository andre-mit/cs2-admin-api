using Cs2Admin.API.Application.Matches.Commands;
using FluentValidation;

namespace Cs2Admin.API.Application.Matches.Validators;

public sealed class CreateMatchCommandValidator : AbstractValidator<CreateMatchCommand>
{
    public CreateMatchCommandValidator()
    {
        RuleFor(x => x.Match).NotNull().WithMessage("Match data is required.");
        
        When(x => x.Match != null, () =>
        {
            RuleFor(x => x.Match.Team1Id).GreaterThan(0).WithMessage("Team1Id is required.");
            RuleFor(x => x.Match.Team2Id).GreaterThan(0).WithMessage("Team2Id is required.");
            RuleFor(x => x.Match.Team1Id).NotEqual(x => x.Match.Team2Id).WithMessage("Team 1 and Team 2 cannot be the same.");
            RuleFor(x => x.Match.MaxMaps).GreaterThan(0).WithMessage("MaxMaps must be greater than zero.");
        });
    }
}
