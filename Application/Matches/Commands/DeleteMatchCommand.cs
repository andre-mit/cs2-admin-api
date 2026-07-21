using Mediator;

namespace Cs2Admin.API.Application.Matches.Commands;

public sealed record DeleteMatchCommand(int Id) : ICommand<bool>;
