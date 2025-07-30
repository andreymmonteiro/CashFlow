namespace ConsolidationService.Application.Commands;

public interface ICommandHandler<TCommand>
    where TCommand : Command
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}
