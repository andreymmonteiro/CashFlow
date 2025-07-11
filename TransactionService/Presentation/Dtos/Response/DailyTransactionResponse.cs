namespace TransactionService.Presentation.Dtos.Response;

public record DailyTransactionResponse(IEnumerable<DailyTransactionItemResponse> Items);
