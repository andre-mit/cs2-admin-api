using Cs2Admin.API.Models;
using Mediator;

namespace Cs2Admin.API.Application.Matches.Commands;

public sealed record CreateMatchCommand(Match Match) : ICommand<Match>;
