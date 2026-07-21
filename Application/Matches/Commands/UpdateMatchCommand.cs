using Cs2Admin.API.Models;
using Mediator;

namespace Cs2Admin.API.Application.Matches.Commands;

public sealed record UpdateMatchCommand(int Id, Match Match) : ICommand<bool>;
