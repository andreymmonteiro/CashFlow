namespace BalanceService.Domain.ValueObjects
{
    public record struct BalanceAmount
    {
        private readonly decimal _value;

        private BalanceAmount(decimal debit, decimal credit)
            => _value = credit - debit;

        public static implicit operator BalanceAmount((decimal debit, decimal credit) amounts)
            => new (amounts.debit, amounts.credit);

        public static implicit operator decimal(BalanceAmount balanceAmount) => balanceAmount._value;
    }
}
