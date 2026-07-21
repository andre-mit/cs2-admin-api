using Cs2Admin.API.Infrastructure.Repositories;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Application.Matches.Commands;

public sealed class UpdateMatchCommandHandler(IMatchRepository repository) : ICommandHandler<UpdateMatchCommand, bool>
{
    public async ValueTask<bool> Handle(UpdateMatchCommand request, CancellationToken cancellationToken)
    {
        if (request.Id != request.Match.Id)
        {
            return false;
        }

        try
        {
            repository.Update(request.Match);
            await repository.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            var exists = await repository.ExistsAsync(e => e.Id == request.Id);
            if (!exists)
            {
                return false;
            }
            throw;
        }
    }
}
