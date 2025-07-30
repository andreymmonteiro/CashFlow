using BalanceService.Infrastructure.Projections;

namespace BalanceService.Infrastructure.Repositories;

public interface IBalanceRepository
{
    Task SaveAsync(BalanceProjection projection, string streamId, CancellationToken cancellationToken);
}
