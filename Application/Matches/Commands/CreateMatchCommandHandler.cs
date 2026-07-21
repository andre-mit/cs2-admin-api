using Cs2Admin.API.Infrastructure.Repositories;
using Cs2Admin.API.Models;
using Mediator;

namespace Cs2Admin.API.Application.Matches.Commands;

public sealed class CreateMatchCommandHandler(IMatchRepository repository) : ICommandHandler<CreateMatchCommand, Match>
{
    public async ValueTask<Match> Handle(CreateMatchCommand request, CancellationToken cancellationToken)
    {
        await repository.AddAsync(request.Match);
        await repository.SaveChangesAsync();
        return request.Match;
    }
}
