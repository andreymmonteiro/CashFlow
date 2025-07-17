namespace ConsolidationService.Presentation.Dtos.Request;

public class DailyConsolidationRequest
{
    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public Guid AccountId { get; set; }
}
