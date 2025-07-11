namespace TransactionService.Application.Commands
{
    public interface ICommandHandler<TCommand, TResponse>
    {
        /// <summary>
        /// Handles the command asynchronously.
        /// </summary>
        /// <param name="command">The command to handle.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation, containing the response of type TResponse.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the command is null.</exception>
        Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
    }
}
