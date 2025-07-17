namespace ConsolidationService.Presentation.Dtos.Response;

public class DailyConsolidationResponse
{
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public IList<DailyConsolidationItemResponse> Items { get; }

    public decimal TotalDebits { get; }

    public decimal TotalCredits { get; }

    public decimal TotalBalance { get; }

    public DailyConsolidationResponse(DateTime? startDate, DateTime? endDate, IList<DailyConsolidationItemResponse> items)
    {
        StartDate = startDate;
        EndDate = endDate;
        Items = items;

        decimal totalDebits = 0;
        decimal totalCredits = 0;

        foreach (var item in items)
        {
            totalCredits += item.TotalCredits;
            totalDebits += item.TotalDebits;
        }

        TotalCredits = totalCredits;
        TotalDebits = totalDebits;
        TotalBalance = totalCredits - totalDebits;
    }
}