namespace ConsolicationService.Application.Commands
{
    public interface ICommandHandler<TCommand, TResponse> 
        where TCommand : Command
    {
        Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
    }
}
