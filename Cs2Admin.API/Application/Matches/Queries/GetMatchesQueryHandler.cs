using Cs2Admin.API.Infrastructure.Repositories;
using Cs2Admin.API.Models;
using Mediator;

namespace Cs2Admin.API.Application.Matches.Queries;

public sealed class GetMatchesQueryHandler(IMatchRepository repository) : IQueryHandler<GetMatchesQuery, IEnumerable<Match>>
{
    public async ValueTask<IEnumerable<Match>> Handle(GetMatchesQuery request, CancellationToken cancellationToken)
    {
        return await repository.GetMatchesWithDetailsAsync();
    }
}
