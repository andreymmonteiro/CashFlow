namespace BalanceService.Domain.ValueObjects
{
    public record struct BalanceAmount
    {
        private readonly decimal _value;

        private BalanceAmount(decimal debit, decimal credit)
            => _value = credit - debit;

        public static implicit operator BalanceAmount((decimal debit, decimal credit) amounts)
            => new (amounts.debit, amounts.credit);

        public decimal Amount => _value;
    }
}
