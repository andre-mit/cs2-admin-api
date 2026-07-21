using Cs2Admin.API.Models;
using Mediator;

namespace Cs2Admin.API.Application.Matches.Queries;

public sealed record GetMatchesQuery() : IQuery<IEnumerable<Match>>;
