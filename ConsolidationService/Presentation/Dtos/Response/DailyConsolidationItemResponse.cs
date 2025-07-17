using ConsolidationService.Infrastructure.Projections;

namespace ConsolidationService.Presentation.Dtos.Response;

public class DailyConsolidationItemResponse
{
    public decimal TotalCredits { get; set; }

    public decimal TotalDebits { get; set; }

    public decimal Balance => TotalCredits - TotalDebits;

    public DateTime Date { get; set; }

    private DailyConsolidationItemResponse(decimal totalCredits, decimal totalDebits, DateTime date)
    {
        TotalCredits = totalCredits;
        TotalDebits = totalDebits;
        Date = date;
    }

    public static explicit operator DailyConsolidationItemResponse(ConsolidationProjection consolidation)
    {
        return new DailyConsolidationItemResponse(consolidation.TotalCredits, consolidation.TotalDebits, consolidation.Date);
    }
}
