namespace BalanceService.Application.Queries;

public interface IQueryHandler<TParameter, TResponse>
{
    Task<TResponse> HandleAsync(TParameter parameter, CancellationToken cancellationToken);
}
