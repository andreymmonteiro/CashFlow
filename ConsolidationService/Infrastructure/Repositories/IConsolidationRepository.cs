using ConsolidationService.Infrastructure.Projections;

namespace ConsolidationService.Infrastructure.Repositories;

public interface IConsolidationRepository
{
    Task SaveAsync(ConsolidationProjection projection, string streamId, CancellationToken cancellationToken);
}
