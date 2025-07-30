namespace ConsolidationService.Application.Commands;

public record CreateConsolidationCommand(Guid AccountId, decimal Amount, DateTime CreatedAt) : Command;