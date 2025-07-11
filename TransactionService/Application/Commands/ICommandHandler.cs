namespace TransactionService.Application.Commands
{
    public interface ICommandHandler<TParameter, TResponse>
    {
        Task<TResponse> HandleAsync(TParameter command, CancellationToken cancellationToken);
    }
}
