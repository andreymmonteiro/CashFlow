namespace BalanceService.Application.Commands;

public interface ICommandHandler<TCommand, TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
