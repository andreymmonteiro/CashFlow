namespace ConsolidationService.Application.Commands;

public record CreateConsolidationCommand(string AccountId, decimal Amount, DateTime CreatedAt) : Command;