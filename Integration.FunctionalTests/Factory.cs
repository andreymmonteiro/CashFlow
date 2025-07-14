using Microsoft.AspNetCore.Mvc.Testing;

namespace Integration.FunctionalTests
{
    public class Factory
    {
        public class TransactionApiFactory : WebApplicationFactory<TransactionService.Program> { }
        public class ConsolidationApiFactory : WebApplicationFactory<ConsolidationService.Program> { }
        public class BalanceApiFactory : WebApplicationFactory<BalanceService.Program> { }

    }
}