using Cs2Admin.API.Infrastructure.Repositories;
using Cs2Admin.API.Models;
using Mediator;

namespace Cs2Admin.API.Application.Matches.Queries;

public sealed class GetMatchQueryHandler(IMatchRepository repository) : IQueryHandler<GetMatchQuery, Match?>
{
    public async ValueTask<Match?> Handle(GetMatchQuery request, CancellationToken cancellationToken)
    {
        return await repository.GetMatchWithDetailsAsync(request.Id);
    }
}
