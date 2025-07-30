namespace BalanceService.Application.Commands;

public interface ICommandHandler<TCommand>
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}
