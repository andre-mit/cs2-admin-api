using Cs2Admin.API.Infrastructure.Repositories;
using Mediator;

namespace Cs2Admin.API.Application.Matches.Commands;

public sealed class DeleteMatchCommandHandler(IMatchRepository repository) : ICommandHandler<DeleteMatchCommand, bool>
{
    public async ValueTask<bool> Handle(DeleteMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await repository.GetByIdAsync(request.Id);
        if (match == null)
        {
            return false;
        }

        repository.Remove(match);
        await repository.SaveChangesAsync();
        return true;
    }
}
