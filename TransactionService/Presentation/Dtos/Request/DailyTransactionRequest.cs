namespace TransactionService.Presentation.Dtos.Request
{
    public class DailyTransactionRequest
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public Guid AccountId { get; set; }
    }
}
