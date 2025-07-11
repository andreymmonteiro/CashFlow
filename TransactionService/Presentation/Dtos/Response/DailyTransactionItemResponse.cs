using TransactionService.Infrastructure.Projections;

namespace TransactionService.Presentation.Dtos.Response
{
    public record DailyTransactionItemResponse(decimal Amount, DateTime Date, string TransactionId)
    {
        public static explicit operator DailyTransactionItemResponse(TransactionProjection projection)
            => new (projection.Amount, projection.CreatedAt, projection.TransactionId);
    }
}
