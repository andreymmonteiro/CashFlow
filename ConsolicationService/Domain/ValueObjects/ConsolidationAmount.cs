namespace ConsolicationService.Domain.ValueObjects
{
    public record struct ConsolidationAmount
    {
        private readonly decimal _debit;
        private readonly decimal _credit;

        public ConsolidationAmount(decimal amount)
        {
            _debit = amount < 0 ? Math.Abs(amount) : 0;
            _credit = amount > 0 ? amount : 0;
        }

        public static implicit operator ConsolidationAmount(decimal amount)
            => new (amount);

        public decimal Debit => _debit;

        public decimal Credit => _credit;

        public decimal TotalAmount => _credit - _debit;
    }
}
