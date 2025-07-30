namespace ConsolidationService.Infrastructure.Logging;

public interface IExceptionNotifier
{
    Task Notify(Exception exception, string dlqName, string message, CancellationToken cancellationToken);
}