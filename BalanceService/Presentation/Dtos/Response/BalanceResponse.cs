using BalanceService.Infrastructure.Projections;

namespace BalanceService.Presentation.Dtos.Response;

public record BalanceResponse(string AccountId, decimal Amount)
{

    public static explicit operator BalanceResponse(BalanceProjection balanceProjection)
        => new(balanceProjection?.AccountId ?? string.Empty, balanceProjection?.Amount ?? 0);
}
