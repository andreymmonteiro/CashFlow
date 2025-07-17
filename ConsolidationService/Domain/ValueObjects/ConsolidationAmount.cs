namespace ConsolidationService.Domain.ValueObjects;

public record struct ConsolidationAmount
{
    private readonly decimal _debit;
    private readonly decimal _credit;
    private readonly decimal _amount;

    public ConsolidationAmount(decimal amount)
    {
        if (amount == 0)
        {
            throw new InvalidOperationException("Amount cannot be zero.");
        }

        _debit = amount < 0 ? Math.Abs(amount) : 0;
        _credit = amount > 0 ? amount : 0;
        _amount = amount;
    }

    public static implicit operator ConsolidationAmount(decimal amount)
        => new(amount);

    public static implicit operator decimal(ConsolidationAmount consolidationAmount)
        => consolidationAmount._amount;

    public readonly decimal Debit => _debit;

    public readonly decimal Credit => _credit;

    public readonly decimal TotalAmount => _credit - _debit;
}
